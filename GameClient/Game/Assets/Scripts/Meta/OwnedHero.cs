using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영웅 개인 스탯 블록 — 영웅 생성 시 범위 내에서 굴려져 영구 고정 (영웅 스펙 v2).
///   HP:     Lv.1 80~120 → Lv.10 140~220 (선형 성장)
///   공격력: Lv.1 8~12   → Lv.10 14~22  (선형 성장)
///   치확 3~7% / 치피 140~160% — 레벨 성장 없음
/// </summary>
[Serializable]
public class HeroStatBlock
{
    public float hpLv1;
    public float hpLv10;
    public float attackLv1;
    public float attackLv10;
    public float critChance;  // % (3~7)
    public float critDamage;  // % (140~160)

    public float HPAt(int level) => Lerp10(hpLv1, hpLv10, level);
    public float AttackAt(int level) => Lerp10(attackLv1, attackLv10, level);

    /// <summary>Lv.1~10 선형 보간</summary>
    static float Lerp10(float lv1, float lv10, int level)
    {
        float t = (Mathf.Clamp(level, 1, OwnedHero.MaxLevel) - 1) / 9f;
        return Mathf.Lerp(lv1, lv10, t);
    }

    /// <summary>스펙 범위 내 랜덤 굴림. ※ 임시 — 정식 생성 로직 확정 시 교체.</summary>
    public static HeroStatBlock Roll(System.Random rng)
    {
        return new HeroStatBlock
        {
            hpLv1 = Range(rng, 80f, 120f),
            hpLv10 = Range(rng, 140f, 220f),
            attackLv1 = Range(rng, 8f, 12f),
            attackLv10 = Range(rng, 14f, 22f),
            critChance = Range(rng, 3f, 7f),
            critDamage = Range(rng, 140f, 160f),
        };
    }

    static float Range(System.Random rng, float min, float max) =>
        Mathf.Round((min + (float)rng.NextDouble() * (max - min)) * 10f) / 10f; // 소수 1자리
}

/// <summary>
/// 영구 영웅 인스턴스 — 런 밖에서 유지되는 '내 영웅 1명' (영웅 스펙 v2).
/// HeroDefinition(불변 에셋: 이름/비주얼/공격 타입/스킬)과
/// HeroRunInstance(런 한정: 장비/HP 이월/사망) 사이의 영속 계층.
///   · 레벨: 영구 저장 대상 (최대 10). 경험치/레벨업 규칙은 추후 — SetLevel만 제공.
///   · 스탯: 생성 시 굴려져 고정 (stats).
/// ※ 저장 시스템 도입 시 이 클래스가 직렬화 대상.
/// </summary>
[Serializable]
public class OwnedHero
{
    public const int MaxLevel = 10;

    public string heroId;             // 영구 인스턴스 고유 키 (저장/조회용)
    public HeroDefinition definition; // 신원/비주얼 원본
    public int level = 1;
    public HeroStatBlock stats = new HeroStatBlock();
    public SkillDefinition activeSkill; // 생성 시 풀에서 랜덤 배정, 교체 불가 (액티브 스펙 v2)
    public string traitId = "";         // 조건부 고유 특성 1개 — 효과는 목록 확정 후 (TraitCatalog)

    // ---- 장비 영속 v1: 장착 상태는 영웅에 유지 (런 종료로 소멸하지 않음) ----
    // 사망(로스터 제거) 시 영웅과 함께 소멸 = "사망 시 장착 장비 소멸" 규칙이 자동 성립.
    public List<EquipmentDefinition> equipment = new List<EquipmentDefinition>();
    public WeaponDefinition weapon;

    // 시작 영웅 전용: 확정 지급 무기 (표 기준). 랜덤 영웅은 없음 → 임시 지급 로직 사용
    public bool hasFixedWeapon;
    public WeaponType fixedWeapon;

    public OwnedHero(string heroId, HeroDefinition definition, HeroStatBlock stats, int level = 1)
    {
        this.heroId = heroId;
        this.definition = definition;
        this.stats = stats;
        this.level = Mathf.Clamp(level, 1, MaxLevel);
    }

    public float MaxHP => stats.HPAt(level);
    public float Attack => stats.AttackAt(level);
    public float CritChance => stats.critChance;
    public float CritDamage => stats.critDamage;

