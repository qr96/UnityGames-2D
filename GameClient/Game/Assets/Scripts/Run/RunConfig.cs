using UnityEngine;

/// <summary>런 규칙 설정. GDD의 미정 수치는 전부 여기서 튜닝.</summary>
[CreateAssetMenu(menuName = "Game/Run Config", fileName = "RunConfig")]
public class RunConfig : ScriptableObject
{
    [Header("영입 (GDD 6 — ※ 시스템 보류 중, DevBootstrap에서 0으로 꺼둠)")]
    public int recruitChances = 2;
    public int[] recruitAfterBattle = { 1, 2 }; // 몇 번째 전투 '승리 후' 영입 이벤트 발생
    public int candidatesPerRecruit = 2;

    [Header("월드")]
    [Tooltip("랜드마크(보스 지역) 적 스탯 배수")]
    public float landmarkStatMultiplier = 1.6f;

    [Header("전투 보상 / 포션 (GDD 4·8, 수치 미정 → 튜닝값)")]
    public int potionsPerBattle = 3;
    public int equipmentDropsPerBattle = 2;

    [Header("적 스폰 — 전투마다 총 마릿수를 조금씩 랜덤 스폰")]
    public int baseEnemyCount = 8;        // 첫 전투의 총 마릿수
    public int enemyCountGrowth = 4;      // 전투 횟수당 총 마릿수 증가
    public float enemyStatGrowth = 0.25f; // 전투 횟수당 적 스탯 +25%
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