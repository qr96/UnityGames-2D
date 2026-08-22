using UnityEngine;

/// <summary>
/// 용사 전투 FSM. 스탯은 HeroRunInstance(정의 + 장비)에서 받아 전투 시작 시 캐시.
/// 전투 중 장비 변경이 불가하므로(GDD 8) 전투 내내 스탯이 고정되는 것이 규칙과 일치.
///
/// GDD 2·3:
///  - 가장 가까운 적 타겟 → 사거리까지 접근 → 정지 후 자동 공격
///  - 힐러: 부상자가 있으면 공격보다 힐 우선, HP 비율 최저 아군 우선
///  - Grabbed: 이동/공격 중지, 손가락 추적, 버둥거림, 피격은 가능, 놓으면 즉시 복귀
/// </summary>
public class Hero : Unit
{
    public enum State { Idle, Move, Act, Grabbed }

    public HeroRunInstance Runtime { get; private set; }
    public State CurrentState { get; private set; } = State.Idle;
    public bool IsGrabbed => CurrentState == State.Grabbed;

    // 전투 시작 시 캐시되는 최종 스탯 (장비 반영)
    float attackPower;
    float attackRange;
    float attackInterval;
    float moveSpeed;
    bool isHealer;
    float healPower;
    float healRange;
    bool usesProjectile;
    float projectileSpeed;
    Color projectileColor;
    float wiggleSpeed = 20f;
    float wiggleAngle = 12f;
    float landingDelay = 0.35f;

    float actTimer;
    float landingTimer; // 착지 후 AI 재개까지 남은 시간
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

        SetMaxHP(instance.GetStat(StatType.MaxHP));

        // GDD 8: 기본 공격/스킬이 참조할 공통 스탯은 정의에서 개별 지정
        attackPower = instance.GetStat(def.basicAttackPowerStat);
        healPower = instance.GetStat(def.healPowerStat);

        attackRange = instance.GetStat(StatType.AttackRange);
        attackInterval = Mathf.Max(0.05f, instance.GetStat(StatType.AttackInterval));
        moveSpeed = instance.GetStat(StatType.MoveSpeed);
        healRange = instance.GetStat(StatType.HealRange);

        isHealer = def.isHealer;
        usesProjectile = def.usesProjectile;
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

        // 배치 단계 등 교전 전에는 AI 정지 (잡기/배치만 가능)
        if (!BattleController.CombatActive)
        {
            CurrentState = State.Idle;
            return;
        }

        actTimer -= Time.deltaTime;

        // 힐러: 부상자가 있으면 힐 우선 (GDD 2)
        if (isHealer)
        {
            Unit ally = FindHealTarget();
            if (ally != null)
            {
                Pursue(ally, healRange, () => ally.Heal(healPower));
                return;
            }
        }

        // 기본: 가장 가까운 적을 공격
        Unit enemy = UnitRegistry.GetNearest(Team.Enemy, transform.position);
        if (enemy == null)
        {
            CurrentState = State.Idle;
            return;
        }
        Pursue(enemy, attackRange, () => Attack(enemy));
    }

    void Attack(Unit enemy)
    {
        if (usesProjectile)
            UnitFactory.SpawnProjectile(transform.position, enemy, attackPower, projectileSpeed, projectileColor);
        else
            enemy.TakeDamage(attackPower);
    }

    /// <summary>사거리 밖이면 접근, 안이면 주기마다 행동(공격/힐)</summary>
    void Pursue(Unit target, float range, System.Action act)
    {
        float dist = Vector2.Distance(transform.position, target.transform.position);
        if (dist > range)
        {
            CurrentState = State.Move;
            transform.position = Vector3.MoveTowards(
                transform.position, target.transform.position,
                moveSpeed * Time.deltaTime);
        }
        else
        {
            CurrentState = State.Act;
            if (actTimer <= 0f)
            {
                act();
                actTimer = attackInterval;
            }
        }
    }

    /// <summary>HP 비율이 가장 낮은 부상 아군 (본인 포함). 없으면 null.</summary>
    Unit FindHealTarget()
    {
        Unit best = null;
        float bestRatio = 0.999f;
        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
        {
            if (u.HPRatio < bestRatio)
            {
                bestRatio = u.HPRatio;
                best = u;
            }
        }
        return best;
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
        landingTimer = landingDelay; // 착지 후 잠시 정비하고 AI 재개
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