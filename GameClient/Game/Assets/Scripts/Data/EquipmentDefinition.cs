using UnityEngine;

/// <summary>
/// 장비 정의.
/// GDD 8: 카테고리 제한 없음(어떤 슬롯에나), 공통 스탯을 변경.
/// 개별 인스턴스 상태(강화 등)가 아직 없으므로 정의 자체를 인벤토리/슬롯에서 직접 참조한다.
/// (같은 정의를 여러 슬롯에 넣는 것 = 동일 장비 중첩)
/// </summary>
[CreateAssetMenu(menuName = "Game/Equipment Definition", fileName = "Equip_")]
public class EquipmentDefinition : ScriptableObject
{
    public string id;
    public string displayName;
    public StatModifier[] modifiers;
}
