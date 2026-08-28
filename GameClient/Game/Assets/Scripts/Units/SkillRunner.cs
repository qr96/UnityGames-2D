using UnityEngine;

/// <summary>
/// 영웅의 액티브 스킬 구동기 (액티브 스펙 v2 — 10종).
///
/// 발동 규칙 (확정):
///  - 자동형: 쿨타임이 돌면 종류별 조건 충족 시 즉시 발동
///      강타/저격/화염구: 현재 공격 대상이 스킬 사거리 안
///      휩쓸기: 현재 대상이 스킬 반경 안
///      처형: 대상이 사거리 안 + 대상 HP가 임계 % 이하
///      회복: 반경 내 HP 임계 % 이하 아군 존재
///  - 내려놓기형: 쿨 준비 + 무기 조건 충족 상태에서 내려놓는 순간 발동 (Hero.Release가 호출).
///      쿨 중이면 그냥 재배치. 쿨이 다 차도 내려놓기 전까지 대기.
///  - 무기 조건 불충족 시 액티브만 비활성 — 기본 공격은 정상.
///
/// 쿨타임은 전투 중 항상 감소 (잡힌 상태 포함 — 내려놓기 발동 흐름과 맞물림).
/// </summary>
public class SkillRunner : MonoBehaviour
{
    Hero hero;
    SkillDefinition skill;
    float timer; // 0에서 시작 → 개전 직후부터 발동 가능

    public bool IsReady => timer <= 0f;
    public SkillDefinition Skill => skill;

    public void Init(Hero hero, SkillDefinition skill)
    {
        this.hero = hero;
        this.skill = skill;
        timer = 0f;
    }

    void Update()
    {
        if (hero == null || skill == null || hero.IsDead) return;
        if (!BattleController.CombatActive) return;

        if (timer > 0f)
            timer -= Time.deltaTime; // 잡힌 상태에서도 쿨은 돎

        if (skill.activation != SkillActivation.Auto) return; // 내려놓기형은 대기
        if (timer > 0f) return;
        if (hero.IsGrabbed || hero.IsChanneling) return;
        if (hero.Status != null && hero.Status.IsStunned) return;
        if (!WeaponOk()) return; // 무기 조건 불충족 → 액티브만 비활성
        if (!AutoConditionMet()) return;

        Execute();
        timer = skill.cooldown;
    }

    /// <summary>내려놓는 순간 Hero.Release가 호출 — 발동했으면 true, 아니면 그냥 재배치.</summary>
    public bool TryTriggerOnRelease()
    {
        if (hero == null || skill == null || hero.IsDead) return false;
        if (skill.activation != SkillActivation.OnRelease) return false;
        if (!BattleController.CombatActive) return false;
        if (timer > 0f) return false;          // 쿨다운 중 → 재배치만
        if (!WeaponOk()) return false;
        if (hero.Status != null && hero.Status.IsStunned) return false;

        Execute();
        timer = skill.cooldown;
        return true;
    }

    bool WeaponOk() => WeaponRules.Meets(hero.Weapon, skill.weaponRequirement);

    // ---------- 자동 발동 조건 ----------

    bool AutoConditionMet()
    {
        Unit target = hero.CurrentTarget;

        switch (skill.kind)
        {
            case SkillKind.PowerStrike:
            case SkillKind.Snipe:
            case SkillKind.Fireball:
                return target != null && DistTo(target) <= skill.range;

            case SkillKind.Sweep:
                return target != null && DistTo(target) <= skill.radius;

            case SkillKind.Execute:
                return target != null && DistTo(target) <= skill.range
                    && target.HPRatio * 100f <= skill.effectValue;

            case SkillKind.Heal:
                foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
                    if (u.HPRatio * 100f <= skill.effectValue && DistTo(u) <= skill.radius)
                        return true;
                return false;

            default:
                return false; // 내려놓기형은 자동 발동 없음
        }
    }

