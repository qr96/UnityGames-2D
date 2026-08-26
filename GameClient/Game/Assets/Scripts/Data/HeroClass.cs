/// <summary>영웅 클래스 계열 (기획 분류: WARRIOR/ROGUE/RANGER/MAGE/SUPPORT)</summary>
public enum HeroClass
{
    Warrior, // 전사 계열
    Rogue,   // 도적 계열
    Ranger,  // 사격 계열
    Mage,    // 주문 계열
    Support, // 지원 계열
}

/// <summary>기본 공격 타입</summary>
public enum AttackType
{
    Melee,  // 근접
    Ranged, // 원거리 (투사체)
}

public static class HeroClassUtil
{
    public static string Korean(HeroClass c)
    {
        switch (c)
        {
            case HeroClass.Warrior: return "전사";
            case HeroClass.Rogue:   return "도적";
            case HeroClass.Ranger:  return "사격";
            case HeroClass.Mage:    return "주문";
            case HeroClass.Support: return "지원";
            default: return "?";
        }
    }
}
