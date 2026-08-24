using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 1회: 배치(SetupBattle) → 교전(BeginCombat) → 승패 보고.
/// 전투장은 현재 장소의 월드 좌표 위에 열림 (GDD 9·10 — 카메라 줌 연속 연출).
///
/// - 배치 단계: 영웅만 스폰, CombatActive=false → 모든 유닛 AI 정지 (잡기/배치는 가능)
/// - 교전: 총 마릿수를 spawnInterval마다 예고 마커 후, '영웅 주변 제외 전투장 랜덤 위치'에 스폰
/// - 승리: 스폰 물량 소진 + 살아있는 적 없음 / 패배: 파티 전멸
/// - 탐험 중에는 파티가 현재 장소에 서 있음 (ShowPartyAt — 전투 종료 위치 보존)
/// </summary>
public class BattleController : MonoBehaviour
{
    public ConsumableBar consumableBar; // 부트스트랩/인스펙터에서 주입

    /// <summary>교전 진행 중인가. false면 영웅/적 AI 정지 (배치·탐험 포함).</summary>
    public static bool CombatActive { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => CombatActive = false;

    readonly List<GameObject> spawned = new List<GameObject>();

    RunConfig config;
    Vector3 arenaCenter;              // 전투장 중심 = 현재 장소의 월드 좌표
    LocationDefinition fieldLocation; // 현재 전장/파티가 서 있는 장소
    int remainingToSpawn;             // 아직 스폰 예약되지 않은 적 수
    int pendingSpawns;                // 예고 중(마커 표시, 아직 등장 전)인 적 수
    float statMultiplier;
    bool judging;                     // 승패 판정 활성 여부
    Coroutine spawnRoutine;

    // ---------- 배치 단계 ----------

    /// <summary>영웅만 스폰하고 AI 정지 상태로 대기. RunManager가 Placement 진입 시 호출.</summary>
    public void SetupBattle(RunState run, RunConfig config, LocationDefinition location)
    {
        this.config = config;
        CombatActive = false;
        judging = false;
        pendingSpawns = 0;
        StopSpawning();
        ClearField();

        fieldLocation = location;
        arenaCenter = LocationCenter(location);

        // 파티 스폰: 사망했던 영웅도 다음 전투에는 정상 참여 (GDD 4: 영구 사망 아님)
        SpawnParty(run);

        // 이번 전투의 스폰 물량/강도 (전투 횟수 기반 스케일링)
        int battleIdx = Mathf.Max(0, run.battleNumber - 1);
        remainingToSpawn = config.baseEnemyCount + battleIdx * config.enemyCountGrowth;
        statMultiplier = 1f + battleIdx * config.enemyStatGrowth;
        if (location != null && location.type == LocationType.Landmark)
            statMultiplier *= config.landmarkStatMultiplier; // 보스 지역 강화

        // GDD 4: 포션은 전투당 N개 지급 (소모품 바에 오른쪽부터 표시)
        if (consumableBar != null) consumableBar.SetPotions(config.potionsPerBattle);
    }

    /// <summary>
    /// 탐험 진입 시 파티를 현재 장소에 표시 (RunManager가 호출).
    /// 방금 이 장소에서 전투를 끝냈다면 그 자리 그대로 유지 — 전투 종료 위치 보존.
    /// 다른 장소(전투 없는 마을 등)에 도착했다면 그 장소 중앙에 파티를 배치.
    /// </summary>
    public void ShowPartyAt(RunState run, LocationDefinition location)
    {
        if (fieldLocation == location && UnitRegistry.AnyAlive(Team.Hero))
            return; // 전투가 끝난 위치 그대로 유지

        ClearField();
        fieldLocation = location;
        arenaCenter = LocationCenter(location);
        SpawnParty(run);
    }

    void SpawnParty(RunState run)
    {
        // 사망자는 스폰하지 않음 — 부활은 교회에서만 (확정 규칙)
        var living = new List<HeroRunInstance>();
        foreach (var inst in run.party)
            if (!inst.isDead) living.Add(inst);

        Vector3[] slots = DefaultHeroSlots(living.Count);
        for (int i = 0; i < living.Count; i++)
        {
            HeroRunInstance inst = living[i];
            Hero hero = UnitFactory.SpawnHero(inst, arenaCenter + slots[i]);
            hero.OnDeath += _ =>
            {
                inst.diedThisRun = true; // 해금 조건 추적 (GDD 7)
                inst.isDead = true;      // 교회에서 부활할 때까지 사망 유지
                inst.currentHP = 0f;
            };
            spawned.Add(hero.gameObject);
        }
    }

    static Vector3 LocationCenter(LocationDefinition location)
    {
        return location != null
            ? new Vector3(location.worldPosition.x, location.worldPosition.y, 0f)
            : Vector3.zero;
    }

    /// <summary>
    /// 야영지 휴식 — 전장의 생존 파티를 완전 회복 + 짧은 회복 연출 (야영지 기획 4).
    /// 사망자는 전장에 없으므로 자연히 제외 (부활은 교회 담당).
    /// </summary>
    public void HealPartyAtCamp()
    {
        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
        {
            u.Heal(u.MaxHP); // Heal이 최대치로 클램프 — 이미 최대면 효과 없음

            var flash = new GameObject("CampHealFlash");
            UnitFactory.MakeVisual(flash.transform, UnitFactory.Circle,
                new Color(0.5f, 1f, 0.6f, 0.35f), 1.6f, sortingOrder: 4);
            flash.transform.position = u.transform.position;
            Destroy(flash, 0.5f);
        }
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
                range: 1f, interval: 1.2f, speed: 1.4f);
            spawned.Add(e.gameObject);
        });
    }

    /// <summary>영웅 주변(minSpawnDistanceFromHeroes)을 제외한 전투장 랜덤 위치.</summary>
    Vector3 FindSpawnPosition()
    {
        List<Unit> heroes = UnitRegistry.GetAll(Team.Hero);

        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector3 p = arenaCenter + new Vector3(
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

        // 영웅들이 전투장을 넓게 덮어 유효 위치를 못 찾으면 상단 가장자리로 폴백
        return arenaCenter + new Vector3(
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

    /// <summary>전장 정리 (런 시작 시 RunManager가 호출)</summary>
    public void ClearField()
    {
        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();
        fieldLocation = null;
    }

    public static Vector3[] DefaultHeroSlots(int count)
    {
        // 배치 단계의 초기 위치일 뿐 — 플레이어가 자유롭게 재배치.
        // 하단 UI(장비 바 등)와 겹치지 않도록 전투장 중하단에 배치.
        var slots = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0.5f : (float)i / (count - 1);
            slots[i] = new Vector3(Mathf.Lerp(-2.4f, 2.4f, t), -1.6f - Mathf.Abs(t - 0.5f) * 1.2f, 0f);
        }
        return slots;
    }
}