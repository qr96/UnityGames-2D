using UnityEngine;

/// <summary>
/// 영웅의 조건부 고유 특성 구동기 (특성 스펙 v1).
/// 전투 중 조건을 주기적으로 판정해 배수를 갱신하고, Hero가 이를 소비:
///   · AttackMult  → Hero.AttackPower에 합류 (기본 공격 + 모든 스킬에 자동 반영)
///   · DamageReductionSum → TotalDR 합산 기여 (장비 명세 §6) / TakenIncreaseMult → 별도 곱 (무모함)
///   · DamageMultVsTarget(target) → 처형인 전용 대상 조건 (피해 지점마다 적용)
///   · 생존본능은 조건 충족 시 즉시 발동 (전투당 1회)
/// 전투가 아닐 때는 항상 중립(1배). 전투 시작 시점 상태(초기 아군 수/시작 시각/1회성 플래그)를 리셋.
/// </summary>
public class TraitRunner : MonoBehaviour
{
    const float EvalInterval = 0.2f; // 주변 스캔 주기 (매 프레임은 과함)

    Hero hero;
    TraitCatalog.Entry trait;

    float evalTimer;
    bool combatWasActive;
    float combatStartTime;
    int alliesAtCombatStart;
    bool survivalUsed;

    /// <summary>주는 피해 배수 (자기/주변 조건 특성) — 1 = 중립</summary>
    public float AttackMult { get; private set; } = 1f;

    /// <summary>받는 피해 '감소' 기여 (0.30 = 30%) — TotalDR 합산에 들어감 (장비 명세 §6)</summary>
    public float DamageReductionSum { get; private set; }

    /// <summary>받는 피해 '증가' 배수 (무모함) — DR 합산과 별도 곱</summary>
    public float TakenIncreaseMult { get; private set; } = 1f;

    public TraitCatalog.Entry Trait => trait;

    public void Init(Hero hero, string traitId)
    {
        this.hero = hero;
        trait = TraitCatalog.Find(traitId);
        hero.AttachTraits(this);
    }

    /// <summary>처형인: 대상 HP가 임계 이하면 추가 배수. 그 외 특성은 항상 1.</summary>
    public float DamageMultVsTarget(Unit target)
    {
        if (trait == null || trait.kind != TraitKind.Executioner) return 1f;
        if (target == null || target.IsDead) return 1f;
        return target.HPRatio * 100f <= trait.threshold ? 1f + trait.power / 100f : 1f;
    }

    void Update()
    {
        if (hero == null || trait == null || hero.IsDead) return;

        if (!BattleController.CombatActive)
        {
            combatWasActive = false;
            AttackMult = 1f;
            DamageReductionSum = 0f;
            TakenIncreaseMult = 1f;
            return;
        }

        if (!combatWasActive)
            OnCombatStart();

        // 생존본능: 즉발형 — 스캔 주기와 무관하게 매 프레임 감시
        if (trait.kind == TraitKind.SurvivalInstinct && !survivalUsed
            && hero.HPRatio * 100f <= trait.threshold)
        {
            survivalUsed = true;
            hero.Heal(hero.MaxHP * trait.power / 100f);
        }

        evalTimer -= Time.deltaTime;
        if (evalTimer > 0f) return;
        evalTimer = EvalInterval;

        Evaluate();
    }

    void OnCombatStart()
    {
        combatWasActive = true;
        combatStartTime = Time.time;
        alliesAtCombatStart = CountAliveAllies();
        survivalUsed = false;
        AttackMult = 1f;
        DamageReductionSum = 0f;
        TakenIncreaseMult = 1f;
    }

