using UnityEngine;

/// <summary>스킬 실행 로직 종류 — SkillRunner.Execute가 이 값으로 분기 (영웅 추가 시 확장)</summary>
public enum SkillKind
{
    IronWall,   // 철벽: 자신에게 받는 피해 감소 버프
    SpinSlash,  // 회전참: 자기 중심 광역 피해
    PierceShot, // 관통사격: 직선 관통 투사체
    Sanctuary,  // 성역: 자기 중심 회복 지속 영역
}

/// <summary>발동 조건 형태 (확정 규칙 — 쿨타임이 돌면 조건 충족 시 자동 발동)</summary>
public enum SkillTrigger
{
    TargetedAttack,     // 공격형: 현재 공격 대상이 스킬 사거리 안
    SelfCenteredAttack, // 자기중심 공격형: 공격 대상이 스킬 범위 안
    HealAlly,           // 회복형: 범위 내 HP 감소한 아군 1명 이상
    BuffAlly,           // 버프형: 범위 내 자신 이외 아군 1명 이상
    WhileEngaged,       // 소환형/자기강화형: 적을 공격 중이면
}

/// <summary>
/// 액티브 스킬 정의 (수치 데이터). 실행 로직은 SkillRunner가 kind로 분기.
/// 스킬 종류에 따라 사용하는 수치 항목이 다름 — 안 쓰는 항목은 0으로 둠.
/// </summary>
[CreateAssetMenu(menuName = "Game/Skill Definition", fileName = "Skill_")]
public class SkillDefinition : ScriptableObject
{
    [Header("신원")]
    public string id;
    public string displayName;

    [Header("동작")]
    public SkillKind kind;
    public SkillTrigger trigger;
    public float cooldown = 10f;

    [Header("수치 (종류별 사용 항목이 다름)")]
    [Tooltip("공격형 사거리 / 관통 길이")]
    public float range;
    [Tooltip("자기중심·존 반경 / 관통 폭")]
    public float radius;
    [Tooltip("버프·존 지속 시간")]
    public float duration;
    [Tooltip("존 효과 주기")]
    public float tickInterval = 1f;
    [Tooltip("공격력 대비 피해 %")]
    public float damagePercent;
    [Tooltip("범용 효과 수치 — 피해감소 % / 최대HP 회복 % 등")]
    public float effectValue;
}
