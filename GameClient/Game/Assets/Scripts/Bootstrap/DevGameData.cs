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
        db.heroes.Add(MakeHero("knight",  "기사",   new Color(0.35f, 0.55f, 1f),   0.95f, true,
            hp: 140f, atk: 12f, range: 1.1f, interval: 0.9f, speed: 1.8f));
        db.heroes.Add(MakeHero("archer",  "궁수",   new Color(1f, 0.8f, 0.25f),    0.80f, true,
            hp: 90f,  atk: 16f, range: 3.5f, interval: 1.2f, speed: 2.0f,
            projectile: true));
        db.heroes.Add(MakeHero("healer",  "사제",   new Color(0.45f, 1f, 0.55f),   0.80f, true,
            hp: 80f,  atk: 6f,  range: 2.5f, interval: 1.1f, speed: 1.7f,
            healer: true, healPower: 14f, healRange: 3.2f));
        db.heroes.Add(MakeHero("rogue",   "도적",   new Color(0.6f, 0.6f, 0.7f),   0.75f, false,
            hp: 75f,  atk: 20f, range: 1.0f, interval: 0.6f, speed: 2.4f));
        db.heroes.Add(MakeHero("mage",    "마법사", new Color(0.75f, 0.45f, 1f),   0.80f, false,
            hp: 70f,  atk: 26f, range: 4.2f, interval: 1.8f, speed: 1.6f,
            projectile: true));
        db.heroes.Add(MakeHero("paladin", "성기사", new Color(1f, 0.95f, 0.7f),    1.00f, false,
            hp: 180f, atk: 10f, range: 1.2f, interval: 1.1f, speed: 1.5f));
        return db;
    }

    public static EquipmentDatabase CreateEquipmentDatabase()
    {
        var db = ScriptableObject.CreateInstance<EquipmentDatabase>();
        db.items.Add(MakeEquip("sword",  "낡은 검",      Mod(StatType.Attack, flat: 5f)));
        db.items.Add(MakeEquip("armor",  "사슬 갑옷",    Mod(StatType.MaxHP, flat: 40f)));
        db.items.Add(MakeEquip("boots",  "바람의 신발",  Mod(StatType.MoveSpeed, pct: 20f)));
        db.items.Add(MakeEquip("lens",   "저격 렌즈",    Mod(StatType.AttackRange, flat: 0.6f)));
        db.items.Add(MakeEquip("ring",   "축복의 반지",  Mod(StatType.HealPower, flat: 8f)));
        return db;
    }

    public static RunConfig CreateRunConfig()
    {
        return ScriptableObject.CreateInstance<RunConfig>(); // 필드 기본값 사용
    }

    static HeroDefinition MakeHero(string id, string name, Color color, float size, bool unlockedByDefault,
        float hp, float atk, float range, float interval, float speed,
        bool healer = false, float healPower = 0f, float healRange = 0f,
        bool projectile = false)
    {
        var d = ScriptableObject.CreateInstance<HeroDefinition>();
        d.id = id;
        d.displayName = name;
        d.color = color;
        d.size = size;
        d.unlockedByDefault = unlockedByDefault;
        d.maxHP = hp;
        d.attack = atk;
        d.attackRange = range;
        d.attackInterval = interval;
        d.moveSpeed = speed;
        d.isHealer = healer;
        d.healPower = healPower;
        d.healRange = healRange;
        d.usesProjectile = projectile;
        return d;
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
