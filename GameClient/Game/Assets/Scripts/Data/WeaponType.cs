/// <summary>무기 타입 (무기 스펙 v2) — 타입이 사거리/공격주기/공격방식을 결정. 타입 고정 공격력 보정 없음.</summary>
public enum WeaponType
{
    Dagger,    // 단검: 1.2 / 0.65초 / 근접 단일
    Sword,     // 검:   1.5 / 1.0초  / 근접 단일
    Greatsword,// 대검: 1.8 / 1.5초  / 근접 소범위
    Bow,       // 활:   6.0 / 1.2초  / 단일 투사체
    MagicTool, // 마법 도구: 4.5 / 1.4초 / 단일 투사체
}

/// <summary>액티브 스킬의 무기 조건. 불충족 시 액티브만 비활성 — 기본 공격은 현재 무기로 정상 수행.</summary>
public enum WeaponRequirement
{
    None,      // 조건 없음 (무기 미장착이어도 사용 가능)
    Melee,     // 근접 (단검/검/대검)
    Bow,       // 활
    MagicTool, // 마법 도구
}

public static class WeaponRules
{
    public static bool IsMelee(WeaponType t) =>
        t == WeaponType.Dagger || t == WeaponType.Sword || t == WeaponType.Greatsword;

    /// <summary>장착 무기가 스킬의 무기 조건을 충족하는가 (None은 맨손 포함 항상 충족)</summary>
    public static bool Meets(WeaponDefinition weapon, WeaponRequirement req)
    {
        if (req == WeaponRequirement.None) return true;
        if (weapon == null) return false;
        switch (req)
        {
            case WeaponRequirement.Melee: return IsMelee(weapon.weaponType);
            case WeaponRequirement.Bow: return weapon.weaponType == WeaponType.Bow;
            case WeaponRequirement.MagicTool: return weapon.weaponType == WeaponType.MagicTool;
            default: return false;
        }
    }

    public static string Korean(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.Dagger: return "단검";
            case WeaponType.Sword: return "검";
            case WeaponType.Greatsword: return "대검";
            case WeaponType.Bow: return "활";
            case WeaponType.MagicTool: return "마법 도구";
            default: return "?";
        }
    }
}
