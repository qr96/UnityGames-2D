using UnityEngine;

/// <summary>
/// 영웅의 액티브 스킬 구동기 (자동 발동 — 확정 규칙):
/// 쿨타임이 돌면 '발동 가능' 상태가 되고, 형태별 조건이 충족되는 순간 즉시 사용.
///  - 공격형: 현재 공격 대상이 스킬 사거리 안
///  - 자기중심 공격형: 공격 대상이 스킬 범위 안
///  - 회복형: 범위 내 HP가 감소한 아군 1명 이상
///  - 버프형: 범위 내 자신 이외 아군 1명 이상
///  - 소환형/자기강화형: 적을 공격 중이면 즉시
/// 실행 로직은 SkillKind로 분기 — 영웅이 늘 때마다 Execute에 케이스 추가.
/// </summary>
public class SkillRunner : MonoBehaviour
{
    Hero hero;
    SkillDefinition skill;
    float timer; // 0에서 시작 → 개전 직후부터 발동 가능

    public void Init(Hero hero, SkillDefinition skill)
    {
        this.hero = hero;
        this.skill = skill;
        timer = 0f;
    }

    /// <summary>쿨타임 감소 (앙코르 등 지원 스킬용)</summary>
    public void ReduceCooldown(float seconds)
    {
        timer = Mathf.Max(0f, timer - seconds);
    }

    void Update()
    {
        if (hero == null || skill == null || hero.IsDead) return;
        if (!BattleController.CombatActive) return;
        if (hero.IsGrabbed) return; // 공중에서는 스킬 사용 안 함
        if (hero.Status != null && hero.Status.IsStunned) return;

        if (timer > 0f)
        {
            timer -= Time.deltaTime;
            return;
        }

        if (!TriggerMet()) return;

        Execute();
        timer = skill.cooldown;
    }

    // ---------- 발동 조건 ----------

    bool TriggerMet()
    {
        Unit target = hero.CurrentTarget;

        switch (skill.trigger)
        {
            case SkillTrigger.TargetedAttack:
                return target != null && DistTo(target) <= skill.range;

            case SkillTrigger.SelfCenteredAttack:
                return target != null && DistTo(target) <= skill.radius;

            case SkillTrigger.HealAlly:
                foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
                    if (u.HPRatio < 0.999f && DistTo(u) <= skill.radius)
                        return true;
                return false;

            case SkillTrigger.BuffAlly:
                foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
                    if (u != (Unit)hero && DistTo(u) <= skill.radius)
                        return true;
                return false;

            case SkillTrigger.WhileEngaged:
                return hero.IsEngaged;

            default:
                return false;
        }
    }

    float DistTo(Unit u) => Vector2.Distance(hero.transform.position, u.transform.position);

    // ---------- 실행 ----------

    void Execute()
    {
        AnnounceSkillName();

        switch (skill.kind)
        {
            case SkillKind.IronWall:
            {
                // 자신 / duration초간 받는 피해 -effectValue%
                hero.GetStatus().Add(StatusEffects.Kind.DamageTaken, 1f - skill.effectValue / 100f, skill.duration);
                SpawnFlash(hero.transform.position, hero.radius * 2f + 0.6f,
                    new Color(0.6f, 0.75f, 1f, 0.45f), skill.duration);
                break;
            }

            case SkillKind.SpinSlash:
            {
                // 자신 중심 radius / 주변 적 damagePercent% 피해
                float dmg = hero.AttackPower * skill.damagePercent / 100f;
                foreach (Unit u in UnitRegistry.GetAll(Team.Enemy))
                    if (DistTo(u) <= skill.radius)
                        u.TakeDamage(dmg);
                SpawnFlash(hero.transform.position, skill.radius * 2f,
                    new Color(1f, 0.7f, 0.35f, 0.4f), 0.25f);
                break;
            }

            case SkillKind.PierceShot:
            {
                // 직선 range × radius(폭) / damagePercent% / 관통
                Unit target = hero.CurrentTarget;
                Vector3 dir = target != null
                    ? (target.transform.position - hero.transform.position).normalized
                    : Vector3.right;
                UnitFactory.SpawnPierceShot(hero.transform.position, dir,
                    hero.AttackPower * skill.damagePercent / 100f, skill.range, skill.radius);
                break;
            }

            case SkillKind.Sanctuary:
            {
                // 자신 중심 radius / duration초 / 틱마다 최대 HP effectValue% 회복
                float healPercent = skill.effectValue / 100f;
                ZoneEffect.Spawn(hero.transform.position, skill.radius, skill.duration, skill.tickInterval,
                    Team.Hero, u => u.Heal(u.MaxHP * healPercent),
                    new Color(0.55f, 1f, 0.65f, 0.22f));
                break;
            }
        }
    }

    /// <summary>스킬 발동 표시 — 머리 위에 스킬명 잠깐 (자리표시자 연출)</summary>
    void AnnounceSkillName()
    {
        var go = new GameObject("SkillName");
        go.transform.position = hero.transform.position + new Vector3(0f, 1.1f, 0f);
        var tm = UnitFactory.MakeWorldLabel(go.transform, skill.displayName, Vector3.zero, 0.06f, 20);
        if (tm != null) tm.color = new Color(1f, 0.9f, 0.4f);
        Destroy(go, 0.8f);
    }

    void SpawnFlash(Vector3 pos, float diameter, Color color, float life)
    {
        var go = new GameObject("SkillFlash");
        go.transform.position = pos;
        UnitFactory.MakeVisual(go.transform, UnitFactory.Circle, color, diameter, sortingOrder: 3);
        Destroy(go, life);
    }
}
