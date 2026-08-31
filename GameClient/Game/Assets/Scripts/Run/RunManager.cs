using System;
using System.Collections.Generic;
using UnityEngine;

public enum RunPhase
{
    None,
    Explore,    // 탐험: 지도에서 다음 장소 선택 (GDD 월드)
    Travel,     // 이동 연출: 영웅들이 길을 따라 목적지로 걸어감 (GDD 11)
    Camp,       // 야영지: 파티 표시 + [휴식]/[떠나기] (야영지 기획 v0.1)
    Placement,  // 전투 준비: 영웅 자유 배치 + 장비 드래그 장착 + '전투 시작' 버튼
    Battle,     // 교전 중
    Loot,       // 전투 승리 → 획득 아이템 팝업
    Recruit,    // 영입 선택 (※ 현재 보류 — 이벤트 꺼둠)
    RunClear,
    RunFailed,
}

/// <summary>
/// 런의 페이즈 머신. 월드 탐험과 전투(BattleController), UI 사이의 유일한 흐름 창구.
///
/// 흐름 (랜덤 맵 통합):
///   StartRun(시작 파티) → 시작 장소에서 Explore
///   → 인접 장소 탭(TravelTo) → 도착
///      · 전투 필요(WorldState.ShouldBattle) → Placement → Battle
///      · 보물(미회수)   → Loot 팝업
///      · 휴식 장소      → Camp
///      · 계단           → 도달 = 확보(Secured) — 이후 층 어디서든 [귀환]/[하강] 가능 (명세 5)
///   → 전투 승리 → Loot → 확인 → Explore ...
///   → 랜드마크 전투 승리 = RunClear (고정 맵 호환)
///   → 계단에서 귀환 = RunClear (임시 정책 — 전리품/해금 유지하고 런 종료)
///   → 전멸 시 RunFailed (RunState 폐기 = 장비/파티 소멸)
///
/// 양방향 규칙: 지나간 장소로 되돌아갈 수 있음. 클리어한 장소는 재전투 없음
/// (특정 이벤트가 WorldState.ReArmBattle로 다시 걸 수 있음).
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("연결 (부트스트랩 또는 인스펙터에서 주입)")]
    public RunConfig config;

    [Header("랜덤 맵 (부트스트랩 주입 — 하강 시 다음 층을 그때 생성, 명세 22)")]
    public int mapSeed;
    public MapGenerator.Config mapConfig; // null = 고정 맵 (하강 불가)
    [Tooltip("출발 층 번호 (1 = 지상 입구, 그 외 = 개방된 진입 포인트 — 부트스트랩 주입)")]
    public int startFloorNumber = 1;
    int currentFloor; // 0-based 층 인덱스 (층 번호 = currentFloor + 1)

    [Header("장비 생성 (장비 명세 v1.2 — RewardLevel = 현재 층)")]
    public EquipmentGenerator.Config equipConfig = new EquipmentGenerator.Config();

    /// <summary>현재 층의 RewardLevel (1층 = RL 1)</summary>
    public int RewardLevel => currentFloor + 1;
    public HeroDatabase heroDatabase;
    public WorldDefinition world;
    public BattleController battleController;
    public TravelController travelController;
    public List<EquipmentDefinition> equipmentPool = new List<EquipmentDefinition>();

    public PlayerProfile Profile { get; private set; }
    public RunState Run { get; private set; }
    public WorldState World { get; private set; }
    public RunPhase Phase { get; private set; } = RunPhase.None;
    public LocationDefinition TravelDestination { get; private set; }

    public List<EquipmentDefinition> LastDrops { get; } = new List<EquipmentDefinition>();
    public List<string> LastUnlockedHeroNames { get; } = new List<string>();

    public event Action<RunPhase> OnPhaseChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Profile = PlayerProfile.Load();
    }

    // ---------- 런 시작 (영입 스펙 v1: 로스터에서 선택한 최대 5명 출전) ----------

    public void StartRun(List<OwnedHero> starters)
    {
        Run = new RunState(starters);
        Run.battleNumber = 0;
        currentFloor = Mathf.Max(0, startFloorNumber - 1); // 진입 포인트 출발 시 해당 층부터 (RewardLevel 연동)
        AssignStarterWeapons(); // ※ 임시 — 무기 획득/생성 흐름은 장비 개편에서
        World = new WorldState(world, world.defaultStartLocation); // 시작 위치는 임시 (미확정)
        battleController.ClearField(); // 이전 런의 파티 정리
        EnterExplore();
    }

    /// <summary>기존 정의 기반 호출 호환 — 로스터에서 해당 정의의 보유 영웅을 찾아 출전</summary>
    public void StartRun(List<HeroDefinition> starters)
    {
        var owned = new List<OwnedHero>();
        foreach (var d in starters)
        {
            var h = HeroRoster.Get(d);
            if (h != null) owned.Add(h);
        }
        StartRun(owned);
    }

    /// <summary>
    /// ※ 임시 (무기 획득 흐름은 드랍이 담당하지만, 무기 없는 영웅은 기본 공격 불가라 최초 1회 지급):
    /// 시작 영웅은 표 확정 무기, 랜덤 영웅은 액티브 조건에 맞는 무기를 RL1로 '생성'해 지급.
    /// 장비 영속 v1: 무기가 영웅에 영구 유지되므로 무기가 '없는' 영웅에게만 지급 (복제 방지).
    /// </summary>
    void AssignStarterWeapons()
    {
        var rng = new System.Random();
        foreach (var h in Run.party)
        {
            if (h.weapon != null) continue; // 이미 보유 — 영속 유지 (재지급 = 복제 버그)

            WeaponType type;
            if (h.owned != null && h.owned.hasFixedWeapon)
            {
                type = h.owned.fixedWeapon; // 시작 영웅: 브란=검, 리나=활, 오웬=마법 도구
            }
            else
            {
                var req = h.owned != null && h.owned.activeSkill != null
                    ? h.owned.activeSkill.weaponRequirement
                    : WeaponRequirement.None;
                switch (req)
                {
                    case WeaponRequirement.Bow: type = WeaponType.Bow; break;
                    case WeaponRequirement.MagicTool: type = WeaponType.MagicTool; break;
                    case WeaponRequirement.Melee: type = WeaponType.Sword; break;
                    default: type = (WeaponType)rng.Next(5); break;
                }
            }
            h.weapon = EquipmentGenerator.GenerateWeapon(type, rewardLevel: 1, special: false, equipConfig, rng);
        }
    }

    /// <summary>임시 로비/직접 실행: 로스터 앞에서부터 최대 5명으로 출전 (출전 선택 UI는 로비 개편에서)</summary>
    public void StartDefaultRun()
    {
        HeroRoster.EnsureStarters(heroDatabase); // 비어 있으면 시작 3명 지급 (전멸 소프트락 방지 겸용)
        var starters = new List<OwnedHero>();
        foreach (var h in HeroRoster.Heroes)
        {
            if (starters.Count >= RunState.MaxPartySize) break;
            starters.Add(h);
        }
        StartRun(starters);
    }

    // ---------- 탐험 → 이동 → 도착 ----------

    void EnterExplore()
    {
        // 파티가 현재 장소에 서 있음 — 방금 전투한 장소면 종료 위치 그대로 유지.
        // (길을 따라 걷는 이동 연출은 다음 단계 — GDD 11)
        battleController.ShowPartyAt(Run, World.Current);
        SetPhase(RunPhase.Explore);
    }

    /// <summary>방향 선택 UI가 호출 — 해당 방향 출구로 이동 (양방향 — 재방문 가능)</summary>
    public void TravelInDirection(Direction dir)
    {
        if (Phase != RunPhase.Explore) return;
        LocationDefinition destination = World != null ? World.GetAvailableExit(dir) : null;
        if (destination == null) return;

        TravelDestination = destination;
        SetPhase(RunPhase.Travel);
        travelController.BeginTravel(Run, World.Current, destination,
            destinationVisited: World.IsVisited(destination));
    }

    /// <summary>이동 연출 완료 시 TravelController가 호출</summary>
    public void CompleteTravel()
    {
        if (Phase != RunPhase.Travel) return;
        World.MoveTo(TravelDestination);
        TravelDestination = null;
        ArriveAtCurrent();
    }

    void ArriveAtCurrent()
    {
        LocationDefinition loc = World.Current;

        // 휴식 기능 장소 (예배당/야영지): 휴식 화면 (진입 → 파티 표시 → 휴식/떠나기)
        if (loc.fixedFunction == LocationFunction.Rest)
        {
            battleController.ShowPartyAt(Run, loc);
            SetPhase(RunPhase.Camp);
            return;
        }

        // 전투 판정: 클리어한 장소는 재전투 없음 (이벤트로 재장전된 경우만 예외)
        if (World.ShouldBattle(loc))
        {
            EnterPlacement();
            return;
        }

        // 보물 (미회수): 전투 없이 다량 획득 (던전 명세 — 3개/특별 3%)
        if (World.CanLoot(loc))
        {
            World.MarkLooted(loc);
            GrantEquipmentDrops(NodeContent.Treasure);
            SetPhase(RunPhase.Loot);
            return;
        }

        // 외부 진입 포인트: 도달 즉시 영구 개방 (던전 명세 — 프로토: 개통 이벤트 생략 확정)
        if (loc.fixedFunction == LocationFunction.EntryPoint)
        {
            if (DungeonProgress.Open(RewardLevel))
                Debug.Log($"[RunManager] 외부 진입 포인트 개방 — 지하 {RewardLevel}층 (이후 출발 지점 선택 가능)");
        }

        // 계단 포함 그 외: Explore — 계단은 도달 시점에 이미 확보됨(WorldState.MarkVisited)
        EnterExplore();
    }

    // ---------- 계단 확보 (명세 5·20) ----------
    // 계단방에 도달하면 Secured (WorldState가 처리) — 이후 층 어디서든 [귀환]/[하강] 가능.
    // 백트래킹 없음: 클리어된 안전 경로로 계단까지 돌아갔다고 추상화 (명세 5.2·21).

    /// <summary>귀환 가능 — 계단 확보 '또는' 진입 포인트 개방 후 Explore 중이면 위치와 무관하게 (명세 5.2 + 던전 명세).</summary>
    public bool CanReturn =>
        Phase == RunPhase.Explore && World != null
        && (World.StairsSecured || World.EntryPointReached);

    /// <summary>하강 가능 — '계단' 확보 + (다음 층이 이미 있거나 랜덤 맵이라 생성 가능). 최심부는 계단이 없어 자연 차단.</summary>
    public bool CanDescend =>
        Phase == RunPhase.Explore && World != null && World.StairsSecured
        && (World.SecuredStairs.descendTo != null
            || (mapConfig != null && currentFloor + 2 <= mapConfig.maxFloor));

    /// <summary>[하강] — 다음 층을 이 시점에 생성해 이어 붙이고 이동 (명세 22).</summary>
    public void DescendStairs()
    {
        if (!CanDescend) return;

        LocationDefinition stairs = World.SecuredStairs;
        if (stairs.descendTo == null) // 다음 층 신규 생성 (랜덤 맵)
        {
            var region = MapGenerator.GenerateFloor(currentFloor + 1, mapSeed, mapConfig,
                out var nextStart, out _);
            world.regions.Add(region);
            stairs.descendTo = nextStart;
        }
        currentFloor++;

        TravelDestination = stairs.descendTo;
        SetPhase(RunPhase.Travel);
        travelController.BeginTravel(Run, World.Current, TravelDestination,
            destinationVisited: World.IsVisited(TravelDestination));
    }

    /// <summary>[귀환] — 확보한 전리품을 유지하고 원정 종료 (명세 21). ※ 임시 정책: 귀환 = 런 클리어 처리.
    /// 전리품 '영구 보관'(명세 21)은 장비 영속화가 없어 미구현 — 장비 개편에서 처리.</summary>
    public void ReturnFromStairs()
    {
        if (!CanReturn) return;
        FinishRunClear();
    }

    // ---------- 야영지 ----------

    /// <summary>[휴식] — 생존 영웅 HP 완전 회복. 사망자 제외 (부활은 교회 담당). 무료·무제한.</summary>
    public void RestAtCamp()
    {
        if (Phase != RunPhase.Camp) return;
        battleController.HealPartyAtCamp();
    }

    /// <summary>[떠나기] — 탐험 복귀. 휴식하지 않고 바로 떠나는 것도 가능 (야영지 기획 5).</summary>
    public void LeaveCamp()
    {
        if (Phase != RunPhase.Camp) return;
        EnterExplore();
    }

    // ---------- 전투 준비 → 교전 ----------

    void EnterPlacement()
    {
        Run.battleNumber++;             // 이번 런에서 몇 번째 전투인지 (적 스케일링 기준)
        Run.inBattle = false;           // 배치 = '전투 사이' → 장비 변경 허용 (GDD 8)
        battleController.SetupBattle(Run, config, World.Current, RewardLevel);
        SetPhase(RunPhase.Placement);
    }

    /// <summary>'전투 시작' 버튼이 호출</summary>
    public void BeginCombat()
    {
        if (Phase != RunPhase.Placement) return;
        Run.inBattle = true; // 이 순간부터 장비 변경 잠금 (GDD 8: 전투 중 변경 불가)
        SetPhase(RunPhase.Battle);
        battleController.BeginCombat();
    }

    // ---------- 전투 결과 보고 (BattleController가 호출) ----------

    public void ReportBattleWon()
    {
        Run.inBattle = false;
        World.MarkBattleCleared(World.Current);

        // 확정: 랜드마크(보스 지역) 클리어 = 런 클리어
        if (World.Current.type == LocationType.Landmark)
        {
            FinishRunClear();
            return;
        }

        // 획득처: 엘리트 2개/특별 8%, 일반 1개/특별 1% (던전 명세)
        GrantEquipmentDrops(World.Current.nodeType == NodeContent.EliteBattle
            ? NodeContent.EliteBattle : NodeContent.NormalBattle);
        SetPhase(RunPhase.Loot);
    }

    public void ReportBattleLost()
    {
        Run.inBattle = false;

        // 탐험 규칙: 전멸 시 '이번 원정에서 획득한 전리품'만 소멸 (기존 보관 장비는 유지 — 장비 영속 v1).
        // 획득 후 장착된 것은 착용자(사망)와 함께 소멸하므로 보관소에서 못 찾아도 정상.
        foreach (var item in Run.acquiredThisRun)
            Armory.RemoveOnce(item);
        Run.acquiredThisRun.Clear();

        EndExpedition(); // 전멸 = 원정 종료 (출전 사망자 전원 영구 제거 — 로스터가 비면 다음 진입 시 시작 3명 재지급)
        SetPhase(RunPhase.RunFailed);
    }

    /// <summary>전리품 팝업의 '확인' 버튼이 호출 (런 내 영입 폐지 — 영입은 로비 상점)</summary>
    public void ConfirmLoot()
    {
        if (Phase != RunPhase.Loot) return;
        EnterExplore();
    }

    // ---------- 런 종료 ----------

    void FinishRunClear()
    {
        LastUnlockedHeroNames.Clear(); // 해금 모델 폐지 (영입 스펙 v1: 소유의 원천은 로스터)
        EndExpedition();
        SetPhase(RunPhase.RunClear);
    }

    /// <summary>원정 1회 종료 공통 처리 (클리어/실패/귀환 모두) — 영입 스펙 v1</summary>
    void EndExpedition()
    {
        int dead = HeroRoster.RemoveDeadFrom(Run.party); // 영구 사망 — 로스터에서 제거
        if (dead > 0) Debug.Log($"[RunManager] 영구 사망 {dead}명 — 로스터에서 제거");
        RecruitShop.Refresh(heroDatabase);               // 원정 종료 시 후보 3명 전체 교체
    }

    /// <summary>장비 드랍 (장비 명세 v1.2): 획득처가 개수/특별 확률을, 현재 층(RewardLevel)이 깡스탯 범위를 결정.</summary>
    void GrantEquipmentDrops(NodeContent source)
    {
        LastDrops.Clear();
        var rng = new System.Random();
        var drops = EquipmentGenerator.GenerateDrops(source, RewardLevel, equipConfig, rng);
        foreach (var item in drops)
        {
            Run.inventory.Add(item);          // = 보관소 — 획득 즉시 영구 반영 (장비 영속 v1)
            Run.acquiredThisRun.Add(item);    // 전멸 시 소멸 대상 추적
            LastDrops.Add(item);
        }
    }

    void SetPhase(RunPhase phase)
    {
        Phase = phase;
        OnPhaseChanged?.Invoke(phase);
    }
}