    void Evaluate()
    {
        float atk = 1f, drSum = 0f, takenInc = 1f;
        float bonus = trait.power / 100f;

        switch (trait.kind)
        {
            case TraitKind.DesperateFighter: // 자신 HP 낮음 → 공격 증가
                if (hero.HPRatio * 100f <= trait.threshold) atk += bonus;
                break;

            case TraitKind.Tenacity: // 자신 HP 낮음 → 받는 피해 감소 (TotalDR 합산 기여)
                if (hero.HPRatio * 100f <= trait.threshold) drSum += bonus;
                break;

            case TraitKind.Duelist: // 주변 적 정확히 1명
                if (CountEnemiesWithin(trait.radius) == 1) atk += bonus;
                break;

            case TraitKind.Brawler: // 주변 적 threshold명 이상
                if (CountEnemiesWithin(trait.radius) >= Mathf.RoundToInt(trait.threshold)) atk += bonus;
                break;

            case TraitKind.LoneWolf: // 주변 아군 없음
                if (CountAlliesWithin(trait.radius) == 0) atk += bonus;
                break;

            case TraitKind.Camaraderie: // 주변 아군 있음 → 받는 피해 감소 (TotalDR 합산 기여)
                if (CountAlliesWithin(trait.radius) >= 1) drSum += bonus;
                break;

            case TraitKind.Guardian: // 주변 HP 낮은 아군 존재 → 받는 피해 감소 (TotalDR 합산 기여)
                if (HasLowHpAllyWithin(trait.radius, trait.threshold)) drSum += bonus;
                break;

            case TraitKind.Vengeance: // 이번 전투에서 아군 사망 발생 (전투 동안 유지)
                if (CountAliveAllies() < alliesAtCombatStart) atk += bonus;
                break;

            case TraitKind.Vanguard: // 전투 시작 후 duration초
                if (Time.time - combatStartTime <= trait.duration) atk += bonus;
                break;

            case TraitKind.Reckless: // 상시 — 주는 피해 증가 + '받는 피해 증가'(감소 합산이 아닌 별도 곱)
                atk += bonus;
                takenInc = 1f + bonus;
                break;

                // Executioner: 대상 조건 → DamageMultVsTarget에서 처리
                // SurvivalInstinct: 즉발형 → Update에서 처리
        }

        AttackMult = atk;
        DamageReductionSum = drSum;
        TakenIncreaseMult = takenInc;

        // 개발용 가시성: 발동 상태가 바뀔 때만 콘솔 로그 (배수는 UI가 없어 확인 수단이 이것뿐)
        bool active = !Mathf.Approximately(AttackMult, 1f) || DamageReductionSum > 0f
            || !Mathf.Approximately(TakenIncreaseMult, 1f);
        if (active != wasActive)
        {
            wasActive = active;
            string heroName = hero.Runtime != null && hero.Runtime.definition != null
                ? hero.Runtime.definition.displayName : hero.name;
            Debug.Log($"[특성] {heroName} — {trait.displayName} {(active ? "발동" : "해제")} " +
                      $"(공격 x{AttackMult:0.00}, DR +{DamageReductionSum * 100f:0}%, 받는 피해 x{TakenIncreaseMult:0.00})");
        }
    }

    bool wasActive;

    // ---------- 주변 판정 ----------

    int CountEnemiesWithin(float radius)
    {
        int count = 0;
        foreach (Unit u in UnitRegistry.GetAll(Team.Enemy))
            if (!u.IsDead && Dist(u) <= radius) count++;
        return count;
    }

    /// <summary>자신 제외 주변 아군 수</summary>
    int CountAlliesWithin(float radius)
    {
        int count = 0;
        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
            if (u != (Unit)hero && !u.IsDead && Dist(u) <= radius) count++;
        return count;
    }

    bool HasLowHpAllyWithin(float radius, float thresholdPercent)
    {
        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
            if (u != (Unit)hero && !u.IsDead && Dist(u) <= radius
                && u.HPRatio * 100f <= thresholdPercent)
                return true;
        return false;
    }

    int CountAliveAllies()
    {
        int count = 0;
        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
            if (!u.IsDead) count++;
        return count;
    }

    float Dist(Unit u) => Vector2.Distance(hero.transform.position, u.transform.position);
}