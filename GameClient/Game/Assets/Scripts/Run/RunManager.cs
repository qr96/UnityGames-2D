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
///      · 계단           → Explore 유지 + IsAtStairs=true (HUD가 [귀환]/[내려가기] 버튼 표시)
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

    public List<HeroDefinition> CurrentCandidates { get; } = new List<HeroDefinition>();
    public List<EquipmentDefinition> LastDrops { get; } = new List<EquipmentDefinition>();
    public List<string> LastUnlockedHeroNames { get; } = new List<string>();

    public event Action<RunPhase> OnPhaseChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Profile = PlayerProfile.Load();
    }

    // ---------- 런 시작 (GDD 6: 보유 영웅 중 3명 선택 후 시작) ----------

    public void StartRun(List<HeroDefinition> starters)
    {
        Run = new RunState(starters, config.recruitChances);
        Run.battleNumber = 0;
        AssignStarterWeapons(); // ※ 임시 — 무기 획득/생성 흐름은 장비 개편에서
        World = new WorldState(world, world.defaultStartLocation); // 시작 위치는 임시 (미확정)
        battleController.ClearField(); // 이전 런의 파티 정리
        EnterExplore();
    }

    /// <summary>
    /// ※ 임시 (장비 개편 전): 무기 획득 경로가 아직 없어 시작 시 무기를 자동 지급.
    /// 각 영웅의 액티브 무기 조건에 맞는 무기를 우선 지급해 스킬 테스트가 가능하게 함.
    /// 같은 영웅은 같은 무기를 받도록 id 시드 고정.
    /// </summary>
    void AssignStarterWeapons()
    {
        var weapons = new List<WeaponDefinition>();
        foreach (var e in equipmentPool)
            if (e is WeaponDefinition w) weapons.Add(w);
        if (weapons.Count == 0) return;

        foreach (var h in Run.party)
        {
            var req = h.owned != null && h.owned.activeSkill != null
                ? h.owned.activeSkill.weaponRequirement
                : WeaponRequirement.None;

            var matching = weapons.FindAll(w => WeaponRules.Meets(w, req));
            var pickFrom = matching.Count > 0 ? matching : weapons;

            int seed = 7;
            foreach (char c in h.definition.id) seed = seed * 31 + c;
            h.weapon = pickFrom[Mathf.Abs(seed) % pickFrom.Count]; // 직접 지급 (인벤토리 경유 X)
        }
    }

    /// <summary>임시 로비: 해금 영웅 중 앞의 3명으로 런 시작 (영웅 선택 UI는 추후)</summary>
    public void StartDefaultRun()
    {
        var unlocked = Profile.GetUnlockedHeroes(heroDatabase);
        var starters = unlocked.GetRange(0, Mathf.Min(RunState.StartPartySize, unlocked.Count));
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

        // 보물 (미회수): 기존 전리품 팝업 재사용 — 확인 시 Explore 복귀
        if (World.CanLoot(loc))
        {
            World.MarkLooted(loc);
            GrantEquipmentDrops();
            SetPhase(RunPhase.Loot);
            return;
        }

        // 계단 포함 그 외: Explore — 계단이면 IsAtStairs=true (HUD가 귀환/내려가기 버튼 표시)
        EnterExplore();
    }

    // ---------- 계단 (탐험 규칙: 발견 시 귀환 혹은 내려가기 선택 가능) ----------
    // 별도 페이즈를 만들지 않고 Explore에서 선택 — HUD 미지원 상태에서도 이동이 막히지 않음.
    // HUD 연동: Explore 중 IsAtStairs면 [내려가기](CanDescend일 때)/[귀환] 버튼 표시.

    /// <summary>현재 계단 위에 서 있는가 (Explore 중에만 유효).</summary>
    public bool IsAtStairs =>
        Phase == RunPhase.Explore &&
        World != null && World.Current != null &&
        World.Current.fixedFunction == LocationFunction.Stairs;

    /// <summary>내려갈 수 있는가 (마지막 층 계단은 descendTo가 없음).</summary>
    public bool CanDescend => IsAtStairs && World.Current.descendTo != null;

    /// <summary>[내려가기] — 다음 층 시작 장소로 이동 (이동 연출 재사용).</summary>
    public void DescendStairs()
    {
        if (!CanDescend) return;
        TravelDestination = World.Current.descendTo;
        SetPhase(RunPhase.Travel);
        travelController.BeginTravel(Run, World.Current, TravelDestination,
            destinationVisited: World.IsVisited(TravelDestination));
    }

    /// <summary>[귀환] — 획득물을 유지하고 런 종료. ※ 임시 정책: 귀환 = 런 클리어 처리.</summary>
    public void ReturnFromStairs()
    {
        if (!IsAtStairs) return;
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
        battleController.SetupBattle(Run, config, World.Current);
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

        GrantEquipmentDrops();
        SetPhase(RunPhase.Loot);
    }

    public void ReportBattleLost()
    {
        Run.inBattle = false;
        SetPhase(RunPhase.RunFailed);
    }

    /// <summary>전리품 팝업의 '확인' 버튼이 호출</summary>
    public void ConfirmLoot()
    {
        if (Phase != RunPhase.Loot) return;

        if (ShouldRecruitNow())
        {
            GenerateCandidates();
            SetPhase(RunPhase.Recruit);
        }
        else
        {
            EnterExplore();
        }
    }

    // ---------- 영입 (※ 보류 중 — recruitChances=0으로 꺼둠. 방식 확정 후 재작업) ----------

    bool ShouldRecruitNow()
    {
        if (Run.recruitChancesLeft <= 0) return false;
        if (Run.party.Count >= RunState.MaxPartySize) return false;
        return Array.IndexOf(config.recruitAfterBattle, Run.battleNumber) >= 0;
    }

    void GenerateCandidates()
    {
        CurrentCandidates.Clear();
        var pool = new List<HeroDefinition>();
        foreach (var h in heroDatabase.heroes)
            if (h != null && !Run.Contains(h)) pool.Add(h);

        int count = Mathf.Min(config.candidatesPerRecruit, pool.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = UnityEngine.Random.Range(0, pool.Count);
            CurrentCandidates.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        if (CurrentCandidates.Count == 0) EnterExplore();
    }

    public void ChooseRecruit(int candidateIndex)
    {
        if (Phase != RunPhase.Recruit) return;
        if (candidateIndex < 0 || candidateIndex >= CurrentCandidates.Count) return;

        HeroDefinition def = CurrentCandidates[candidateIndex];
        bool wasLocked = !Profile.IsUnlocked(def);
        Run.Recruit(def, wasLocked);
        Run.recruitChancesLeft--;
        CurrentCandidates.Clear();

        EnterExplore();
    }

    // ---------- 런 종료 ----------

    void FinishRunClear()
    {
        // GDD 7 유력안: 미해금 영웅 영입 → 죽이지 않고 런 클리어 → 영구 해금
        LastUnlockedHeroNames.Clear();
        foreach (var h in Run.party)
        {
            if (h.recruitedWhileLocked && !h.diedThisRun && !Profile.IsUnlocked(h.definition))
            {
                Profile.Unlock(h.definition);
                LastUnlockedHeroNames.Add(h.definition.displayName);
            }
        }
        SetPhase(RunPhase.RunClear);
    }

    void GrantEquipmentDrops()
    {
        LastDrops.Clear();
        if (equipmentPool.Count == 0) return;
        for (int i = 0; i < config.equipmentDropsPerBattle; i++)
        {
            var item = equipmentPool[UnityEngine.Random.Range(0, equipmentPool.Count)];
            Run.inventory.Add(item);
            LastDrops.Add(item);
        }
    }

    void SetPhase(RunPhase phase)
    {
        Phase = phase;
        OnPhaseChanged?.Invoke(phase);
    }
}