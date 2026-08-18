using UnityEngine;

/// <summary>런 규칙 설정. GDD의 미정 수치는 전부 여기서 튜닝.</summary>
[CreateAssetMenu(menuName = "Game/Run Config", fileName = "RunConfig")]
public class RunConfig : ScriptableObject
{
    [Header("런 구성 (GDD 6)")]
    public int battlesPerRun = 3;
    public int recruitChances = 2;               // ※ 영입 시스템 보류 중 — DevBootstrap에서 0으로 꺼둠
    public int[] recruitAfterBattle = { 1, 2 };
    public int candidatesPerRecruit = 2;

    [Header("전투 보상 / 포션 (GDD 4·8, 수치 미정 → 튜닝값)")]
    public int potionsPerBattle = 3;
    public int equipmentDropsPerBattle = 2;

    [Header("적 스폰 — 스테이지별 총 마릿수를 조금씩 랜덤 스폰")]
    public int baseEnemyCount = 8;        // 1번째 전투의 총 마릿수
    public int enemyCountGrowth = 4;      // 전투당 총 마릿수 증가
    public float enemyStatGrowth = 0.25f; // 전투당 적 스탯 +25%
    public float firstSpawnDelay = 0.8f;  // 전투 시작 후 첫 스폰까지
    public float spawnInterval = 2.5f;    // 스폰 주기
    public int spawnBatchMin = 1;         // 주기당 스폰 마릿수 (최소)
    public int spawnBatchMax = 2;         // 주기당 스폰 마릿수 (최대)
    public float spawnWarnTime = 0.5f;    // 스폰 예고 시간 (0이면 즉시 등장)

    [Header("적 스폰 위치 — 영웅 주변을 제외한 전장 아무 위치")]
    public Vector2 spawnAreaMin = new Vector2(-4f, -6f);
    public Vector2 spawnAreaMax = new Vector2(4f, 6.5f);
    public float minSpawnDistanceFromHeroes = 2.5f;
}