    public void SetLevel(int value) => level = Mathf.Clamp(value, 1, MaxLevel);

    /// <summary>레벨업 (경험치 규칙 확정 전 임시 API — 호출부는 추후 시스템이 담당)</summary>
    public void LevelUp() => SetLevel(level + 1);
}

/// <summary>
/// 보유 영웅 로스터 (영입 스펙 v1) — 저장 시스템 도입 전 인메모리 대체물.
///   · 최대 8명 보유, 출전은 별도(최대 5명 — RunState)
///   · 시작 영웅: 고정 3명 지급 (랜덤 아님 — 스탯 중간값 고정, 액티브 고정)
///   · 영입: RecruitShop에서 랜덤 생성 후보를 골드로 영입 → Recruit()
///   · 해고: 가능, 환급 없음 → Dismiss()
///   · 사망: 영구 사망 — 원정 종료 시 RemoveDeadFrom()으로 로스터에서 제거
///   · ※ 임시 안전장치: 로스터가 비면 시작 3명 재지급 (전멸 소프트락 방지 — 정식 규칙 확정 시 교체)
/// 저장 시스템 도입 시 heroes 리스트가 직렬화 대상.
/// </summary>
public static class HeroRoster
{
    public const int MaxRoster = 8;

    static readonly List<OwnedHero> heroes = new List<OwnedHero>();
    static List<SkillDefinition> skillPool = new List<SkillDefinition>();

    public static IReadOnlyList<OwnedHero> Heroes => heroes;
    public static bool HasSpace => heroes.Count < MaxRoster;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { heroes.Clear(); skillPool.Clear(); }

    /// <summary>액티브 배정 풀 설정 — 부트스트랩이 영웅 생성 전에 호출.</summary>
    public static void SetSkillPool(List<SkillDefinition> pool)
    {
        skillPool = pool ?? new List<SkillDefinition>();
    }

    // ---------------- 시작 영웅 ----------------

    /// <summary>
    /// 로스터가 비어 있으면 고정 시작 영웅 3명 지급 (시작 영웅 표 확정치 — 랜덤 아님).
    ///   브란: HP 115/175, 공격 9/17, 치확 4%/치피 150%, 전투 함성, 끈질김, 검
    ///   리나: HP 90/160, 공격 12/20, 치확 7%/치피 155%, 저격, 처형인, 활
    ///   오웬: HP 100/185, 공격 10/18, 치확 5%/치피 145%, 응급 치료, 전우애, 마법 도구
    /// (전멸로 로스터가 비어도 재지급 — 임시 소프트락 방지)
    /// </summary>
    public static void EnsureStarters(HeroDatabase db)
    {
        if (heroes.Count > 0 || db == null) return;
        var starters = db.starters.Count >= 3 ? db.starters : db.heroes; // 폴백: 일반 목록 앞 3명
        if (starters.Count == 0) return;

        AddStarter(starters, 0, Block(115f, 175f, 9f, 17f, 4f, 150f), "battlecry", "tenacity", WeaponType.Sword);
        AddStarter(starters, 1, Block(90f, 160f, 12f, 20f, 7f, 155f), "snipe", "executioner", WeaponType.Bow);
        AddStarter(starters, 2, Block(100f, 185f, 10f, 18f, 5f, 145f), "firstaid", "camaraderie", WeaponType.MagicTool);
    }

    static void AddStarter(List<HeroDefinition> defs, int index,
        HeroStatBlock block, string skillId, string traitId, WeaponType weapon)
    {
        if (index >= defs.Count || defs[index] == null) return;
        var def = defs[index];
        var hero = new OwnedHero($"starter_{def.id}", def, block)
        {
            activeSkill = FindSkill(skillId) ?? RandomSkill(new System.Random(index)),
            traitId = traitId,
            hasFixedWeapon = true,
            fixedWeapon = weapon,
        };
        GrantInitialWeapon(hero, new System.Random(index)); // 생성 시 지급 — 로비에서 '미장착' 오해 방지
        heroes.Add(hero);
    }

    static HeroStatBlock Block(float hp1, float hp10, float a1, float a10, float cc, float cd) =>
        new HeroStatBlock { hpLv1 = hp1, hpLv10 = hp10, attackLv1 = a1, attackLv10 = a10, critChance = cc, critDamage = cd };

    // ---------------- 생성 / 영입 / 해고 / 사망 ----------------

