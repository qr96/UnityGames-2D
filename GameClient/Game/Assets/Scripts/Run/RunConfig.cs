using UnityEngine;

/// <summary>런 규칙 설정. 미정 수치는 전부 여기서 튜닝.</summary>
[CreateAssetMenu(menuName = "Game/Run Config", fileName = "RunConfig")]
public class RunConfig : ScriptableObject
{
    [Header("전투 보상 / 포션 (수치 미정 → 튜닝값)")]
    public int potionsPerBattle = 3;

    [Header("적 기본 스탯 (1층 일반 기준 — 층 배수로 스케일)")]
    public float enemyBaseHP = 35f;
    public float enemyBaseDamage = 5f;
    public float enemyAttackRange = 1f;
    public float enemyAttackInterval = 1.2f;
    public float enemyMoveSpeed = 1.4f;

    [Header("적 스케일링 — 층(RewardLevel) 기반 (던전 명세: 깊이 = 난이도)")]
    [Tooltip("1층 일반 전투의 총 마릿수")]
    public int baseEnemyCount = 5;
    [Tooltip("층당 마릿수 증가 (소수 허용 — 합산 후 반올림)")]
    public float enemyCountPerFloor = 0.5f;
    [Tooltip("마릿수 상한 (전투장 밀도 보호)")]
    public int maxEnemyCount = 16;
    [Tooltip("층당 적 스탯 증가 (0.15 = +15%/층)")]
    public float enemyStatGrowthPerFloor = 0.15f;

    [Header("전투 종류 배수")]
    [Tooltip("엘리트 전투 — 스탯 배수 / 추가 마릿수")]
    public float eliteStatMultiplier = 1.5f;
    public int eliteExtraEnemies = 2;
    [Tooltip("랜드마크(최심부 보스) 적 스탯 배수 — 엘리트 배수와 중첩")]
    public float landmarkStatMultiplier = 1.6f;

    [Header("적 스폰 — 전투마다 총 마릿수를 조금씩 랜덤 스폰")]
    public float firstSpawnDelay = 0.8f;
    public float spawnInterval = 2.5f;
    public int spawnBatchMin = 1;
    public int spawnBatchMax = 2;
    public float spawnWarnTime = 0.5f;    // 스폰 예고 시간 (0이면 즉시 등장)

    [Header("적 스폰 위치 — 전투장 중심 기준 상대 오프셋, 영웅 주변 제외")]
    public Vector2 spawnAreaMin = new Vector2(-4f, -6f);
    public Vector2 spawnAreaMax = new Vector2(4f, 6.5f);
    public float minSpawnDistanceFromHeroes = 2.5f;
}