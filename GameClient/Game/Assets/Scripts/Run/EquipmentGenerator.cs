using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장비 절차 생성기 (장비 명세 v1.2 + 던전 명세: 장비 드롭).
///
/// 생성 흐름 (§13):
///   [드랍] 방 보상 → 개수 → 각 드롭의 특별 여부 → EquipmentType(1/3씩)
///          → Weapon이면 WeaponType(1/5씩) → RewardLevel 전달 → 생성
///   [비무기] POWER_GEAR=공격력 깡스탯 / VITAL_GEAR=HP 깡스탯 → RL 보간 롤 → 공통 옵션 1 → (특별) 고유효과 1
///   [무기]   고정 프로필(§12) + 공격력 깡스탯 → 공통 옵션 1 → (특별) 고유효과 1
///
/// 깡스탯 RL 보간 (§3): t = clamp((R-1)/(Rmax-1)), Min/Max = lerp(Start, End, t), 반올림.
/// 부가옵션(§4)과 고유효과(§8)는 RL과 무관 — 깊이에 따라 강해지지 않음.
/// 획득처 차이는 개수·특별 확률뿐, 같은 RL이면 깡스탯 범위 동일.
/// 수치 전부 프로토타입 임시값 — Config로 데이터화.
/// </summary>
public static class EquipmentGenerator
{
    [System.Serializable]
    public class Config
    {
        [Header("RewardLevel (장비 명세 §3 — Rmax는 실제 최대 층과 별개로 데이터화)")]
        public int rewardLevelMax = 40;

        [Header("깡스탯 보간 범위 (Start = RL1, End = RL Rmax)")]
        public Vector2 attackFlatStart = new Vector2(1f, 3f);
        public Vector2 attackFlatEnd = new Vector2(9f, 13f);
        public Vector2 hpFlatStart = new Vector2(15f, 30f);
        public Vector2 hpFlatEnd = new Vector2(90f, 130f);

        [Header("획득처별 드랍 (던전 명세 — 개수 / 특별 확률 %)")]
        public int normalDropCount = 1;
        public float normalSpecialChance = 1f;
        public int eliteDropCount = 2;
        public float eliteSpecialChance = 8f;
        public int treasureDropCount = 3;
        public float treasureSpecialChance = 3f;
    }

    // 공통 추가 옵션 8종 (§4) — 장비당 정확히 1개, 동일 가중치, RL 무관
    class OptionDef
    {
        public string label;
        public StatType stat;
        public bool isPercent;   // false = flat(%p 포함)
        public int min, max;     // 정수 롤 (§4)
        public string Format(int v) => isPercent ? $"{label}+{v}%" : $"{label}+{v}";
    }

    static readonly OptionDef[] Options =
    {
        new OptionDef { label = "공격",   stat = StatType.Attack,            isPercent = true,  min = 6,  max = 12 }, // ATK_PCT
        new OptionDef { label = "HP",     stat = StatType.MaxHP,             isPercent = true,  min = 8,  max = 15 }, // HP_PCT
        new OptionDef { label = "치확",   stat = StatType.CritChance,        isPercent = false, min = 2,  max = 4  }, // CRIT_CHANCE (%p)
        new OptionDef { label = "치피",   stat = StatType.CritDamage,        isPercent = false, min = 10, max = 20 }, // CRIT_DAMAGE (%p)
        new OptionDef { label = "피해감소", stat = StatType.DamageReduction,  isPercent = false, min = 4,  max = 8  }, // DAMAGE_REDUCTION
        new OptionDef { label = "쿨감",   stat = StatType.CooldownReduction, isPercent = false, min = 5,  max = 10 }, // CDR
        new OptionDef { label = "사거리", stat = StatType.AttackRange,       isPercent = true,  min = 8,  max = 15 }, // RANGE
        new OptionDef { label = "공속",   stat = StatType.AttackInterval,    isPercent = true,  min = 5,  max = 10 }, // ATTACK_INTERVAL (주기 감소 — 음수 적용)
    };

    static readonly UniqueEffect[] UniquePool =
    {
        UniqueEffect.LastStand, UniqueEffect.HealthyFury, UniqueEffect.Execution,
        UniqueEffect.Bloodthirst, UniqueEffect.DropFury, UniqueEffect.DropGuard,
        UniqueEffect.ActiveStrike, UniqueEffect.EmergencyShield,
    };

    static readonly string[] UniqueNames =
    {
        "", "최후의 저항", "건강한 격노", "처형", "피의 갈증",
        "낙하의 격노", "낙하의 수호", "연계 일격", "비상 보호막",
    };

