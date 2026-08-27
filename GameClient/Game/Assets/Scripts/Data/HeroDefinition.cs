using UnityEngine;

/// <summary>
/// 영웅의 정적 정의 (신원 + 기본 스탯). HeroData를 대체.
/// 런/전투 상태는 HeroRunInstance가 들고, 이 에셋은 절대 변하지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Hero Definition", fileName = "Hero_")]
public class HeroDefinition : ScriptableObject
{
    [Header("신원")]
    public string id;                 // 저장/조회용 고유 키 (예: "bram")
    public string displayName;
    public bool unlockedByDefault;    // 최초 보유 영웅 여부

    [Header("분류")]
    public HeroClass heroClass = HeroClass.Warrior;
    public AttackType attackType = AttackType.Melee;

    [Header("임시 비주얼 (프리팹 전환 전)")]
    public Color color = Color.white;
    public float size = 0.9f;

    [Header("공통 스탯 기본치")]
    public float maxHP = 100f;
    public float attack = 10f;
    public float attackRange = 1.2f;
    public float attackInterval = 1f;
    public float moveSpeed = 2.5f;

    [Header("기본 공격 (공격력 대비 %)")]
    public float basicAttackPercent = 100f;
    [Tooltip("기본 공격 독: 지속시간에 걸친 총 피해 % (0 = 없음). 비중첩 — 지속시간 갱신")]
    public float basicPoisonTotalPercent;
    public float basicPoisonDuration = 3f;

    [Header("액티브 스킬")]
    public SkillDefinition skill;

    [Header("장비 스탯 호환용 (현재 미사용 — 유물/장비가 참조 가능)")]
    public float healPower;
    public float healRange;

    [Header("원거리 투사체 (attackType = Ranged일 때)")]
    public float projectileSpeed = 9f;

    [Header("행동별 참조 스탯 정의 (GDD 8)")]
    public StatType basicAttackPowerStat = StatType.Attack;
    public StatType healPowerStat = StatType.HealPower;

    [Header("잡기 연출")]
    public float wiggleSpeed = 20f;
    public float wiggleAngle = 12f;
    public float landingDelay = 0.35f; // 내려놓은 뒤 AI 재개까지의 착지 딜레이

    public float GetBaseStat(StatType type)
    {
        switch (type)
        {
            case StatType.MaxHP: return maxHP;
            case StatType.Attack: return attack;
            case StatType.AttackRange: return attackRange;
            case StatType.AttackInterval: return attackInterval;
            case StatType.MoveSpeed: return moveSpeed;
            case StatType.HealPower: return healPower;
            case StatType.HealRange: return healRange;
            default: return 0f;
        }
    }
}