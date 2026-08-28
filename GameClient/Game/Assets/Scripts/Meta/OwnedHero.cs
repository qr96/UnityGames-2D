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
    public HeroDefinition definition; // 신원/비주얼/공격타입/스킬 원본
    public int level = 1;
    public HeroStatBlock stats = new HeroStatBlock();
    public SkillDefinition activeSkill; // 생성 시 풀에서 랜덤 배정, 교체 불가 (액티브 스펙 v2)

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
/// 보유 영웅 로스터 — 저장 시스템 도입 전 인메모리 대체물.
/// HeroDefinition으로 진입하는 기존 흐름(SortieData/RunState)과의 호환 창구:
/// 정의당 1명의 OwnedHero를 자동 생성해 유지.
///   · 굴림 시드 = 정의 id 해시 → 세션을 다시 시작해도 같은 영웅은 같은 스탯
///     (영구 저장처럼 보이게 하는 개발용 장치. 정식 생성/저장 도입 시 교체)
/// </summary>
public static class HeroRoster
{
    static readonly Dictionary<string, OwnedHero> owned = new Dictionary<string, OwnedHero>();
    static List<SkillDefinition> skillPool = new List<SkillDefinition>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { owned.Clear(); skillPool.Clear(); }

    /// <summary>액티브 배정 풀 설정 — 부트스트랩이 영웅 생성 전에 호출.</summary>
    public static void SetSkillPool(List<SkillDefinition> pool)
    {
        skillPool = pool ?? new List<SkillDefinition>();
    }

    /// <summary>정의에 대응하는 보유 영웅. 없으면 임시 굴림으로 생성 (스탯 + 액티브 랜덤 배정).</summary>
    public static OwnedHero Get(HeroDefinition def)
    {
        if (def == null) return null;
        if (owned.TryGetValue(def.id, out var hero)) return hero;

        var rng = new System.Random(StableSeed(def.id)); // 세션 간 동일 굴림 (임시)
        hero = new OwnedHero(def.id, def, HeroStatBlock.Roll(rng));
        if (skillPool.Count > 0)
            hero.activeSkill = skillPool[rng.Next(skillPool.Count)]; // 영구 고정, 교체 불가
        owned[def.id] = hero;
        return hero;
    }

    /// <summary>정식 생성/저장 시스템이 만든 영웅 등록 (추후 사용)</summary>
    public static void Register(OwnedHero hero)
    {
        if (hero != null && !string.IsNullOrEmpty(hero.heroId))
            owned[hero.heroId] = hero;
    }

    /// <summary>문자열 → 안정 시드 (string.GetHashCode는 세션마다 달라질 수 있어 직접 계산)</summary>
    static int StableSeed(string s)
    {
        unchecked
        {
            int hash = 23;
            foreach (char c in s) hash = hash * 31 + c;
            return hash;
        }
    }
}