using UnityEngine;

/// <summary>고유 효과 8종 (장비 명세 v1.2 §8) — 특별 장비에 1개. 동작은 UniqueEffectRunner(배치 ②)에서.</summary>
public enum UniqueEffect
{
    None,
    LastStand,       // HP ≤ 20% → 주는 피해 +30%
    HealthyFury,     // HP ≥ 80% → 주는 피해 +20%
    Execution,       // 대상 HP ≤ 30% → 해당 대상 피해 +25%
    Bloodthirst,     // 적 처치 → 최대 HP 5% 회복
    DropFury,        // 내려놓음 → 3초간 주는 피해 +25% (재발동 시 갱신)
    DropGuard,       // 내려놓음 → 3초간 받는 피해 감소 25% (TotalDR 합산)
    ActiveStrike,    // 액티브 발동 → 다음 기본공격 +50% (재충전형)
    EmergencyShield, // HP 최초 25% 이하 진입 → 최대 HP 20% 보호막, 전투당 1회
}

/// <summary>
/// 장비 정의 (장비 명세 v1.2).
/// 절차 생성: 드랍 시 EquipmentGenerator가 런타임 인스턴스를 생성 (깡스탯 1 + 공통 옵션 1,
/// 특별 장비는 + 고유효과 1). 인벤토리/슬롯은 인스턴스를 직접 참조 — 생성 장비는 전부 개별 개체.
/// (에셋으로 만든 고정 장비도 여전히 지원 — 개발용)
/// </summary>
[CreateAssetMenu(menuName = "Game/Equipment Definition", fileName = "Equip_")]
public class EquipmentDefinition : ScriptableObject
{
    public string id;
    public string displayName;
    public StatModifier[] modifiers;

    [Header("특별 장비 (장비 명세 v1.2)")]
    public bool isSpecial;
    public UniqueEffect uniqueEffect = UniqueEffect.None;
}