    // 무기 고정 프로필 (§12) — 랜덤 생성하지 않음
    class WeaponProfile
    {
        public WeaponType type;
        public string name;
        public AttackType attackType;
        public float range, interval, aoeRadius, projectileSpeed;
    }

    static readonly WeaponProfile[] Weapons =
    {
        new WeaponProfile { type = WeaponType.Dagger,     name = "단검",     attackType = AttackType.Melee,  range = 1.2f, interval = 0.65f },
        new WeaponProfile { type = WeaponType.Sword,      name = "검",       attackType = AttackType.Melee,  range = 1.5f, interval = 1.0f },
        new WeaponProfile { type = WeaponType.Greatsword, name = "대검",     attackType = AttackType.Melee,  range = 1.8f, interval = 1.5f, aoeRadius = 1.0f },
        new WeaponProfile { type = WeaponType.Bow,        name = "활",       attackType = AttackType.Ranged, range = 6.0f, interval = 1.2f, projectileSpeed = 11f },
        new WeaponProfile { type = WeaponType.MagicTool,  name = "마법 도구", attackType = AttackType.Ranged, range = 4.5f, interval = 1.4f, projectileSpeed = 9f },
    };

    // =================================================================
    //  공개 API
    // =================================================================

    /// <summary>획득처별 드랍 일괄 생성 — 개수/특별 확률은 소스로 구분, 깡스탯 범위는 RL만 따름.</summary>
    public static List<EquipmentDefinition> GenerateDrops(NodeContent source, int rewardLevel,
        Config cfg, System.Random rng)
    {
        cfg ??= new Config();
        int count; float specialChance;
        switch (source)
        {
            case NodeContent.EliteBattle: count = cfg.eliteDropCount; specialChance = cfg.eliteSpecialChance; break;
            case NodeContent.Treasure: count = cfg.treasureDropCount; specialChance = cfg.treasureSpecialChance; break;
            default: count = cfg.normalDropCount; specialChance = cfg.normalSpecialChance; break;
        }

        var drops = new List<EquipmentDefinition>();
        for (int i = 0; i < count; i++)
        {
            bool special = rng.NextDouble() * 100.0 < specialChance;
            drops.Add(GenerateOne(special, rewardLevel, cfg, rng));
        }
        return drops;
    }

    /// <summary>장비 1개 생성 — EquipmentType 1/3씩 (Weapon / POWER_GEAR / VITAL_GEAR).</summary>
    public static EquipmentDefinition GenerateOne(bool special, int rewardLevel, Config cfg, System.Random rng)
    {
        cfg ??= new Config();
        int typeRoll = rng.Next(3);
        if (typeRoll == 0) // Weapon — WeaponType 1/5씩
            return GenerateWeapon((WeaponType)rng.Next(Weapons.Length), rewardLevel, special, cfg, rng);
        return GenerateGear(powerGear: typeRoll == 1, special, rewardLevel, cfg, rng);
    }

    /// <summary>비무기 생성 — POWER_GEAR(공격력) / VITAL_GEAR(최대 HP).</summary>
    public static EquipmentDefinition GenerateGear(bool powerGear, bool special, int rewardLevel,
        Config cfg, System.Random rng)
    {
        cfg ??= new Config();
        var item = ScriptableObject.CreateInstance<EquipmentDefinition>();
        int flat = powerGear
            ? RollFlat(cfg.attackFlatStart, cfg.attackFlatEnd, rewardLevel, cfg, rng)
            : RollFlat(cfg.hpFlatStart, cfg.hpFlatEnd, rewardLevel, cfg, rng);

        var option = RollOption(rng, out var optionMod, out string optionText);
        item.modifiers = new[]
        {
            new StatModifier { stat = powerGear ? StatType.Attack : StatType.MaxHP, flat = flat },
            optionMod,
        };

        ApplySpecial(item, special, rng, out string uniqueText);
        string baseName = powerGear ? "힘의 장구" : "생명의 장구"; // 외형/이름은 자유 — 전투 타입은 2종 (§1)
        string flatText = powerGear ? $"공격+{flat}" : $"HP+{flat}";
        item.id = $"gen_{(powerGear ? "power" : "vital")}_{rng.Next(int.MaxValue):x}";
        item.displayName = ComposeName(special, baseName, flatText, optionText, uniqueText);
        return item;
    }

