using UnityEngine;

/// <summary>
/// 무기 정의 (무기 스펙 v2). EquipmentDefinition 서브클래스라 인벤토리/드랍 흐름을 그대로 공유.
///   · 전용 무기 슬롯 1개에만 장착 (자유 슬롯 3칸에는 장착 불가 — RunState가 라우팅)
///   · 무기가 기본 공격의 사거리/공격주기/공격방식(단일·소범위·투사체)을 전부 결정
///   · 타입 고정 공격력 보정 없음 — 피해 원천은 영웅 공격력.
///     개별 무기의 랜덤 옵션(공격력 +8% 등)은 상속받은 modifiers로 부여 (장비 개편에서)
///   · 무기 미장착 = 기본 공격 불가 (무기 조건 없는 액티브는 사용 가능)
/// </summary>
[CreateAssetMenu(menuName = "Game/Weapon Definition", fileName = "Weapon_")]
public class WeaponDefinition : EquipmentDefinition
{
    [Header("무기 타입 (기본 공격 결정)")]
    public WeaponType weaponType = WeaponType.Sword;
    public AttackType attackType = AttackType.Melee;
    public float attackRange = 1.5f;
    public float attackInterval = 1f;

    [Tooltip("근접 소범위 반경 (대검) — 대상 주변 이 반경의 적도 함께 타격. 0 = 단일")]
    public float aoeRadius;

    [Header("투사체 (attackType = Ranged일 때)")]
    public float projectileSpeed = 9f;
}
