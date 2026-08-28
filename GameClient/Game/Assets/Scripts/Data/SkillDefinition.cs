using UnityEngine;

/// <summary>액티브 스킬 종류 (액티브 스펙 v2 — 10종 풀). 기존 16종은 폐기.</summary>
public enum SkillKind
{
    PowerStrike, // 강타: 현재 공격 대상을 강하게 공격 (자동/근접)
    Sweep,       // 휩쓸기: 현재 대상 방향으로 범위 공격 (자동/근접)
    Snipe,       // 저격: 현재 대상을 강하게 원거리 공격 (자동/활)
    Fireball,    // 화염구: 현재 대상과 주변 적에게 피해 (자동/마법 도구)
    Execute,     // 처형: 현재 대상 HP가 낮을 때 강력한 공격 (자동/근접)
    Heal,        // 회복: 일정 HP 이하의 아군을 회복 (자동/마법 도구)
    BattleCry,   // 전투 함성: 주변 아군을 일정 시간 강화 (내려놓기/조건 없음)
    Shockwave,   // 충격파: 주변 적에게 피해 + 밀쳐냄 (내려놓기/조건 없음)
    Barrier,     // 보호막: 주변 아군에게 일정량의 보호막 부여 (내려놓기/조건 없음)
    FirstAid,    // 응급 치료: 주변에서 가장 HP가 낮은 아군 회복 (내려놓기/조건 없음)
}

/// <summary>발동 방식 (확정 규칙)</summary>
public enum SkillActivation
{
    Auto,      // 쿨타임이 돌면 종류별 조건 충족 시 즉시 자동 발동
    OnRelease, // 쿨 준비 + 무기 조건 충족 상태에서 영웅을 내려놓는 순간 발동.
               // 쿨 중이면 그냥 재배치. 쿨이 다 차도 내려놓기 전까지 대기.
}

/// <summary>
/// 액티브 스킬 정의 (액티브 스펙 v2).
///   · 영웅 생성 시 풀에서 랜덤 1개 배정, 영구 고정 (OwnedHero.activeSkill)
///   · 스킬 레벨/성장 없음 — 모든 수치는 고정값. 피해형은 영웅 공격력 성장으로 자연히 강해짐
///   · 무기 조건 불충족 시 액티브만 비활성 (기본 공격은 정상)
/// 종류별 수치 항목 사용처:
///   강타: range, damagePercent
///   휩쓸기: radius(전방 부채꼴 반경), damagePercent
///   저격: range, damagePercent (투사체)
///   화염구: range(발사 사거리), radius(폭발 반경), damagePercent
///   처형: range, damagePercent, effectValue(대상 HP 임계 %)
///   회복: radius, effectValue(아군 HP 임계 %), damagePercent(회복량 — 시전자 공격력 %)
///   전투 함성: radius, duration, effectValue(공격력 +%), effectValue2(공속 +%)
///   충격파: radius, damagePercent, effectValue(밀침 거리)
///   보호막: radius, duration, effectValue(보호막량 — 시전자 공격력 %)
///   응급 치료: radius, damagePercent(회복량 — 시전자 공격력 %)
/// </summary>
[CreateAssetMenu(menuName = "Game/Skill Definition", fileName = "Skill_")]
public class SkillDefinition : ScriptableObject
{
    [Header("신원")]
    public string id;
    public string displayName;

    [Header("동작")]
    public SkillKind kind;
    public SkillActivation activation = SkillActivation.Auto;
    public WeaponRequirement weaponRequirement = WeaponRequirement.None;
    public float cooldown = 10f;

    [Header("수치 (종류별 사용 항목이 다름 — 클래스 주석 참고)")]
    public float range;
    public float radius;
    public float duration;
    public float tickInterval = 1f;
    [Tooltip("공격력 대비 피해/회복 %")]
    public float damagePercent;
    public float effectValue;
    public float effectValue2;
}