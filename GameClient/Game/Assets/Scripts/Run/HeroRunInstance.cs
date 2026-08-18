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

    public HeroDefinition definition;
    public List<EquipmentDefinition> equipment = new List<EquipmentDefinition>();

    public bool recruitedWhileLocked;  // 미해금 상태로 영입되었는가
    public bool diedThisRun;           // 런 중 한 번이라도 사망했는가 (해금 조건 판정용)

    public bool HasFreeSlot => equipment.Count < MaxEquipSlots;

    public HeroRunInstance(HeroDefinition def, bool recruitedWhileLocked)
    {
        definition = def;
        this.recruitedWhileLocked = recruitedWhileLocked;
    }

    /// <summary>
    /// 장비가 반영된 최종 스탯.
    /// 동일 장비 중첩 시에도 단순 합산 — 효율 감소 없음 (GDD 8).
    /// </summary>
    public float GetStat(StatType type)
    {
        float baseValue = definition.GetBaseStat(type);
        float flat = 0f, percent = 0f;

        foreach (var eq in equipment)
        {
            if (eq == null || eq.modifiers == null) continue;
            foreach (var m in eq.modifiers)
            {
                if (m.stat != type) continue;
                flat += m.flat;
                percent += m.percent;
            }
        }
        return (baseValue + flat) * (1f + percent / 100f);
    }
}