    /// <summary>무기 생성 — 고정 프로필(§12) + 공격력 깡스탯. 시작 무기 지급에도 사용.</summary>
    public static WeaponDefinition GenerateWeapon(WeaponType type, int rewardLevel, bool special,
        Config cfg, System.Random rng)
    {
        cfg ??= new Config();
        var p = System.Array.Find(Weapons, w => w.type == type) ?? Weapons[1];

        var item = ScriptableObject.CreateInstance<WeaponDefinition>();
        item.weaponType = p.type;
        item.attackType = p.attackType;
        item.attackRange = p.range;
        item.attackInterval = p.interval;
        item.aoeRadius = p.aoeRadius;
        item.projectileSpeed = p.projectileSpeed;

        int flat = RollFlat(cfg.attackFlatStart, cfg.attackFlatEnd, rewardLevel, cfg, rng); // 모든 무기 깡스탯 = 공격력 (§1)
        var option = RollOption(rng, out var optionMod, out string optionText);
        item.modifiers = new[]
        {
            new StatModifier { stat = StatType.Attack, flat = flat },
            optionMod,
        };

        ApplySpecial(item, special, rng, out string uniqueText);
        item.id = $"gen_weapon_{p.type}_{rng.Next(int.MaxValue):x}";
        item.displayName = ComposeName(special, p.name, $"공격+{flat}", optionText, uniqueText);
        return item;
    }

    // =================================================================
    //  내부
    // =================================================================

    /// <summary>깡스탯 롤 — RL 선형 보간 후 정수 반올림 (§3, 정수화 정책: 반올림)</summary>
    static int RollFlat(Vector2 start, Vector2 end, int rewardLevel, Config cfg, System.Random rng)
    {
        float t = Mathf.Clamp01((rewardLevel - 1f) / Mathf.Max(1f, cfg.rewardLevelMax - 1f));
        float min = Mathf.Lerp(start.x, end.x, t);
        float max = Mathf.Lerp(start.y, end.y, t);
        return Mathf.RoundToInt(min + (float)rng.NextDouble() * (max - min));
    }

    /// <summary>공통 옵션 1개 롤 — 8종 동일 가중치, 정수 롤 (§4)</summary>
    static OptionDef RollOption(System.Random rng, out StatModifier mod, out string text)
    {
        var opt = Options[rng.Next(Options.Length)];
        int v = rng.Next(opt.min, opt.max + 1);
        // ATTACK_INTERVAL은 '주기 감소'라 음수 percent로 적용 (GetStat에서 30% 상한)
        float value = opt.stat == StatType.AttackInterval ? -v : v;
        mod = opt.isPercent
            ? new StatModifier { stat = opt.stat, percent = value }
            : new StatModifier { stat = opt.stat, flat = value };
        text = opt.stat == StatType.AttackInterval ? $"공속+{v}%" : opt.Format(v);
        return opt;
    }

    static void ApplySpecial(EquipmentDefinition item, bool special, System.Random rng, out string uniqueText)
    {
        uniqueText = "";
        if (!special) return;
        item.isSpecial = true;
        item.uniqueEffect = UniquePool[rng.Next(UniquePool.Length)];
        uniqueText = UniqueName(item.uniqueEffect);
    }

    public static string UniqueName(UniqueEffect effect) => UniqueNames[(int)effect];

    /// <summary>생명 장구인가 — EquipmentDefinition에 타입 필드가 없어 깡스탯(modifiers)으로 분류 (§3: 장구 깡스탯은 공격 또는 HP 하나)</summary>
    public static bool IsVitalGear(EquipmentDefinition item)
    {
        if (item == null || item.modifiers == null) return false;
        foreach (var m in item.modifiers)
            if (m.stat == StatType.MaxHP && m.flat > 0f) return true;
        return false;
    }

    /// <summary>슬롯 칸용 축약명 — "★검" / "힘" / "생명" (전체 정보는 상세 줄/목록에서)</summary>
    public static string ShortName(EquipmentDefinition item)
    {
        if (item == null) return "";
        string star = item.isSpecial ? "★" : "";
        if (item is WeaponDefinition w) return star + WeaponName(w.weaponType);
        return star + (IsVitalGear(item) ? "생명" : "힘");
    }

    /// <summary>무기 타입 한글명 (프로필 표 공유)</summary>
    public static string WeaponName(WeaponType type)
    {
        foreach (var p in Weapons)
            if (p.type == type) return p.name;
        return type.ToString();
    }

    /// <summary>표시명 — 슬롯 UI가 이름 문자열만 보여주므로 내용 요약을 이름에 담음 (전용 툴팁 전까지)</summary>
    static string ComposeName(bool special, string baseName, string flatText, string optionText, string uniqueText)
    {
        string name = $"{baseName} ({flatText}, {optionText})";
        if (special) name = $"★{name} [{uniqueText}]";
        return name;
    }
}