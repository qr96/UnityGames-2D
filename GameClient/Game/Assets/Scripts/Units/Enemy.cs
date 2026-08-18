using UnityEngine;

/// <summary>
/// 일반 적 AI.
/// GDD: 가장 가까운 살아있는 용사 탐색 → 접근 → 사거리 진입 → 자동 공격.
/// 잡혀 있는 용사도 타겟/피격 대상에 포함됨.
/// </summary>
public class Enemy : Unit
{
    public EnemyData data;

    float attackTimer;

    protected override void Awake()
    {
        base.Awake();
        team = Team.Enemy;
    }

    protected virtual void Start()
    {
        if (MaxHP <= 0f && data != null) SetMaxHP(data.maxHP);
    }

    protected virtual void Update()
    {
        if (IsDead || data == null) return;
        if (!BattleController.CombatActive) return; // 교전 중에만 행동

        attackTimer -= Time.deltaTime;

        Unit target = UnitRegistry.GetNearest(Team.Hero, transform.position);
        if (target == null) return;

        float dist = Vector2.Distance(transform.position, target.transform.position);
        if (dist > data.attackRange)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, target.transform.position,
                data.moveSpeed * Time.deltaTime);
        }
        else if (attackTimer <= 0f)
        {
            target.TakeDamage(data.attackDamage);
            attackTimer = data.attackInterval;
        }
    }
}
