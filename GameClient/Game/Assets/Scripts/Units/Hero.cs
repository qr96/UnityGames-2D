using UnityEngine;

/// <summary>
/// 용사 전투 FSM. 스탯은 HeroRunInstance(정의 + 장비)에서 받아 전투 시작 시 캐시.
/// 전투 중 장비 변경이 불가하므로(GDD 8) 전투 내내 기본 스탯이 고정 —
/// 전투 중 변동(버프/디버프)은 StatusEffects 계수로 처리.
///
///  - 가장 가까운 적 타겟 → 사거리까지 접근 → 정지 후 자동 공격 (공격력 × 기본공격 %)
///  - 액티브 스킬은 SkillRunner가 자동 발동 (확정 규칙)
///  - Grabbed: 이동/공격 중지, 손가락 추적, 버둥거림, 피격은 가능, 놓으면 착지 딜레이 후 복귀
/// </summary>
public class Hero : Unit
{
    public enum State { Idle, Move, Act, Grabbed }

    public HeroRunInstance Runtime { get; private set; }
    public State CurrentState { get; private set; } = State.Idle;
    public bool IsGrabbed => CurrentState == State.Grabbed;

    /// <summary>현재 공격 대상 (스킬 발동 조건 판정용)</summary>
    public Unit CurrentTarget { get; private set; }

    /// <summary>적을 공격 중인가 (대상이 있고 기본 공격 사거리 안)</summary>
    public bool IsEngaged { get; private set; }

    /// <summary>스킬 피해 계산용 — 버프 계수가 반영된 현재 공격력</summary>
    public float AttackPower =>
        attackPower * (Status != null ? Status.Multiplier(StatusEffects.Kind.Damage) : 1f);

    // 전투 시작 시 캐시되는 최종 스탯 (장비 반영)
    float attackPower;
    float basicAttackPercent = 100f;
    float attackRange;
    float attackInterval;
    float moveSpeed;
    AttackType attackType;
    float projectileSpeed;
    Color projectileColor;
    float wiggleSpeed = 20f;
    float wiggleAngle = 12f;
    float landingDelay = 0.35f;

    float actTimer;
    float landingTimer;
    Vector3 grabWorldPos;
    Transform visual;
    float wigglePhase;

    protected override void Awake()
    {
        base.Awake();
        team = Team.Hero;
    }

    /// <summary>전투 스폰 직후 호출. 장비가 반영된 최종 스탯을 캐시.</summary>
    public void Init(HeroRunInstance instance)
    {
        Runtime = instance;
        HeroDefinition def = instance.definition;

        // HP 이월: 기록된 HP로 스폰 (음수 = 미기록 → 최대치). 장비로 최대치가 변해도 잃은 HP는 유지.
        float maxHP = instance.GetStat(StatType.MaxHP);
        float carried = instance.currentHP < 0f ? maxHP : instance.currentHP;
        SetVitals(maxHP, Mathf.Min(carried, maxHP));

        attackPower = instance.GetStat(def.basicAttackPowerStat);
        basicAttackPercent = def.basicAttackPercent;
        attackRange = instance.GetStat(StatType.AttackRange);
        attackInterval = Mathf.Max(0.05f, instance.GetStat(StatType.AttackInterval));
        moveSpeed = instance.GetStat(StatType.MoveSpeed);

        attackType = def.attackType;
        projectileSpeed = def.projectileSpeed;
        projectileColor = def.color;
        wiggleSpeed = def.wiggleSpeed;
        wiggleAngle = def.wiggleAngle;
        landingDelay = def.landingDelay;

        visual = sr != null ? sr.transform : transform;
    }

    void Update()
    {
        if (IsDead || Runtime == null) return;

        Runtime.currentHP = CurrentHP; // HP 이월: 런 상태에 상시 기록

        if (IsGrabbed)
        {
            UpdateGrabbed();
            return;
        }

        // 착지 딜레이: 내려놓인 직후 잠시 제자리 (피격은 가능)
        if (landingTimer > 0f)
        {
            landingTimer -= Time.deltaTime;
            CurrentState = State.Idle;
            return;
        }

        // 배치/탐험 등 교전 전에는 AI 정지
        if (!BattleController.CombatActive)
        {
            CurrentState = State.Idle;
            CurrentTarget = null;
            IsEngaged = false;
            return;
        }

        // 기절: 행동 불가 (피격은 가능)
        if (Status != null && Status.IsStunned)
        {
            CurrentState = State.Idle;
            IsEngaged = false;
            return;
        }

        actTimer -= Time.deltaTime;

        // 가장 가까운 적을 공격
        Unit enemy = UnitRegistry.GetNearest(Team.Enemy, transform.position);
        CurrentTarget = enemy;
        if (enemy == null)
        {
            CurrentState = State.Idle;
            IsEngaged = false;
            return;
        }

        float dist = Vector2.Distance(transform.position, enemy.transform.position);
        IsEngaged = dist <= attackRange;

        if (!IsEngaged)
        {
            CurrentState = State.Move;
            float speed = moveSpeed * (Status != null ? Status.Multiplier(StatusEffects.Kind.MoveSpeed) : 1f);
            transform.position = Vector3.MoveTowards(
                transform.position, enemy.transform.position, speed * Time.deltaTime);
        }
        else
        {
            CurrentState = State.Act;
            if (actTimer <= 0f)
            {
                Attack(enemy);
                float speedMult = Status != null ? Status.Multiplier(StatusEffects.Kind.AttackSpeed) : 1f;
                actTimer = attackInterval / Mathf.Max(0.1f, speedMult);
            }
        }
    }

    void Attack(Unit enemy)
    {
        float damage = AttackPower * basicAttackPercent / 100f;

        if (attackType == AttackType.Ranged)
        {
            UnitFactory.SpawnProjectile(transform.position, enemy, damage, projectileSpeed, projectileColor);
        }
        else
        {
            enemy.TakeDamage(damage);

            // 흡혈 (광폭화 등) — 근접 즉시 타격에만 적용 (투사체 흡혈은 추후)
            float lifesteal = Status != null ? Status.Sum(StatusEffects.Kind.Lifesteal) : 0f;
            if (lifesteal > 0f)
                Heal(damage * lifesteal);
        }
    }

    // ---------- 잡기 (GDD 3) ----------

    public void Grab()
    {
        if (IsDead) return;
        CurrentState = State.Grabbed;
        grabWorldPos = transform.position;
        wigglePhase = 0f;
    }

    public void UpdateGrabPosition(Vector3 world)
    {
        world.z = 0f;
        grabWorldPos = world;
    }

    public void Release()
    {
        if (!IsGrabbed) return;
        CurrentState = State.Idle;
        if (visual != null) visual.localRotation = Quaternion.identity;
        landingTimer = landingDelay;
        actTimer = Mathf.Max(actTimer, landingDelay);
    }

    void UpdateGrabbed()
    {
        transform.position = Vector3.Lerp(transform.position, grabWorldPos, 25f * Time.deltaTime);

        wigglePhase += Time.deltaTime * wiggleSpeed;
        if (visual != null)
            visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(wigglePhase) * wiggleAngle);
    }
}