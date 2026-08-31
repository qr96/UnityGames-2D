using UnityEngine;

/// <summary>
/// 특별 장비 고유 효과 구동기 (장비 명세 v1.2 §8~11).
/// 전투 시작 시 장착 장비(무기 + 자유 3칸)에서 효과별 개수를 집계 — 전투 중 장비 변경이
/// 불가하므로(GDD 8) 전투 내내 고정. 동일 효과 여러 개 = 효과량 합산 (§9).
///
///   LAST_STAND       HP ≤ 20% → 주는 피해 +30%/개
///   HEALTHY_FURY     HP ≥ 80% → 주는 피해 +20%/개
///   EXECUTION        대상 HP ≤ 30% → 해당 대상 피해 +25%/개
///   BLOODTHIRST      적 처치 → 최대 HP 5%/개 회복 (처치 크레딧은 Hero/SkillRunner가 전달)
///   DROP_FURY        내려놓음 → 3초간 주는 피해 +25%/개 — 재발동 시 3초로 '갱신' (§10)
///   DROP_GUARD       내려놓음 → 3초간 받는 피해 감소 25%/개 — TotalDR 합산 (§6), 갱신형
///   ACTIVE_STRIKE    액티브 발동 시 충전 → 다음 기본공격 +50%/개, 소비형·재충전 (§11)
///   EMERGENCY_SHIELD HP 최초 25% 이하 진입 → 최대 HP 20%/개 보호막, 전투당 1회 (§9: 개수만큼 합산)
///
/// 주는 피해 증가 계열은 전부 합연산 후 치명타 이전 적용 (§9) — 합산은 Hero가 수행.
/// 수치는 명세 고정값 (RewardLevel과 무관 — 깊이에 따라 강화되지 않음).
/// </summary>
public class UniqueEffectRunner : MonoBehaviour
{
    // ---- 명세 §8 고정값 ----
    const float LastStandThresholdPct = 20f, LastStandBonusPct = 30f;
    const float HealthyFuryThresholdPct = 80f, HealthyFuryBonusPct = 20f;
    const float ExecutionThresholdPct = 30f, ExecutionBonusPct = 25f;
    const float BloodthirstHealPct = 5f;
    const float DropDuration = 3f;
    const float DropFuryBonusPct = 25f, DropGuardDRPct = 25f;
    const float ActiveStrikeBonusPct = 50f;
    const float EmergencyThresholdPct = 25f, EmergencyShieldPct = 20f;
    const float EmergencyShieldDuration = 15f; // ※ 명세에 지속 없음 — 임시값

    Hero hero;

    // 장착 개수 (전투 시작 시 집계)
    int lastStand, healthyFury, execution, bloodthirst;
    int dropFury, dropGuard, activeStrike, emergencyShield;

    float dropFuryLeft, dropGuardLeft;
    bool activeStrikeCharged;
    bool emergencyUsed;
    bool combatWasActive;

    public void Init(Hero hero)
    {
        this.hero = hero;
        hero.AttachUniques(this);
    }

    void Update()
    {
        if (hero == null || hero.IsDead) return;

        if (!BattleController.CombatActive)
        {
            combatWasActive = false;
            dropFuryLeft = dropGuardLeft = 0f;
            return;
        }

        if (!combatWasActive)
            OnCombatStart();

        if (dropFuryLeft > 0f) dropFuryLeft -= Time.deltaTime;
        if (dropGuardLeft > 0f) dropGuardLeft -= Time.deltaTime;

        // EMERGENCY_SHIELD: 최초 임계 진입 시 1회 (§8) — 여러 개면 합산 발동 (§9)
        if (emergencyShield > 0 && !emergencyUsed
            && hero.HPRatio * 100f <= EmergencyThresholdPct)
        {
            emergencyUsed = true;
            float amount = hero.MaxHP * EmergencyShieldPct / 100f * emergencyShield;
            hero.GetStatus().AddShield(amount, EmergencyShieldDuration);
        }
    }

    void OnCombatStart()
    {
        combatWasActive = true;
        emergencyUsed = false;
        activeStrikeCharged = false;
        dropFuryLeft = dropGuardLeft = 0f;
        CountEquipped();
    }