    float DistTo(Unit u) => Vector2.Distance(hero.transform.position, u.transform.position);

    // ---------- 실행 ----------

    void Execute()
    {
        AnnounceSkillName();

        switch (skill.kind)
        {
            case SkillKind.PowerStrike:
                {
                    // 현재 대상 / damagePercent% 단일 강타
                    Unit target = hero.CurrentTarget;
                    if (target == null || target.IsDead) break;
                    target.TakeDamage(hero.AttackPower * skill.damagePercent / 100f);
                    SpawnFlash(target.transform.position, target.radius * 2f + 0.5f,
                        new Color(1f, 0.75f, 0.3f, 0.55f), 0.22f);
                    break;
                }

            case SkillKind.Sweep:
                {
                    // 현재 대상 방향 전방 부채꼴(±60°) radius / damagePercent%
                    Unit target = hero.CurrentTarget;
                    if (target == null) break;
                    Vector2 dir = (target.transform.position - hero.transform.position).normalized;
                    float dmg = hero.AttackPower * skill.damagePercent / 100f;
                    foreach (Unit u in UnitRegistry.GetAll(Team.Enemy).ToArray())
                    {
                        if (DistTo(u) > skill.radius) continue;
                        Vector2 to = (u.transform.position - hero.transform.position).normalized;
                        if (Vector2.Angle(dir, to) <= 60f)
                            u.TakeDamage(dmg);
                    }
                    SpawnFlash(hero.transform.position + (Vector3)(dir * skill.radius * 0.5f),
                        skill.radius, new Color(1f, 0.6f, 0.35f, 0.4f), 0.25f);
                    break;
                }

            case SkillKind.Snipe:
                {
                    // 현재 대상 / damagePercent% 강력한 단일 투사체
                    Unit target = hero.CurrentTarget;
                    if (target == null || target.IsDead) break;
                    UnitFactory.SpawnProjectile(hero.transform.position, target,
                        hero.AttackPower * skill.damagePercent / 100f,
                        speed: 14f, new Color(1f, 0.45f, 0.35f), onHit: null);
                    break;
                }

            case SkillKind.Fireball:
                {
                    // 현재 대상에게 투사체 → 명중 시 대상 + 주변 radius 적에게 damagePercent%
                    Unit target = hero.CurrentTarget;
                    if (target == null || target.IsDead) break;
                    float dmg = hero.AttackPower * skill.damagePercent / 100f;
                    float splash = skill.radius;
                    UnitFactory.SpawnProjectile(hero.transform.position, target,
                        dmg, speed: 10f, new Color(1f, 0.5f, 0.2f),
                        onHit: u =>
                        {
                            // 주변 적 동일 피해 (명중 대상 제외 — 대상은 투사체 피해로 이미 타격)
                            foreach (Unit e in UnitRegistry.GetAll(Team.Enemy).ToArray())
                                if (e != u && !e.IsDead &&
                                    Vector2.Distance(e.transform.position, u.transform.position) <= splash)
                                    e.TakeDamage(dmg);
                            SpawnFlash(u.transform.position, splash * 2f,
                                new Color(1f, 0.45f, 0.15f, 0.5f), 0.3f);
                        });
                    break;
                }

            case SkillKind.Execute:
                {
                    // HP 임계 이하의 현재 대상 / damagePercent% 처형타
                    Unit target = hero.CurrentTarget;
                    if (target == null || target.IsDead) break;
                    target.TakeDamage(hero.AttackPower * skill.damagePercent / 100f);
                    SpawnFlash(target.transform.position, target.radius * 2f + 0.7f,
                        new Color(0.85f, 0.15f, 0.2f, 0.6f), 0.28f);
                    break;
                }

            case SkillKind.Heal:
                {
                    // 반경 내 임계 이하 아군 중 HP 비율이 가장 낮은 1명 회복 (시전자 공격력 %)
                    Unit best = null;
                    float bestRatio = skill.effectValue / 100f + 0.0001f;
                    foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
                        if (DistTo(u) <= skill.radius && u.HPRatio < bestRatio)
                        {
                            bestRatio = u.HPRatio;
                            best = u;
                        }
                    if (best == null) break;
                    best.Heal(hero.AttackPower * skill.damagePercent / 100f);
                    SpawnFlash(best.transform.position, best.radius * 2f + 0.5f,
                        new Color(0.5f, 1f, 0.6f, 0.5f), 0.3f);
                    break;
                }

            case SkillKind.BattleCry:
                {
                    // 반경 내 아군(자신 포함) / duration초 / 공격력 +effectValue%, 공속 +effectValue2%
                    foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
                    {
                        if (DistTo(u) > skill.radius) continue;
                        var st = u.GetStatus();
                        st.Add(StatusEffects.Kind.Damage, 1f + skill.effectValue / 100f, skill.duration);
                        st.Add(StatusEffects.Kind.AttackSpeed, 1f + skill.effectValue2 / 100f, skill.duration);
                    }
                    SpawnFlash(hero.transform.position, skill.radius * 2f,
                        new Color(1f, 0.85f, 0.4f, 0.35f), 0.4f);
                    break;
                }

            case SkillKind.Shockwave:
                {
                    // 반경 내 적 / damagePercent% + effectValue 밀침
                    float dmg = hero.AttackPower * skill.damagePercent / 100f;
                    foreach (Unit u in UnitRegistry.GetAll(Team.Enemy).ToArray())
                    {
                        if (DistTo(u) > skill.radius) continue;
                        u.TakeDamage(dmg);
                        if (u.IsDead) continue;
                        Vector3 away = (u.transform.position - hero.transform.position).normalized;
                        if (away.sqrMagnitude < 0.001f) away = Vector3.up;
                        StartCoroutine(Knockback(u, away * skill.effectValue));
                    }
                    SpawnFlash(hero.transform.position, skill.radius * 2f,
                        new Color(0.8f, 0.8f, 0.95f, 0.45f), 0.3f);
                    break;
                }

            case SkillKind.Barrier:
                {
                    // 반경 내 아군(자신 포함) / duration초 / 시전자 공격력 effectValue% 만큼의 보호막
                    float amount = hero.AttackPower * skill.effectValue / 100f;
                    foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
                    {
                        if (DistTo(u) > skill.radius) continue;
                        u.GetStatus().AddShield(amount, skill.duration);
                        SpawnFlash(u.transform.position, u.radius * 2f + 0.5f,
                            new Color(0.55f, 0.75f, 1f, 0.45f), 0.35f);
                    }
                    break;
                }

            case SkillKind.FirstAid:
                {
                    // 반경 내 HP 비율이 가장 낮은 아군 1명 회복 (임계 없음)
                    Unit best = null;
                    float bestRatio = 0.999f; // 만피는 제외
                    foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
                        if (DistTo(u) <= skill.radius && u.HPRatio < bestRatio)
                        {
                            bestRatio = u.HPRatio;
                            best = u;
                        }
                    if (best == null) break;
                    best.Heal(hero.AttackPower * skill.damagePercent / 100f);
                    SpawnFlash(best.transform.position, best.radius * 2f + 0.6f,
                        new Color(0.45f, 1f, 0.7f, 0.55f), 0.35f);
                    break;
                }
        }
    }

    /// <summary>밀침 — 짧은 시간에 걸쳐 밀려남 (겹침 방지와 자연스럽게 어울림)</summary>
    System.Collections.IEnumerator Knockback(Unit unit, Vector3 offset, float time = 0.15f)
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

    void SpawnFlash(Vector3 pos, float diameter, Color color, float life)
    {
        var go = new GameObject("SkillFlash");
        go.transform.position = pos;
        UnitFactory.MakeVisual(go.transform, UnitFactory.Circle, color, diameter, sortingOrder: 3);
        Destroy(go, life);
    }
}