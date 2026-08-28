using System.Collections.Generic;

/// <summary>
/// 런 1회의 전체 상태. 런이 끝나면 이 객체를 버림 → 장비/파티 자연 소멸 (GDD 8: 런 종료 시 소멸).
/// 탐험 규칙: 원정 시작 시 5명 선정 후 고정 (영입 시스템은 보류 중 — recruitChances=0).
/// </summary>
public class RunState
{
    public const int StartPartySize = 5;
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

    // ---------- 장비 규칙 (GDD 8 + 무기 스펙 v2) ----------

    /// <summary>인벤토리 → 영웅의 빈 슬롯에 장착 (무기는 전용 무기 슬롯으로 라우팅). 전투 중에는 불가.</summary>
    public bool Equip(HeroRunInstance hero, EquipmentDefinition item)
    {
        if (item is WeaponDefinition w) return EquipWeapon(hero, w);
        return hero != null && EquipAt(hero, item, hero.equipment.Count);
    }

    /// <summary>
    /// 인벤토리 → 영웅의 특정 슬롯에 장착. 무기는 자유 슬롯에 들어갈 수 없음 (전용 슬롯으로 라우팅).
    /// 점유된 슬롯이면 기존 장비를 인벤토리로 돌려보내고 교체 (드래그 스왑 탈착).
    /// </summary>
    public bool EquipAt(HeroRunInstance hero, EquipmentDefinition item, int slotIndex)
    {
        if (inBattle) return false;          // 전투 중 장비 변경 불가 (GDD 8)
        if (hero == null || item == null) return false;
        if (item is WeaponDefinition w) return EquipWeapon(hero, w); // 무기 스펙 v2
        if (slotIndex < 0 || slotIndex >= HeroRunInstance.MaxEquipSlots) return false;
        if (!inventory.Contains(item)) return false;

        if (slotIndex < hero.equipment.Count)
        {
            // 교체: 기존 장비는 인벤토리로 (탈착 → 다른 영웅에게 이전 가능)
            inventory.Remove(item);
            inventory.Add(hero.equipment[slotIndex]);
            hero.equipment[slotIndex] = item;
            return true;
        }

        // 빈 슬롯: 앞에서부터 채워 장착
        if (!hero.HasFreeSlot) return false;
        inventory.Remove(item);
        hero.equipment.Add(item);            // 카테고리 제한 없음, 동일 장비 중첩 가능
        return true;
    }

    /// <summary>
    /// 장착된 장비를 다른 칸으로 이동 (영웅 간 이전 / 같은 영웅 내 교환).
    /// 목적지가 점유 칸이면 두 장비를 서로 교환. 빈 칸이면 이동 (같은 영웅의 빈 칸은 의미 없어 거부).
    /// </summary>
    public bool MoveEquipped(HeroRunInstance from, int fromIndex, HeroRunInstance to, int toIndex)
    {
        if (inBattle) return false; // 전투 중 장비 변경 불가 (GDD 8)
        if (from == null || to == null) return false;
        if (fromIndex < 0 || fromIndex >= from.equipment.Count) return false;
        if (toIndex < 0 || toIndex >= HeroRunInstance.MaxEquipSlots) return false;
        if (from == to && fromIndex == toIndex) return false;

        EquipmentDefinition item = from.equipment[fromIndex];

        if (toIndex < to.equipment.Count)
        {
            // 교환 (영웅 간 또는 같은 영웅의 칸끼리)
            EquipmentDefinition other = to.equipment[toIndex];
            to.equipment[toIndex] = item;
            from.equipment[fromIndex] = other;
            return true;
        }

        // 빈 칸으로 이동
        if (from == to) return false;        // 같은 영웅의 빈 칸 = 제자리 (리스트는 앞에서부터 채워짐)
        if (!to.HasFreeSlot) return false;
        from.equipment.RemoveAt(fromIndex);
        to.equipment.Add(item);
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

    // ---------- 무기 전용 슬롯 (무기 스펙 v2) ----------

    /// <summary>인벤토리의 무기 → 전용 무기 슬롯. 점유 시 기존 무기는 인벤토리로 교체.</summary>
    public bool EquipWeapon(HeroRunInstance hero, WeaponDefinition weapon)
    {
        if (inBattle) return false; // 전투 중 장비 변경 불가 (GDD 8)
        if (hero == null || weapon == null) return false;
        if (!inventory.Contains(weapon)) return false;

        inventory.Remove(weapon);
        if (hero.weapon != null) inventory.Add(hero.weapon);
        hero.weapon = weapon;
        return true;
    }

    /// <summary>무기 슬롯 → 인벤토리. 미장착 상태 허용 (기본 공격 불가 상태).</summary>
    public bool UnequipWeapon(HeroRunInstance hero)
    {
        if (inBattle) return false;
        if (hero == null || hero.weapon == null) return false;
        inventory.Add(hero.weapon);
        hero.weapon = null;
        return true;
    }
}