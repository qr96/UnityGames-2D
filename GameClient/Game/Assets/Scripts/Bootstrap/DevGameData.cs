using UnityEngine;

/// <summary>
/// 개발용 영웅/장비 데이터 생성 (에셋이 없을 때의 런타임 폴백 + 에셋 생성 툴의 원본).
/// 실제 밸런싱은 [Tools > GrabProto > 게임 데이터 에셋 생성]으로 만든 에셋에서 진행.
/// </summary>
public static class DevGameData
{
    public static HeroDatabase CreateHeroDatabase()
    {
        var db = ScriptableObject.CreateInstance<HeroDatabase>();

        // ---- 스킬 (기획 표 수치 그대로) ----
        var ironWall = MakeSkill("ironwall", "철벽", SkillKind.IronWall, SkillTrigger.WhileEngaged,
            cooldown: 10f, duration: 4f, effectValue: 60f); // 4초간 받는 피해 -60%
        var spinSlash = MakeSkill("spinslash", "회전참", SkillKind.SpinSlash, SkillTrigger.SelfCenteredAttack,
            cooldown: 7f, radius: 2.5f, damagePercent: 280f);
        var pierceShot = MakeSkill("pierceshot", "관통사격", SkillKind.PierceShot, SkillTrigger.TargetedAttack,
            cooldown: 6f, range: 10f, radius: 0.8f, damagePercent: 220f); // radius = 폭
        var sanctuary = MakeSkill("sanctuary", "성역", SkillKind.Sanctuary, SkillTrigger.HealAlly,
            cooldown: 12f, radius: 4f, duration: 4f, tickInterval: 1f, effectValue: 5f); // 매초 최대HP 5%

        // ---- 1차 영웅 4명 (스킬 축 검증: 버프/광역/관통/회복존) ----
        // 기본 스탯(HP/공격/이속)은 초안 — 에셋에서 튜닝. 영입 보류 중이라 전원 기본 해금.
        db.heroes.Add(MakeHero("bram", "브람", HeroClass.Warrior, AttackType.Melee,
            new Color(0.40f, 0.55f, 0.95f), 1.00f, unlocked: true,
            hp: 220f, atk: 14f, range: 1.2f, interval: 1.1f, speed: 1.6f,
            basicPercent: 100f, skill: ironWall));       // 방패 가격 100%

        db.heroes.Add(MakeHero("kyle", "카일", HeroClass.Warrior, AttackType.Melee,
            new Color(0.90f, 0.45f, 0.30f), 0.95f, unlocked: true,
            hp: 170f, atk: 18f, range: 1.5f, interval: 1.4f, speed: 1.7f,
            basicPercent: 130f, skill: spinSlash));      // 대검 베기 130%

        db.heroes.Add(MakeHero("luna", "루나", HeroClass.Ranger, AttackType.Ranged,
            new Color(1.00f, 0.85f, 0.30f), 0.80f, unlocked: true,
            hp: 90f, atk: 15f, range: 7.0f, interval: 0.8f, speed: 2.0f,
            basicPercent: 90f, skill: pierceShot));      // 화살 90%

        db.heroes.Add(MakeHero("lia", "리아", HeroClass.Support, AttackType.Ranged,
            new Color(0.55f, 0.95f, 0.65f), 0.80f, unlocked: true,
            hp: 110f, atk: 10f, range: 6.0f, interval: 1.2f, speed: 1.8f,
            basicPercent: 80f, skill: sanctuary));       // 성광탄 80%

        return db;
    }

    static SkillDefinition MakeSkill(string id, string name, SkillKind kind, SkillTrigger trigger,
        float cooldown, float range = 0f, float radius = 0f, float duration = 0f,
        float tickInterval = 1f, float damagePercent = 0f, float effectValue = 0f)
    {
        var sk = ScriptableObject.CreateInstance<SkillDefinition>();
        sk.id = id;
        sk.displayName = name;
        sk.kind = kind;
        sk.trigger = trigger;
        sk.cooldown = cooldown;
        sk.range = range;
        sk.radius = radius;
        sk.duration = duration;
        sk.tickInterval = tickInterval;
        sk.damagePercent = damagePercent;
        sk.effectValue = effectValue;
        return sk;
    }

    static HeroDefinition MakeHero(string id, string name, HeroClass heroClass, AttackType attackType,
        Color color, float size, bool unlocked,
        float hp, float atk, float range, float interval, float speed,
        float basicPercent, SkillDefinition skill)
    {
        var d = ScriptableObject.CreateInstance<HeroDefinition>();
        d.id = id;
        d.displayName = name;
        d.heroClass = heroClass;
        d.attackType = attackType;
        d.color = color;
        d.size = size;
        d.unlockedByDefault = unlocked;
        d.maxHP = hp;
        d.attack = atk;
        d.attackRange = range;
        d.attackInterval = interval;
        d.moveSpeed = speed;
        d.basicAttackPercent = basicPercent;
        d.skill = skill;
        return d;
    }

    public static EquipmentDatabase CreateEquipmentDatabase()
    {
        var db = ScriptableObject.CreateInstance<EquipmentDatabase>();
        db.items.Add(MakeEquip("sword", "낡은 검", Mod(StatType.Attack, flat: 5f)));
        db.items.Add(MakeEquip("armor", "사슬 갑옷", Mod(StatType.MaxHP, flat: 40f)));
        db.items.Add(MakeEquip("boots", "바람의 신발", Mod(StatType.MoveSpeed, pct: 20f)));
        db.items.Add(MakeEquip("lens", "저격 렌즈", Mod(StatType.AttackRange, flat: 0.6f)));
        db.items.Add(MakeEquip("ring", "축복의 반지", Mod(StatType.HealPower, flat: 8f)));
        return db;
    }

    public static RunConfig CreateRunConfig()
    {
        return ScriptableObject.CreateInstance<RunConfig>(); // 필드 기본값 사용
    }

    static EquipmentDefinition MakeEquip(string id, string name, params StatModifier[] mods)
    {
        var e = ScriptableObject.CreateInstance<EquipmentDefinition>();
        e.id = id;
        e.displayName = name;
        e.modifiers = mods;
        return e;
    }

    static StatModifier Mod(StatType stat, float flat = 0f, float pct = 0f) =>
        new StatModifier { stat = stat, flat = flat, percent = pct };
}