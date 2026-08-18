using System;
using System.Collections.Generic;
using UnityEngine;

public enum RunPhase
{
    None,
    Placement,  // 배치 단계: 적 없음, 영웅 자유 배치, '전투 시작' 버튼 대기
    Battle,     // 교전 중
    Recruit,    // 영입 선택 (※ 현재 보류 — 이벤트 꺼둠)
    Prep,       // 정비 (장비 장착/이전) → 다음 전투
    RunClear,
    RunFailed,
}

/// <summary>
/// 런의 페이즈 머신. 전투(BattleController)와 UI 사이의 유일한 흐름 창구.
///
/// 흐름:
///   StartRun(시작 3명)
///   → Placement(영웅 배치) → BeginCombat(시작 버튼) → Battle
///   → 승리 → 장비 드랍 → [영입: 보류] → Prep → 다음 Placement ...
///   → 마지막 전투 승리 → RunClear (미해금 영웅 해금 판정 + 영구 저장)
///   → 패배 시 즉시 RunFailed (RunState 폐기 = 장비/파티 소멸)
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("연결 (부트스트랩 또는 인스펙터에서 주입)")]
    public RunConfig config;
    public HeroDatabase heroDatabase;
    public BattleController battleController;
    public List<EquipmentDefinition> equipmentPool = new List<EquipmentDefinition>();

    public PlayerProfile Profile { get; private set; }
    public RunState Run { get; private set; }
    public RunPhase Phase { get; private set; } = RunPhase.None;

    public List<HeroDefinition> CurrentCandidates { get; } = new List<HeroDefinition>();
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
        EnterPlacement();
    }

    // ---------- 배치 → 전투 ----------

    void EnterPlacement()
    {
        Run.inBattle = true; // 배치 시점부터 장비 변경 잠금 (GDD 8)
        battleController.SetupBattle(Run, config); // 영웅만 스폰, AI 정지 상태
        SetPhase(RunPhase.Placement);
    }

    /// <summary>'전투 시작' 버튼이 호출</summary>
    public void BeginCombat()
    {
        if (Phase != RunPhase.Placement) return;
        SetPhase(RunPhase.Battle);
        battleController.BeginCombat();
    }

    // ---------- 전투 결과 보고 (BattleController가 호출) ----------

    public void ReportBattleWon()
    {
        Run.inBattle = false;
        GrantEquipmentDrops();

        if (ShouldRecruitNow())
        {
            GenerateCandidates();
            SetPhase(RunPhase.Recruit);
        }
        else
        {
            EnterPrepOrClear();
        }
    }

    public void ReportBattleLost()
    {
        Run.inBattle = false;
        SetPhase(RunPhase.RunFailed);
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

        if (CurrentCandidates.Count == 0) EnterPrepOrClear();
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

        EnterPrepOrClear();
    }

    // ---------- 정비 → 다음 전투 / 런 종료 ----------

    void EnterPrepOrClear()
    {
        if (Run.battleNumber >= config.battlesPerRun) FinishRunClear();
        else SetPhase(RunPhase.Prep);
    }

    public void ContinueToNextBattle()
    {
        if (Phase != RunPhase.Prep) return;
        Run.battleNumber++;
        EnterPlacement();
    }

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
        if (equipmentPool.Count == 0) return;
        for (int i = 0; i < config.equipmentDropsPerBattle; i++)
            Run.inventory.Add(equipmentPool[UnityEngine.Random.Range(0, equipmentPool.Count)]);
    }

    void SetPhase(RunPhase phase)
    {
        Phase = phase;
        OnPhaseChanged?.Invoke(phase);
    }
}
