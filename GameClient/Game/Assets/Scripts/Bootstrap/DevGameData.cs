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
        var earthSmash = MakeSkill("earthsmash", "대지강타", SkillKind.EarthSmash, SkillTrigger.SelfCenteredAttack,
            cooldown: 9f, radius: 3.0f, damagePercent: 180f, duration: 1f, effectValue: 2.5f); // 밀침 2.5 + 기절 1초
        var berserk = MakeSkill("berserk", "광폭화", SkillKind.Berserk, SkillTrigger.WhileEngaged,
            cooldown: 12f, duration: 5f, effectValue: 60f, effectValue2: 30f, effectValue3: 15f); // 공속/공격력/흡혈
        var absoluteZero = MakeSkill("absolutezero", "절대영도", SkillKind.AbsoluteZero, SkillTrigger.SelfCenteredAttack,
            cooldown: 13f, radius: 3.5f, damagePercent: 100f, duration: 2.5f); // 빙결 2.5초
        var doomMark = MakeSkill("doommark", "파멸의 표식", SkillKind.DoomMark, SkillTrigger.TargetedAttack,
            cooldown: 12f, range: 7f, duration: 5f, effectValue: 30f); // 받는 피해 +30%
        var timeWarp = MakeSkill("timewarp", "시간 왜곡", SkillKind.TimeWarp, SkillTrigger.TargetedAttack,
            cooldown: 16f, range: 7f, radius: 3.5f, duration: 5f, effectValue: 50f, effectValue2: 35f); // 이속-50/공속-35
        var bladeStorm = MakeSkill("bladestorm", "칼날폭풍", SkillKind.BladeStorm, SkillTrigger.SelfCenteredAttack,
            cooldown: 8f, radius: 2.0f, duration: 1.5f, tickInterval: 0.3f, damagePercent: 60f);
        var snipe = MakeSkill("snipe", "저격", SkillKind.Snipe, SkillTrigger.TargetedAttack,
            cooldown: 10f, range: 12f, duration: 1f, damagePercent: 500f); // 1초 조준
        var rapidFire = MakeSkill("rapidfire", "속사", SkillKind.RapidFire, SkillTrigger.TargetedAttack,
            cooldown: 9f, range: 6.5f, duration: 2f, tickInterval: 0.2f, damagePercent: 45f);
        var meteor = MakeSkill("meteor", "메테오", SkillKind.Meteor, SkillTrigger.TargetedAttack,
            cooldown: 11f, range: 7f, radius: 2.5f, damagePercent: 350f, duration: 3f, effectValue: 40f); // 화염지대 초당 40% (초안)
        var chainLightning = MakeSkill("chainlightning", "연쇄번개", SkillKind.ChainLightning, SkillTrigger.TargetedAttack,
            cooldown: 8f, range: 6f, radius: 3f, damagePercent: 180f, effectValue: 6f); // 연쇄 3.0 / 최대 6명
        var poisonCloud = MakeSkill("poisoncloud", "맹독 구름", SkillKind.PoisonCloud, SkillTrigger.TargetedAttack,
            cooldown: 10f, range: 6f, radius: 3f, duration: 6f, tickInterval: 1f, damagePercent: 45f); // 초당 45%
        var encore = MakeSkill("encore", "앙코르", SkillKind.Encore, SkillTrigger.BuffAlly,
            cooldown: 15f, radius: 4f, duration: 4f, effectValue: 3f, effectValue2: 20f); // 쿨 -3초 / 공속 +20%

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

        // ---- 배치 2: 상태이상/버프 조 ----
        db.heroes.Add(MakeHero("dora", "도라", HeroClass.Warrior, AttackType.Melee,
            new Color(0.75f, 0.55f, 0.30f), 1.00f, unlocked: true,
            hp: 200f, atk: 16f, range: 1.3f, interval: 1.6f, speed: 1.5f,
            basicPercent: 150f, skill: earthSmash));     // 망치 타격 150%

        db.heroes.Add(MakeHero("rex", "렉스", HeroClass.Warrior, AttackType.Melee,
            new Color(0.85f, 0.25f, 0.25f), 0.95f, unlocked: true,
            hp: 185f, atk: 15f, range: 1.4f, interval: 1.0f, speed: 1.8f,
            basicPercent: 110f, skill: berserk));        // 도끼 공격 110%

        db.heroes.Add(MakeHero("noah", "노아", HeroClass.Mage, AttackType.Ranged,
            new Color(0.55f, 0.80f, 1.00f), 0.80f, unlocked: true,
            hp: 85f, atk: 20f, range: 5.5f, interval: 1.2f, speed: 1.6f,
            basicPercent: 80f, skill: absoluteZero));    // 얼음탄 80%

        db.heroes.Add(MakeHero("nia", "니아", HeroClass.Mage, AttackType.Ranged,
            new Color(0.80f, 0.40f, 0.90f), 0.80f, unlocked: true,
            hp: 85f, atk: 17f, range: 6.0f, interval: 1.2f, speed: 1.6f,
            basicPercent: 90f, skill: doomMark));        // 룬탄 90%

        db.heroes.Add(MakeHero("zero", "제로", HeroClass.Mage, AttackType.Ranged,
            new Color(0.55f, 0.50f, 0.95f), 0.80f, unlocked: true,
            hp: 80f, atk: 16f, range: 6.0f, interval: 1.4f, speed: 1.6f,
            basicPercent: 80f, skill: timeWarp));        // 시간탄 80%

        // ---- 배치 3: 채널링/연쇄/존/독 조 (로스터 완성) ----
        db.heroes.Add(MakeHero("sian", "시안", HeroClass.Rogue, AttackType.Melee,
            new Color(0.60f, 0.60f, 0.72f), 0.75f, unlocked: true,
            hp: 120f, atk: 12f, range: 1.0f, interval: 0.55f, speed: 2.4f,
            basicPercent: 70f, skill: bladeStorm));      // 단검 공격 70%

        db.heroes.Add(MakeHero("bell", "벨", HeroClass.Ranger, AttackType.Ranged,
            new Color(0.35f, 0.70f, 0.55f), 0.80f, unlocked: true,
            hp: 85f, atk: 18f, range: 9.0f, interval: 1.8f, speed: 1.7f,
            basicPercent: 160f, skill: snipe));          // 석궁 160%

        db.heroes.Add(MakeHero("eugene", "유진", HeroClass.Ranger, AttackType.Ranged,
            new Color(0.90f, 0.65f, 0.35f), 0.80f, unlocked: true,
            hp: 95f, atk: 12f, range: 6.5f, interval: 0.7f, speed: 1.9f,
            basicPercent: 75f, skill: rapidFire));       // 권총 75%

        db.heroes.Add(MakeHero("pipi", "피피", HeroClass.Mage, AttackType.Ranged,
            new Color(1.00f, 0.45f, 0.30f), 0.80f, unlocked: true,
            hp: 85f, atk: 18f, range: 5.0f, interval: 1.1f, speed: 1.6f,
            basicPercent: 100f, skill: meteor));         // 화염탄 100%

        db.heroes.Add(MakeHero("zet", "제트", HeroClass.Mage, AttackType.Ranged,
            new Color(0.45f, 0.75f, 1.00f), 0.80f, unlocked: true,
            hp: 85f, atk: 16f, range: 5.0f, interval: 1.0f, speed: 1.7f,
            basicPercent: 95f, skill: chainLightning));  // 번개 95%

        var momo = MakeHero("momo", "모모", HeroClass.Mage, AttackType.Ranged,
            new Color(0.55f, 0.85f, 0.30f), 0.80f, unlocked: true,
            hp: 85f, atk: 15f, range: 4.5f, interval: 1.3f, speed: 1.6f,
            basicPercent: 60f, skill: poisonCloud);      // 독병 60%
        momo.basicPoisonTotalPercent = 60f;              // + 3초간 총 60% 독 (초당 20%, 비중첩 갱신)
        momo.basicPoisonDuration = 3f;
        db.heroes.Add(momo);

        db.heroes.Add(MakeHero("lulu", "루루", HeroClass.Support, AttackType.Ranged,
            new Color(1.00f, 0.70f, 0.85f), 0.80f, unlocked: true,
            hp: 100f, atk: 9f, range: 4.5f, interval: 1.0f, speed: 1.8f,
            basicPercent: 75f, skill: encore));          // 음파 75%

        return db;
    }

    static SkillDefinition MakeSkill(string id, string name, SkillKind kind, SkillTrigger trigger,
        float cooldown, float range = 0f, float radius = 0f, float duration = 0f,
        float tickInterval = 1f, float damagePercent = 0f, float effectValue = 0f,
        float effectValue2 = 0f, float effectValue3 = 0f)
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
        sk.effectValue2 = effectValue2;
        sk.effectValue3 = effectValue3;
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