using System;
using System.Collections.Generic;
using UnityEngine;

public enum RunPhase
{
    None,
    Explore,    // 탐험: 지도에서 다음 장소 선택 (GDD 월드)
    Travel,     // 이동 연출: 영웅들이 길을 따라 목적지로 걸어감 (GDD 11)
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
/// 흐름 (월드 통합):
///   StartRun(시작 3명) → 시작 장소에서 Explore
///   → 인접 장소 탭(TravelTo) → 도착
///      · 전투 장소  → Placement(그 장소 좌표 위 전투장) → Battle
///      · 그 외 장소 → 콘텐츠 스텁 → 다시 Explore
///   → 전투 승리 → Loot → 확인 → Explore ...
///   → 랜드마크 전투 승리 = RunClear (확정: 랜드마크 클리어 = 런 클리어)
///   → 전멸 시 RunFailed (RunState 폐기 = 장비/파티 소멸)
///
/// 임시 결정(미확정 항목): 클리어한 장소는 재전투 없음 (config.refightClearedLocations로 변경 가능)
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
        World = new WorldState(world, world.defaultStartLocation); // 시작 위치는 임시 (미확정)
        battleController.ClearField(); // 이전 런의 파티 정리
        EnterExplore();
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

    /// <summary>지도에서 인접 장소를 탭하면 호출 (WorldMapView)</summary>
    public void TravelTo(LocationDefinition destination)
    {
        if (Phase != RunPhase.Explore) return;
        if (World == null || !World.CanMoveTo(destination)) return; // 인접해야만 이동 (GDD 5)

        TravelDestination = destination;
        SetPhase(RunPhase.Travel);
        travelController.BeginTravel(Run, World.Current, destination, World.IsVisited(destination));
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

        bool battle = loc.hasBattle &&
                      (config.refightClearedLocations || !World.IsBattleCleared(loc));

        if (battle)
        {
            EnterPlacement();
        }
        else
        {
            // 야영지/마을/클리어된 장소 — 장소 콘텐츠(회복, 상점 등)는 스텁 상태
            EnterExplore();
        }
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