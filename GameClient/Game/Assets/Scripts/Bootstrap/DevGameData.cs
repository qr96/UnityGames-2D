using UnityEngine;

/// <summary>
/// 개발용 영웅/스킬/장비 데이터 생성 (에셋이 없을 때의 런타임 폴백 + 에셋 생성 툴의 원본).
/// 실제 밸런싱은 [Tools > GrabProto > 게임 데이터 에셋 생성]으로 만든 에셋에서 진행.
///
/// 영웅 스펙 v2 반영:
///  - 클래스 없음, 영웅별 스킬 지정 없음 (액티브는 HeroRoster가 skillPool에서 랜덤 배정)
///  - HP/공격력은 OwnedHero 굴림값 사용 — 여기의 수치는 폴백일 뿐
///  - 사거리/공격주기는 무기가 결정 — 영웅 정의의 해당 값은 무기 미장착 폴백조차 아님 (0 처리)
///  - 스킬 수치는 전부 임시값 — 플레이테스트 후 밸런싱
/// </summary>
public static class DevGameData
{
    // ---------------- 스킬 (액티브 스펙 v2 — 10종, 임시 수치) ----------------

    public static System.Collections.Generic.List<SkillDefinition> CreateSkillPool()
    {
        var pool = new System.Collections.Generic.List<SkillDefinition>
        {
            // ---- 자동형 ----
            MakeSkill("powerstrike", "강타", SkillKind.PowerStrike, SkillActivation.Auto, WeaponRequirement.Melee,
                cooldown: 6f, range: 2.0f, damagePercent: 250f),
            MakeSkill("sweep", "휩쓸기", SkillKind.Sweep, SkillActivation.Auto, WeaponRequirement.Melee,
                cooldown: 8f, radius: 2.5f, damagePercent: 180f),
            MakeSkill("snipe", "저격", SkillKind.Snipe, SkillActivation.Auto, WeaponRequirement.Bow,
                cooldown: 10f, range: 8f, damagePercent: 400f),
            MakeSkill("fireball", "화염구", SkillKind.Fireball, SkillActivation.Auto, WeaponRequirement.MagicTool,
                cooldown: 9f, range: 6f, radius: 2.0f, damagePercent: 220f),
            MakeSkill("execute", "처형", SkillKind.Execute, SkillActivation.Auto, WeaponRequirement.Melee,
                cooldown: 12f, range: 2.0f, damagePercent: 500f, effectValue: 30f), // 대상 HP 30% 이하
            MakeSkill("heal", "회복", SkillKind.Heal, SkillActivation.Auto, WeaponRequirement.MagicTool,
                cooldown: 10f, radius: 5f, damagePercent: 300f, effectValue: 50f), // HP 50% 이하 아군, 공격력 300% 회복

            // ---- 내려놓기형 (무기 조건 없음) ----
            MakeSkill("battlecry", "전투 함성", SkillKind.BattleCry, SkillActivation.OnRelease, WeaponRequirement.None,
                cooldown: 15f, radius: 3.5f, duration: 5f, effectValue: 25f, effectValue2: 25f), // 공격력/공속 +25%
            MakeSkill("shockwave", "충격파", SkillKind.Shockwave, SkillActivation.OnRelease, WeaponRequirement.None,
                cooldown: 10f, radius: 3.0f, damagePercent: 150f, effectValue: 2.5f), // 밀침 2.5
            MakeSkill("barrier", "보호막", SkillKind.Barrier, SkillActivation.OnRelease, WeaponRequirement.None,
                cooldown: 14f, radius: 3.5f, duration: 6f, effectValue: 400f), // 공격력 400% 보호막
            MakeSkill("firstaid", "응급 치료", SkillKind.FirstAid, SkillActivation.OnRelease, WeaponRequirement.None,
                cooldown: 8f, radius: 4f, damagePercent: 400f), // 최저 HP 아군 공격력 400% 회복
        };
        return pool;
    }

