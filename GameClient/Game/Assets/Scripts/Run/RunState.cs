using System.Collections.Generic;

/// <summary>
/// 런 1회의 전체 상태. 런이 끝나면 이 객체를 버림 → 장비/파티 자연 소멸 (GDD 8: 런 종료 시 소멸).
/// GDD 6: 시작 3명 → 영입 → 최대 5명, 벤치 없음.
/// </summary>
public class RunState
{
    public const int StartPartySize = 3;
    public const int MaxPartySize = 5;

    public readonly List<HeroRunInstance> party = new List<HeroRunInstance>();
    public readonly List<EquipmentDefinition> inventory = new List<EquipmentDefinition>();

    public int battleNumber = 1;      // 1부터 시작
    public int recruitChancesLeft;    // GDD 유력안: 2회 고정
    public bool inBattle;             // 전투 중 장비 변경 불가 가드 (GDD 8)

    public RunState(IEnumerable<HeroDefinition> starters, int recruitChances)
    {
        foreach (var d in starters)
        {
            if (party.Count >= StartPartySize) break;
            if (d != null) party.Add(new HeroRunInstance(d, recruitedWhileLocked: false));
        }
        recruitChancesLeft = recruitChances;
    }

    public bool Contains(HeroDefinition def) =>
        party.Exists(h => h.definition == def);

    public HeroRunInstance Recruit(HeroDefinition def, bool wasLocked)
    {
        if (def == null || party.Count >= MaxPartySize) return null;
        var inst = new HeroRunInstance(def, wasLocked);
        party.Add(inst);
        return inst;
    }

    // ---------- 장비 규칙 (GDD 8) ----------

    /// <summary>인벤토리 → 영웅 슬롯. 전투 중에는 불가.</summary>
    public bool Equip(HeroRunInstance hero, EquipmentDefinition item)
    {
        if (inBattle) return false;          // 전투 중 장비 변경 불가
        if (hero == null || item == null) return false;
        if (!hero.HasFreeSlot) return false; // 자유 슬롯 3칸
        if (!inventory.Remove(item)) return false;
        hero.equipment.Add(item);            // 카테고리 제한 없음, 동일 장비 중첩 가능
        return true;
    }

    /// <summary>영웅 슬롯 → 인벤토리. 전투 사이 자유로운 탈착/영웅 간 이전의 기반.</summary>
    public bool Unequip(HeroRunInstance hero, int slotIndex)
    {
        if (inBattle) return false;
        if (hero == null || slotIndex < 0 || slotIndex >= hero.equipment.Count) return false;
        inventory.Add(hero.equipment[slotIndex]);
        hero.equipment.RemoveAt(slotIndex);
        return true;
    }
}
