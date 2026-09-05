using UnityEngine;

/// <summary>
/// 로비 장비 관리 도메인 (장비 관리 개편 ①) — 보관소(Armory) ↔ 보유 영웅(OwnedHero).
/// RunState의 장착 코드는 '런 인벤토리·전투 중 금지' 전제라 로비에서 재사용 불가 — 별도 서비스.
/// 규칙은 동일: 무기칸엔 무기만, 자유 장비칸 3, 교체 시 기존 장비는 보관소로.
/// 장비 영속 v1: 여기서의 변경은 즉시 영구 반영 (별도 저장 절차 없음).
/// </summary>
public static class EquipService
{
    /// <summary>보관소 무기 → 무기칸 (점유 시 교체 — 기존 무기는 보관소로)</summary>
    public static bool EquipWeapon(OwnedHero hero, WeaponDefinition weapon)
    {
        if (hero == null || weapon == null) return false;
        if (!Armory.Items.Contains(weapon)) return false;

        Armory.Items.Remove(weapon);
        if (hero.weapon != null) Armory.Items.Add(hero.weapon);
        hero.weapon = weapon;
        return true;
    }

    /// <summary>보관소 장비 → 자유칸. slotIndex가 점유 칸이면 그 칸과 교체, 범위 밖이면 빈 칸에 추가.</summary>
    public static bool EquipGearAt(OwnedHero hero, EquipmentDefinition item, int slotIndex)
    {
        if (hero == null || item == null || item is WeaponDefinition) return false;
        if (!Armory.Items.Contains(item)) return false;

        if (slotIndex >= 0 && slotIndex < hero.equipment.Count)
        {
            // 점유 칸과 교체
            Armory.Items.Remove(item);
            Armory.Items.Add(hero.equipment[slotIndex]);
            hero.equipment[slotIndex] = item;
            return true;
        }

        if (hero.equipment.Count >= HeroRunInstance.MaxEquipSlots) return false; // 가득 — 교체 칸을 지정해야 함
        Armory.Items.Remove(item);
        hero.equipment.Add(item);
        return true;
    }

    /// <summary>무기칸 → 보관소 (미장착 허용 — 기본 공격 불가 상태)</summary>
    public static bool UnequipWeapon(OwnedHero hero)
    {
        if (hero == null || hero.weapon == null) return false;
        Armory.Items.Add(hero.weapon);
        hero.weapon = null;
        return true;
    }

    /// <summary>자유칸 → 보관소</summary>
    public static bool UnequipGear(OwnedHero hero, int slotIndex)
    {
        if (hero == null || slotIndex < 0 || slotIndex >= hero.equipment.Count) return false;
        Armory.Items.Add(hero.equipment[slotIndex]);
        hero.equipment.RemoveAt(slotIndex);
        return true;
    }
}
