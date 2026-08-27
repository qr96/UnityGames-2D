using System.Collections;
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
                    foreach (Unit u in UnitRegistry.GetAll(Team.Enemy).ToArray()) // 스냅샷: 처치로 목록이 변해도 안전
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

            case SkillKind.EarthSmash:
                {
                    // 자기 중심 radius / damagePercent% 피해 / effectValue 밀침 / duration초 기절
                    float dmg = hero.AttackPower * skill.damagePercent / 100f;
                    foreach (Unit u in UnitRegistry.GetAll(Team.Enemy).ToArray())
                    {
                        if (DistTo(u) > skill.radius) continue;
                        u.TakeDamage(dmg);
                        if (u.IsDead) continue;
                        u.GetStatus().Add(StatusEffects.Kind.Stun, 1f, skill.duration);
                        Vector3 away = (u.transform.position - hero.transform.position).normalized;
                        if (away.sqrMagnitude < 0.001f) away = Vector3.up;
                        StartCoroutine(Knockback(u, away * skill.effectValue));
                    }
                    SpawnFlash(hero.transform.position, skill.radius * 2f,
                        new Color(0.75f, 0.55f, 0.3f, 0.45f), 0.3f);
                    break;
                }

            case SkillKind.Berserk:
                {
                    // 자신 / duration초 / 공속 +effectValue%, 공격력 +effectValue2%, 흡혈 effectValue3%
                    var st = hero.GetStatus();
                    st.Add(StatusEffects.Kind.AttackSpeed, 1f + skill.effectValue / 100f, skill.duration);
                    st.Add(StatusEffects.Kind.Damage, 1f + skill.effectValue2 / 100f, skill.duration);
                    st.Add(StatusEffects.Kind.Lifesteal, skill.effectValue3 / 100f, skill.duration);
                    SpawnFlash(hero.transform.position, hero.radius * 2f + 0.6f,
                        new Color(1f, 0.3f, 0.25f, 0.45f), skill.duration);
                    break;
                }

            case SkillKind.AbsoluteZero:
                {
                    // 자기 중심 radius / damagePercent% 피해 / duration초 빙결
                    float dmg = hero.AttackPower * skill.damagePercent / 100f;
                    foreach (Unit u in UnitRegistry.GetAll(Team.Enemy).ToArray())
                    {
                        if (DistTo(u) > skill.radius) continue;
                        u.TakeDamage(dmg);
                        if (!u.IsDead)
                            u.GetStatus().Add(StatusEffects.Kind.Stun, 1f, skill.duration); // 빙결 = 행동 불가
                    }
                    SpawnFlash(hero.transform.position, skill.radius * 2f,
                        new Color(0.5f, 0.85f, 1f, 0.45f), 0.4f);
                    break;
                }

            case SkillKind.DoomMark:
                {
                    // 현재 대상 / duration초간 받는 피해 +effectValue%
                    Unit target = hero.CurrentTarget;
                    if (target != null && !target.IsDead)
                    {
                        target.GetStatus().Add(StatusEffects.Kind.DamageTaken, 1f + skill.effectValue / 100f, skill.duration);
                        SpawnFlash(target.transform.position, target.radius * 2f + 0.5f,
                            new Color(0.85f, 0.3f, 0.9f, 0.5f), 0.5f);
                    }
                    break;
                }

            case SkillKind.BladeStorm:
                StartCoroutine(BladeStormRoutine());
                break;

            case SkillKind.Snipe:
                {
                    Unit target = hero.CurrentTarget;
                    if (target != null && !target.IsDead)
                        StartCoroutine(SnipeRoutine(target));
                    break;
                }

            case SkillKind.RapidFire:
                {
                    Unit target = hero.CurrentTarget;
                    if (target != null && !target.IsDead)
                        StartCoroutine(RapidFireRoutine(target));
                    break;
                }

            case SkillKind.Meteor:
                {
                    // 대상 위치 / radius 광역 damagePercent% + duration초 화염지대 (초당 effectValue% — 수치 초안)
                    Unit target = hero.CurrentTarget;
                    Vector3 pos = target != null ? target.transform.position : hero.transform.position;
                    float dmg = hero.AttackPower * skill.damagePercent / 100f;

                    foreach (Unit u in UnitRegistry.GetAll(Team.Enemy).ToArray())
                        if (Vector2.Distance(u.transform.position, pos) <= skill.radius)
                            u.TakeDamage(dmg);
                    SpawnFlash(pos, skill.radius * 2f, new Color(1f, 0.5f, 0.2f, 0.55f), 0.35f);

                    float zoneDps = hero.AttackPower * skill.effectValue / 100f;
                    ZoneEffect.Spawn(pos, skill.radius, skill.duration, 0.5f,
                        Team.Enemy, u => u.TakeDamage(zoneDps * 0.5f), // 0.5초 틱 = 초당 effectValue%
                        new Color(1f, 0.45f, 0.15f, 0.20f));
                    break;
                }

            case SkillKind.ChainLightning:
                {
                    // 현재 대상에서 시작, radius(연쇄 거리) 이내 미타격 적으로 점프, 최대 effectValue명
                    Unit current = hero.CurrentTarget;
                    if (current == null || current.IsDead) break;

                    float dmg = hero.AttackPower * skill.damagePercent / 100f;
                    int maxTargets = Mathf.Max(1, Mathf.RoundToInt(skill.effectValue));
                    var visited = new System.Collections.Generic.HashSet<Unit>();
                    Vector3 prevPos = hero.transform.position;

                    for (int i = 0; i < maxTargets && current != null; i++)
                    {
                        visited.Add(current);
                        SpawnLineFlash(prevPos, current.transform.position, new Color(0.5f, 0.8f, 1f, 0.7f));
                        prevPos = current.transform.position;
                        current.TakeDamage(dmg);

                        // 다음 연쇄 대상: 마지막 위치에서 radius 이내 최근접 미타격 적
                        Unit next = null;
                        float best = skill.radius;
                        foreach (Unit u in UnitRegistry.GetAll(Team.Enemy).ToArray())
                        {
                            if (visited.Contains(u) || u.IsDead) continue;
                            float d = Vector2.Distance(u.transform.position, prevPos);
                            if (d <= best) { best = d; next = u; }
                        }
                        current = next;
                    }
                    break;
                }

            case SkillKind.PoisonCloud:
                {
                    // 대상 위치 / radius / duration초 / 초당 damagePercent%
                    Unit target = hero.CurrentTarget;
                    Vector3 pos = target != null ? target.transform.position : hero.transform.position;
                    float tickDmg = hero.AttackPower * skill.damagePercent / 100f * skill.tickInterval;
                    ZoneEffect.Spawn(pos, skill.radius, skill.duration, skill.tickInterval,
                        Team.Enemy, u => u.TakeDamage(tickDmg),
                        new Color(0.45f, 0.8f, 0.25f, 0.22f));
                    break;
                }

            case SkillKind.Encore:
                {
                    // 범위 아군: 액티브 쿨타임 -effectValue초 (자신 제외) + duration초 공속 +effectValue2%
                    foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
                    {
                        if (DistTo(u) > skill.radius) continue;
                        u.GetStatus().Add(StatusEffects.Kind.AttackSpeed, 1f + skill.effectValue2 / 100f, skill.duration);

                        var runner = u.GetComponent<SkillRunner>();
                        if (runner != null && runner != this)
                            runner.ReduceCooldown(skill.effectValue);
                    }
                    SpawnFlash(hero.transform.position, skill.radius * 2f,
                        new Color(1f, 0.75f, 0.9f, 0.3f), 0.4f);
                    break;
                }

            case SkillKind.TimeWarp:
                {
                    // 대상 위치 / radius / duration초 존 — 틱마다 이속 -effectValue%, 공속 -effectValue2% (짧게 재부여)
                    Unit target = hero.CurrentTarget;
                    Vector3 pos = target != null ? target.transform.position : hero.transform.position;
                    float moveMult = 1f - skill.effectValue / 100f;
                    float atkMult = 1f - skill.effectValue2 / 100f;
                    float reapply = 0.35f; // 존을 벗어나면 곧 풀리도록 짧게 반복 부여
                    ZoneEffect.Spawn(pos, skill.radius, skill.duration, 0.25f,
                        Team.Enemy, u =>
                        {
                            var st = u.GetStatus();
                            st.Add(StatusEffects.Kind.MoveSpeed, moveMult, reapply);
                            st.Add(StatusEffects.Kind.AttackSpeed, atkMult, reapply);
                        },
                        new Color(0.6f, 0.5f, 1f, 0.20f));
                    break;
                }
        }
    }

    // ---------- 배치 3: 채널링/연쇄/존 ----------

    /// <summary>칼날폭풍: duration 동안 tick마다 자기 주변 radius 안 적에게 피해 (이동하며 유지)</summary>
    IEnumerator BladeStormRoutine()
    {
        float perHit = skill.damagePercent / 100f;
        float elapsed = 0f;
        float tickTimer = 0f;
        while (elapsed < skill.duration)
        {
            if (hero == null || hero.IsDead || hero.IsGrabbed || !BattleController.CombatActive) yield break;
            elapsed += Time.deltaTime;
            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0f)
            {
                tickTimer += skill.tickInterval;
                float dmg = hero.AttackPower * perHit;
                foreach (Unit u in UnitRegistry.GetAll(Team.Enemy).ToArray())
                    if (DistTo(u) <= skill.radius)
                        u.TakeDamage(dmg);
                SpawnFlash(hero.transform.position, skill.radius * 2f,
                    new Color(0.8f, 0.85f, 0.95f, 0.25f), 0.15f);
            }
            yield return null;
        }
    }

    /// <summary>저격: duration초 조준 채널링 후 500% — 조준 중 잡히거나 기절/대상 사망 시 취소 (쿨타임은 소모)</summary>
    IEnumerator SnipeRoutine(Unit target)
    {
        hero.SetChanneling(true);
        GameObject line = SpawnLine(hero.transform.position, target.transform.position,
            new Color(1f, 0.4f, 0.4f, 0.5f), thickness: 0.08f);

        float elapsed = 0f;
        bool ok = true;
        while (elapsed < skill.duration)
        {
            if (hero == null || hero.IsDead || hero.IsGrabbed ||
                (hero.Status != null && hero.Status.IsStunned) ||
                target == null || target.IsDead || !BattleController.CombatActive)
            {
                ok = false;
                break;
            }
            elapsed += Time.deltaTime;
            UpdateLine(line, hero.transform.position, target.transform.position);
            yield return null;
        }

        if (line != null) Destroy(line);
        if (hero != null) hero.SetChanneling(false);

        if (ok && target != null && !target.IsDead)
        {
            target.TakeDamage(hero.AttackPower * skill.damagePercent / 100f);
            SpawnFlash(target.transform.position, target.radius * 2f + 0.6f,
                new Color(1f, 0.45f, 0.35f, 0.6f), 0.25f);
        }
    }

    /// <summary>속사: duration초 채널링 연사 — 처치 시 사거리 내 최근접 적으로 전환</summary>
    IEnumerator RapidFireRoutine(Unit target)
    {
        hero.SetChanneling(true);
        float perShot = skill.damagePercent / 100f;
        float elapsed = 0f;
        float shotTimer = 0f;

        while (elapsed < skill.duration)
        {
            if (hero == null || hero.IsDead || hero.IsGrabbed ||
                (hero.Status != null && hero.Status.IsStunned) || !BattleController.CombatActive)
                break;

            elapsed += Time.deltaTime;
            shotTimer -= Time.deltaTime;

            if (shotTimer <= 0f)
            {
                // 대상 유지, 죽었으면 사거리 내 최근접 적으로 전환 (확정 규칙)
                if (target == null || target.IsDead)
                {
                    target = UnitRegistry.GetNearest(Team.Enemy, hero.transform.position);
                    if (target == null || DistTo(target) > skill.range) break; // 사거리 내 적 없음 → 종료
                }
                shotTimer += skill.tickInterval;
                target.TakeDamage(hero.AttackPower * perShot);
            }
            yield return null;
        }

        if (hero != null) hero.SetChanneling(false);
    }

    /// <summary>밀침 — 짧은 시간에 걸쳐 밀려남 (겹침 방지와 자연스럽게 어울림)</summary>
    IEnumerator Knockback(Unit unit, Vector3 offset, float time = 0.15f)
    {
        float elapsed = 0f;
        while (elapsed < time && unit != null && !unit.IsDead)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            unit.transform.position += offset * (dt / time);
            yield return null;
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

    GameObject SpawnLine(Vector3 a, Vector3 b, Color color, float thickness)
    {
        var go = new GameObject("SkillLine");
        var sr = UnitFactory.MakeVisual(go.transform, UnitFactory.Square, color, 1f, sortingOrder: 6);
        sr.transform.localScale = new Vector3(1f, thickness, 1f);
        UpdateLine(go, a, b);
        return go;
    }

    void UpdateLine(GameObject line, Vector3 a, Vector3 b)
    {
        if (line == null) return;
        line.transform.position = (a + b) * 0.5f;
        line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        var visual = line.transform.GetChild(0);
        visual.localScale = new Vector3(Vector2.Distance(a, b), visual.localScale.y, 1f);
    }

    void SpawnLineFlash(Vector3 a, Vector3 b, Color color)
    {
        Destroy(SpawnLine(a, b, color, 0.12f), 0.18f);
    }

    void SpawnFlash(Vector3 pos, float diameter, Color color, float life)
    {
        var go = new GameObject("SkillFlash");
        go.transform.position = pos;
        UnitFactory.MakeVisual(go.transform, UnitFactory.Circle, color, diameter, sortingOrder: 3);
        Destroy(go, life);
    }
}