    /// <summary>랜덤 영웅 생성 (영입 후보용 — 로스터 미등록 상태로 반환). 외형은 DB 템플릿 랜덤 재사용.</summary>
    public static OwnedHero CreateRandomHero(HeroDatabase db, System.Random rng)
    {
        if (db == null || db.heroes.Count == 0) return null;
        var def = db.heroes[rng.Next(db.heroes.Count)];
        var hero = new OwnedHero(System.Guid.NewGuid().ToString("N"), def, HeroStatBlock.Roll(rng));
        hero.activeSkill = RandomSkill(rng);
        hero.traitId = TraitCatalog.RandomId(rng); // 특성 1개 (효과는 목록 확정 후)
        GrantInitialWeapon(hero, rng);
        return hero;
    }

    /// <summary>로스터에 편입 (영입 확정 시 RecruitShop이 호출). 가득 차면 실패.</summary>
    public static bool Recruit(OwnedHero hero)
    {
        if (hero == null || !HasSpace || heroes.Contains(hero)) return false;
        heroes.Add(hero);
        return true;
    }

    /// <summary>해고 — 골드 환급 없음 (영입 스펙 v1). 장착 장비/무기는 보관소로 회수 (장비 영속 v1).</summary>
    public static bool Dismiss(OwnedHero hero)
    {
        if (hero == null || !heroes.Remove(hero)) return false;

        foreach (var item in hero.equipment)
            Armory.Add(item);
        hero.equipment.Clear();
        if (hero.weapon != null)
        {
            Armory.Add(hero.weapon);
            hero.weapon = null;
        }
        return true;
    }

    /// <summary>원정 종료 시 사망 영웅 영구 제거. 제거된 수를 반환.</summary>
    public static int RemoveDeadFrom(IEnumerable<HeroRunInstance> party)
    {
        int removed = 0;
        foreach (var inst in party)
            if (inst != null && inst.isDead && inst.owned != null && heroes.Remove(inst.owned))
                removed++;
        return removed;
    }

    // ---------------- 조회 ----------------

    /// <summary>heroId 우선, 없으면 정의 id로 검색 (구 SortieData 호환).</summary>
    public static OwnedHero FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var h in heroes)
            if (h.heroId == id) return h;
        foreach (var h in heroes)
            if (h.definition != null && h.definition.id == id) return h;
        return null;
    }

    /// <summary>정의로 보유 영웅 검색 (기존 정의 기반 흐름 호환 — 없으면 null).</summary>
    public static OwnedHero Get(HeroDefinition def) =>
        def != null ? FindById(def.id) : null;

    /// <summary>
    /// ※ 임시 (무기 획득 흐름 확정 전): 생성 시 기본 무기 1회 지급 — RL1, 비특별.
    /// 시작 영웅은 표 확정 타입, 랜덤 영웅은 액티브 무기 조건에 맞는 타입.
    /// 지급 시점을 출정 → 생성으로 옮긴 이유: 장비 영속에서 로비 '미장착 — 기본 공격 불가'
    /// 표시가 오해를 부름 (RunManager의 출정 시 지급은 안전망으로 유지 — 무기 없을 때만).
    /// </summary>
    static void GrantInitialWeapon(OwnedHero hero, System.Random rng)
    {
        if (hero == null || hero.weapon != null) return;

        WeaponType type;
        if (hero.hasFixedWeapon)
        {
            type = hero.fixedWeapon;
        }
        else
        {
            var req = hero.activeSkill != null ? hero.activeSkill.weaponRequirement : WeaponRequirement.None;
            switch (req)
            {
                case WeaponRequirement.Bow: type = WeaponType.Bow; break;
                case WeaponRequirement.MagicTool: type = WeaponType.MagicTool; break;
                case WeaponRequirement.Melee: type = WeaponType.Sword; break;
                default: type = (WeaponType)rng.Next(5); break;
            }
        }
        hero.weapon = EquipmentGenerator.GenerateWeapon(type, rewardLevel: 1, special: false, null, rng);
    }

    static SkillDefinition FindSkill(string id) =>
        skillPool.Find(s => s != null && s.id == id);

    static SkillDefinition RandomSkill(System.Random rng) =>
        skillPool.Count > 0 ? skillPool[rng.Next(skillPool.Count)] : null;
}