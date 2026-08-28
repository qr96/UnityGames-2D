using System;
using System.Collections.Generic;

/// <summary>
/// 런 안에서의 영웅 1명 (정의 + 런 한정 상태).
/// GDD 8: 영웅당 자유 슬롯 3칸, 카테고리 제한 없음, 동일 장비 중첩 가능, 중첩 효율 감소 없음.
/// GDD 7: 미해금 상태로 영입 → 죽이지 않고 런 클리어 → 영구 해금 (추적 플래그 보유).
/// </summary>
[Serializable]
public class HeroRunInstance
{
    public const int MaxEquipSlots = 3;

    public OwnedHero owned;            // 영구 영웅 (레벨 + 굴림 스탯 + 고정 액티브) — 영웅 스펙 v2
    public HeroDefinition definition;  // = owned.definition (기존 참조부 호환용)
    public WeaponDefinition weapon;    // 전용 무기 슬롯 (무기 스펙 v2) — null = 미장착(기본 공격 불가)
    public List<EquipmentDefinition> equipment = new List<EquipmentDefinition>();

    public bool recruitedWhileLocked;  // 미해금 상태로 영입되었는가
    public bool diedThisRun;           // 런 중 한 번이라도 사망했는가 (해금 조건 판정용)

    // ---- 전투 간 이월 상태 (확정: HP 이월, 사망은 교회에서 부활할 때까지 유지) ----
    public float currentHP = -1f;      // 음수 = 미기록(최대치로 스폰). 필드의 Hero가 상시 동기화.
    public bool isDead;                // 사망 유지 — 야영지 휴식으로 회복되지 않음 (부활은 교회 담당)

    public bool HasFreeSlot => equipment.Count < MaxEquipSlots;

    public HeroRunInstance(OwnedHero owned, bool recruitedWhileLocked)
    {
        this.owned = owned;
        definition = owned != null ? owned.definition : null;
        this.recruitedWhileLocked = recruitedWhileLocked;
    }

    /// <summary>기존 호환 — 정의로 생성 시 로스터에서 영구 영웅을 찾음</summary>
    public HeroRunInstance(HeroDefinition def, bool recruitedWhileLocked)
        : this(HeroRoster.Get(def), recruitedWhileLocked) { }

    /// <summary>
    /// 장비가 반영된 최종 스탯.
    /// 기본치: HP/공격력은 영구 영웅의 레벨 곡선, 치확/치피는 굴림값, 나머지는 정의.
    /// 동일 장비 중첩 시에도 단순 합산 — 효율 감소 없음 (GDD 8).
    /// </summary>
    public float GetStat(StatType type)
    {
        float baseValue = BaseStat(type);
        float flat = 0f, percent = 0f;

        Accumulate(weapon, type, ref flat, ref percent); // 무기의 랜덤 옵션도 일반 수정치로 합산
        foreach (var eq in equipment)
            Accumulate(eq, type, ref flat, ref percent);
        return (baseValue + flat) * (1f + percent / 100f);
    }

    /// <summary>
    /// 스탯 기본치 —
    ///   HP/공격/치확/치피: 영구 영웅(레벨/굴림)
    ///   공격 사거리/주기: 장착 무기 (미장착이면 0 — Hero가 기본 공격 불가 처리)
    ///   나머지: 정의값 폴백
    /// </summary>
    float BaseStat(StatType type)
    {
        if (owned != null)
        {
            switch (type)
            {
                case StatType.MaxHP: return owned.MaxHP;
                case StatType.Attack: return owned.Attack;
                case StatType.CritChance: return owned.CritChance;
                case StatType.CritDamage: return owned.CritDamage;
            }
        }
        switch (type) // 무기 스펙 v2: 사거리/공격주기의 원천은 무기
        {
            case StatType.AttackRange: return weapon != null ? weapon.attackRange : 0f;
            case StatType.AttackInterval: return weapon != null ? weapon.attackInterval : 1f;
        }
        return definition != null ? definition.GetBaseStat(type) : 0f;
    }

    static void Accumulate(EquipmentDefinition eq, StatType type, ref float flat, ref float percent)
    {
        if (eq == null || eq.modifiers == null) return;
        foreach (var m in eq.modifiers)
        {
            if (m.stat != type) continue;
            flat += m.flat;
            percent += m.percent;
        }
    }
}