    /// <summary>장착품에서 효과별 개수 집계 (무기 포함 — 특별 무기도 고유효과 보유 가능)</summary>
    void CountEquipped()
    {
        lastStand = healthyFury = execution = bloodthirst = 0;
        dropFury = dropGuard = activeStrike = emergencyShield = 0;

        var inst = hero.Runtime;
        if (inst == null) return;

        Count(inst.weapon);
        foreach (var eq in inst.equipment)
            Count(eq);
    }

    void Count(EquipmentDefinition eq)
    {
        if (eq == null || !eq.isSpecial) return;
        switch (eq.uniqueEffect)
        {
            case UniqueEffect.LastStand: lastStand++; break;
            case UniqueEffect.HealthyFury: healthyFury++; break;
            case UniqueEffect.Execution: execution++; break;
            case UniqueEffect.Bloodthirst: bloodthirst++; break;
            case UniqueEffect.DropFury: dropFury++; break;
            case UniqueEffect.DropGuard: dropGuard++; break;
            case UniqueEffect.ActiveStrike: activeStrike++; break;
            case UniqueEffect.EmergencyShield: emergencyShield++; break;
        }
    }

    // ---------- 주는 피해 (합연산 재료 — Hero가 §9 순서로 합산) ----------

    /// <summary>자기 조건 피해 보너스 % 합 (LAST_STAND + HEALTHY_FURY + DROP_FURY 활성분)</summary>
    public float SelfDamageBonusPercent
    {
        get
        {
            float hp = hero.HPRatio * 100f;
            float sum = 0f;
            if (lastStand > 0 && hp <= LastStandThresholdPct) sum += lastStand * LastStandBonusPct;
            if (healthyFury > 0 && hp >= HealthyFuryThresholdPct) sum += healthyFury * HealthyFuryBonusPct;
            if (dropFury > 0 && dropFuryLeft > 0f) sum += dropFury * DropFuryBonusPct;
            return sum;
        }
    }

    /// <summary>대상 조건 피해 보너스 % (EXECUTION — 대상 HP ≤ 30%)</summary>
    public float ExecutionBonusPercent(Unit target)
    {
        if (execution == 0 || target == null || target.IsDead) return 0f;
        return target.HPRatio * 100f <= ExecutionThresholdPct ? execution * ExecutionBonusPct : 0f;
    }

    /// <summary>ACTIVE_STRIKE 소비 — 충전 상태면 보너스 % 반환 후 미충전으로 (§11)</summary>
    public float ConsumeActiveStrikePercent()
    {
        if (activeStrike == 0 || !activeStrikeCharged) return 0f;
        activeStrikeCharged = false;
        return activeStrike * ActiveStrikeBonusPct;
    }

    // ---------- 받는 피해 ----------

    /// <summary>DROP_GUARD 활성 시 TotalDR 기여 (0.25/개 — 75% 상한은 Unit이 적용)</summary>
    public float DropGuardDR =>
        dropGuard > 0 && dropGuardLeft > 0f ? dropGuard * DropGuardDRPct / 100f : 0f;

    // ---------- 이벤트 훅 ----------

    /// <summary>영웅을 내려놓는 순간 (Hero.Release) — 지속시간 3초로 갱신, 누적 없음 (§10)</summary>
    public void OnRelease()
    {
        if (!BattleController.CombatActive) return;
        if (dropFury > 0) dropFuryLeft = DropDuration;
        if (dropGuard > 0) dropGuardLeft = DropDuration;
    }

    /// <summary>액티브 발동 시 (SkillRunner) — ACTIVE_STRIKE 재충전 (§11: 횟수 저장 없음)</summary>
    public void OnActiveCast()
    {
        if (activeStrike > 0) activeStrikeCharged = true;
    }

    /// <summary>적 처치 크레딧 (Hero/SkillRunner가 전달) — BLOODTHIRST 회복 (§9: 개수 합산)</summary>
    public void NotifyKill()
    {
        if (bloodthirst == 0 || hero.IsDead) return;
        hero.Heal(hero.MaxHP * BloodthirstHealPct / 100f * bloodthirst);
    }
}