    // ---------------- 영웅 ----------------

    public static HeroDatabase CreateHeroDatabase()
    {
        var db = ScriptableObject.CreateInstance<HeroDatabase>();
        db.skillPool = CreateSkillPool();

        // ---- 시작 영웅 전용 정의 (표 확정 — 스탯/액티브/특성/무기는 HeroRoster.EnsureStarters가 지정) ----
        db.starters.Add(MakeHero("bran", "브란", new Color(0.35f, 0.50f, 0.90f), 0.95f, speed: 1.7f));
        db.starters.Add(MakeHero("rina", "리나", new Color(0.95f, 0.75f, 0.35f), 0.80f, speed: 1.9f));
        db.starters.Add(MakeHero("owen", "오웬", new Color(0.60f, 0.85f, 0.70f), 0.85f, speed: 1.7f));

        // ---- 랜덤 후보 외형 템플릿 풀 ----
        // 색/이름만 유의미 — HP/공격은 OwnedHero 굴림, 사거리/주기는 무기, 액티브는 풀에서 배정.
        db.heroes.Add(MakeHero("bram", "브람", new Color(0.40f, 0.55f, 0.95f), 1.00f, speed: 1.6f));
        db.heroes.Add(MakeHero("kyle", "카일", new Color(0.90f, 0.45f, 0.30f), 0.95f, speed: 1.7f));
        db.heroes.Add(MakeHero("luna", "루나", new Color(1.00f, 0.85f, 0.30f), 0.80f, speed: 2.0f));
        db.heroes.Add(MakeHero("lia", "리아", new Color(0.55f, 0.95f, 0.65f), 0.80f, speed: 1.8f));
        db.heroes.Add(MakeHero("dora", "도라", new Color(0.75f, 0.55f, 0.30f), 1.00f, speed: 1.5f));
        db.heroes.Add(MakeHero("rex", "렉스", new Color(0.85f, 0.25f, 0.25f), 0.95f, speed: 1.8f));
        db.heroes.Add(MakeHero("noah", "노아", new Color(0.55f, 0.80f, 1.00f), 0.80f, speed: 1.6f));
        db.heroes.Add(MakeHero("nia", "니아", new Color(0.80f, 0.40f, 0.90f), 0.80f, speed: 1.6f));
        db.heroes.Add(MakeHero("zero", "제로", new Color(0.55f, 0.50f, 0.95f), 0.80f, speed: 1.6f));
        db.heroes.Add(MakeHero("sian", "시안", new Color(0.60f, 0.60f, 0.72f), 0.75f, speed: 2.4f));
        db.heroes.Add(MakeHero("bell", "벨", new Color(0.35f, 0.70f, 0.55f), 0.80f, speed: 1.7f));
        db.heroes.Add(MakeHero("eugene", "유진", new Color(0.90f, 0.65f, 0.35f), 0.80f, speed: 1.9f));
        db.heroes.Add(MakeHero("pipi", "피피", new Color(1.00f, 0.45f, 0.30f), 0.80f, speed: 1.6f));
        db.heroes.Add(MakeHero("zet", "제트", new Color(0.45f, 0.75f, 1.00f), 0.80f, speed: 1.7f));
        db.heroes.Add(MakeHero("momo", "모모", new Color(0.55f, 0.85f, 0.30f), 0.80f, speed: 1.6f));
        db.heroes.Add(MakeHero("lulu", "루루", new Color(1.00f, 0.70f, 0.85f), 0.80f, speed: 1.8f));

        return db;
    }

