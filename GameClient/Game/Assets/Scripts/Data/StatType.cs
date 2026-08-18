using System;

/// <summary>
/// 영웅 공통 스탯 종류.
/// GDD 8: 장비는 기본적으로 영웅의 공통 스탯을 변경하며,
/// 각 기본 공격/고유 스킬은 어떤 공통 스탯을 참조하는지 개별 정의한다.
/// </summary>
public enum StatType
{
    MaxHP,
    Attack,
    AttackRange,
    AttackInterval,
    MoveSpeed,
    HealPower,
    HealRange,
}

/// <summary>장비 하나가 주는 스탯 수정치. 최종치 = (기본 + Σflat) × (1 + Σpercent/100)</summary>
[Serializable]
public struct StatModifier
{
    public StatType stat;
    public float flat;     // 고정 수치 증감
    public float percent;  // 퍼센트 증감 (10 = +10%)
}
