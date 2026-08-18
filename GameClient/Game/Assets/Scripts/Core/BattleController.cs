using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 1회: 배치(SetupBattle) → 교전(BeginCombat) → 승패 보고.
///
/// - 배치 단계: 영웅만 스폰, CombatActive=false → 모든 유닛 AI 정지 (잡기/배치는 가능)
/// - 교전: 스테이지 총 마릿수를 spawnInterval마다 1~2마리씩,
///          '영웅 주변을 제외한 전장 아무 위치'에 랜덤 스폰
/// - 승리: 스폰 예정 물량 소진 + 살아있는 적 없음 / 패배: 파티 전멸
/// </summary>
public class BattleController : MonoBehaviour
{
    public PotionButton potionButton; // 부트스트랩/인스펙터에서 주입

    /// <summary>교전 진행 중인가. false면 영웅/적 AI 정지 (배치 단계 포함).</summary>
    public static bool CombatActive { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => CombatActive = false;

    readonly List<GameObject> spawned = new List<GameObject>();

    RunConfig config;
    int remainingToSpawn;   // 아직 스폰 예약되지 않은 적 수
    int pendingSpawns;      // 예고 중(마커 표시, 아직 등장 전)인 적 수
    float statMultiplier;
    bool judging;           // 승패 판정 활성 여부
    Coroutine spawnRoutine;

    // ---------- 배치 단계 ----------

    /// <summary>영웅만 스폰하고 AI 정지 상태로 대기. RunManager가 Placement 진입 시 호출.</summary>
    public void SetupBattle(RunState run, RunConfig config)
    {
        this.config = config;
        CombatActive = false;
        judging = false;
        pendingSpawns = 0;
        StopSpawning();
        ClearField();

        // 파티 스폰: 사망했던 영웅도 다음 전투에는 정상 참여 (GDD 4: 영구 사망 아님)
        Vector3[] slots = DefaultHeroSlots(run.party.Count);
        for (int i = 0; i < run.party.Count; i++)
        {
            HeroRunInstance inst = run.party[i];
            Hero hero = UnitFactory.SpawnHero(inst, slots[i]);
            hero.OnDeath += _ => inst.diedThisRun = true; // 해금 조건 추적 (GDD 7)
            spawned.Add(hero.gameObject);
        }

        // 이번 스테이지 스폰 물량/강도
        int battleIdx = run.battleNumber - 1;
        remainingToSpawn = config.baseEnemyCount + battleIdx * config.enemyCountGrowth;
        statMultiplier = 1f + battleIdx * config.enemyStatGrowth;

        // GDD 4: 포션은 전투당 N개 지급
        if (potionButton != null) potionButton.SetCount(config.potionsPerBattle);
    }

    // ---------- 교전 ----------

    /// <summary>'전투 시작' 버튼 → RunManager가 호출.</summary>
    public void BeginCombat()
    {
        CombatActive = true;
        judging = true;
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(config.firstSpawnDelay);

        while (remainingToSpawn > 0 && CombatActive)
        {
            int batch = Mathf.Min(remainingToSpawn,
                Random.Range(config.spawnBatchMin, config.spawnBatchMax + 1));

            for (int i = 0; i < batch; i++)
            {
                ScheduleEnemySpawn();
                remainingToSpawn--;
            }
            yield return new WaitForSeconds(config.spawnInterval);
        }
    }

    /// <summary>예고 마커를 먼저 깔고, warnTime 후 실제 적을 등장시킴.</summary>
    void ScheduleEnemySpawn()
    {
        Vector3 pos = FindSpawnPosition();
        pendingSpawns++;

        SpawnMarker marker = UnitFactory.CreateSpawnMarker(pos);
        spawned.Add(marker.gameObject); // 전장 정리 대상에 포함

        marker.Play(config.spawnWarnTime, () =>
        {
            pendingSpawns--;
            if (!CombatActive) return; // 예고 도중 교전이 끝났으면 등장 취소

            Enemy e = UnitFactory.SpawnEnemy("Slime", pos,
                maxHP: 60f * statMultiplier,
                dmg: 8f * statMultiplier,
                range: 1f, interval: 1.2f, speed: 1.8f);
            spawned.Add(e.gameObject);
        });
    }

    /// <summary>영웅 주변(minSpawnDistanceFromHeroes)을 제외한 전장 랜덤 위치.</summary>
    Vector3 FindSpawnPosition()
    {
        List<Unit> heroes = UnitRegistry.GetAll(Team.Hero);

        for (int attempt = 0; attempt < 30; attempt++)
        {
            var p = new Vector3(
                Random.Range(config.spawnAreaMin.x, config.spawnAreaMax.x),
                Random.Range(config.spawnAreaMin.y, config.spawnAreaMax.y), 0f);

            bool clear = true;
            foreach (Unit h in heroes)
            {
                if (Vector2.Distance(h.transform.position, p) < config.minSpawnDistanceFromHeroes)
                {
                    clear = false;
                    break;
                }
            }
            if (clear) return p;
        }

        // 영웅들이 전장을 넓게 덮어 유효 위치를 못 찾으면 상단 가장자리로 폴백
        return new Vector3(
            Random.Range(config.spawnAreaMin.x, config.spawnAreaMax.x),
            config.spawnAreaMax.y, 0f);
    }

    // ---------- 승패 판정 ----------

    void Update()
    {
        if (!judging || !CombatActive) return;

        // 예고 중인 적(pendingSpawns)도 아직 남은 적으로 취급
        if (remainingToSpawn <= 0 && pendingSpawns <= 0 && !UnitRegistry.AnyAlive(Team.Enemy))
        {
            EndCombat();
            RunManager.Instance.ReportBattleWon();
        }
        else if (!UnitRegistry.AnyAlive(Team.Hero))
        {
            EndCombat();
            RunManager.Instance.ReportBattleLost();
        }
    }

    void EndCombat()
    {
        judging = false;
        CombatActive = false;
        StopSpawning();
    }

    void StopSpawning()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    void ClearField()
    {
        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();
    }

    static Vector3[] DefaultHeroSlots(int count)
    {
        // 배치 단계의 초기 위치일 뿐 — 플레이어가 자유롭게 재배치
        var slots = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0.5f : (float)i / (count - 1);
            slots[i] = new Vector3(Mathf.Lerp(-2.4f, 2.4f, t), -3.2f - Mathf.Abs(t - 0.5f) * 1.6f, 0f);
        }
        return slots;
    }
}