    static SkillDefinition MakeSkill(string id, string name, SkillKind kind,
        SkillActivation activation, WeaponRequirement weaponReq,
        float cooldown, float range = 0f, float radius = 0f, float duration = 0f,
        float tickInterval = 1f, float damagePercent = 0f, float effectValue = 0f,
        float effectValue2 = 0f)
    {
        var sk = ScriptableObject.CreateInstance<SkillDefinition>();
        sk.id = id;
        sk.displayName = name;
        sk.kind = kind;
        sk.activation = activation;
        sk.weaponRequirement = weaponReq;
        sk.cooldown = cooldown;
        sk.range = range;
        sk.radius = radius;
        sk.duration = duration;
        sk.tickInterval = tickInterval;
        sk.damagePercent = damagePercent;
        sk.effectValue = effectValue;
        sk.effectValue2 = effectValue2;
        return sk;
    }

    static HeroDefinition MakeHero(string id, string name, Color color, float size, float speed)
    {
        var d = ScriptableObject.CreateInstance<HeroDefinition>();
        d.id = id;
        d.displayName = name;
        d.color = color;
        d.size = size;
        d.unlockedByDefault = true; // 영입 보류 중 — 전원 기본 해금
        d.moveSpeed = speed;
        d.basicAttackPercent = 100f; // v2: 피해 원천은 영웅 공격력 — 개성 계수 제거
        d.attackRange = 0f;          // v2: 사거리/주기는 무기가 결정 (폴백 없음)
        d.attackInterval = 1f;
        return d;
    }

    // ---------------- 장비 (무기 5종 + 일반 장비) ----------------

    public static EquipmentDatabase CreateEquipmentDatabase()
    {
        var db = ScriptableObject.CreateInstance<EquipmentDatabase>();

        // ---- 무기 (무기 스펙 v2 표 그대로 — 타입 고정 공격력 보정 없음) ----
        db.items.Add(MakeWeapon("w_dagger", "단검", WeaponType.Dagger, AttackType.Melee,
            range: 1.2f, interval: 0.65f));
        db.items.Add(MakeWeapon("w_sword", "검", WeaponType.Sword, AttackType.Melee,
            range: 1.5f, interval: 1.0f));
        db.items.Add(MakeWeapon("w_greatsword", "대검", WeaponType.Greatsword, AttackType.Melee,
            range: 1.8f, interval: 1.5f, aoeRadius: 1.0f)); // 소범위 반경 임시 1.0
        db.items.Add(MakeWeapon("w_bow", "활", WeaponType.Bow, AttackType.Ranged,
            range: 6.0f, interval: 1.2f, projectileSpeed: 11f));
        db.items.Add(MakeWeapon("w_magictool", "마법 도구", WeaponType.MagicTool, AttackType.Ranged,
            range: 4.5f, interval: 1.4f, projectileSpeed: 9f));

        // ---- 일반 장비 (자유 슬롯) ----
        db.items.Add(MakeEquip("armor", "사슬 갑옷", Mod(StatType.MaxHP, flat: 40f)));
        db.items.Add(MakeEquip("boots", "바람의 신발", Mod(StatType.MoveSpeed, pct: 20f)));
        db.items.Add(MakeEquip("charm", "맹공의 부적", Mod(StatType.Attack, pct: 10f)));
        db.items.Add(MakeEquip("dice", "행운의 주사위", Mod(StatType.CritChance, flat: 4f)));
        db.items.Add(MakeEquip("edge", "예리한 숫돌", Mod(StatType.CritDamage, flat: 20f)));

        return db;
    }

    public static RunConfig CreateRunConfig()
    {
        return ScriptableObject.CreateInstance<RunConfig>(); // 필드 기본값 사용
    }

    static WeaponDefinition MakeWeapon(string id, string name, WeaponType type, AttackType attackType,
        float range, float interval, float aoeRadius = 0f, float projectileSpeed = 9f)
    {
        var w = ScriptableObject.CreateInstance<WeaponDefinition>();
        w.id = id;
        w.displayName = name;
        w.weaponType = type;
        w.attackType = attackType;
        w.attackRange = range;
        w.attackInterval = interval;
        w.aoeRadius = aoeRadius;
        w.projectileSpeed = projectileSpeed;
        w.modifiers = new StatModifier[0]; // 랜덤 옵션은 장비 개편에서
        return w;
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