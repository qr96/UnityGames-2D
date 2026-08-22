using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이동 연출 (GDD 11): 영웅들이 실제 길을 따라 목적지로 걸어가는 모습을 보여줌.
/// - 영웅별 출발 시차 + 걷는 통통거림으로 '여행하는 파티' 느낌
/// - 도착 지점 = 목적지의 파티 대형 자리 → 도착 직후 재배치와 좌표가 일치해 이음새 없음
/// - 이미 방문한 장소로의 반복 이동은 가속 (GDD 11: 반복 이동 짧게)
/// - 길 위에서는 조작/이벤트 없음 (GDD 5) — 입력은 페이즈 가드로 차단됨
/// ※ 연출 시간/스킵 방식은 미확정 → 전부 튜닝값. 완료 시 RunManager.CompleteTravel() 호출.
/// </summary>
public class TravelController : MonoBehaviour
{
    [Header("이동 연출 튜닝 (플레이테스트 후 확정)")]
    public float travelSpeed = 5.5f;            // 초당 이동 거리
    public float revisitSpeedMultiplier = 2.5f; // 방문했던 장소로의 반복 이동 가속
    public float departStagger = 0.12f;         // 영웅별 출발 시차 (초)
    public float bobHeight = 0.12f;             // 걷기 통통거림 높이
    public float bobFrequency = 7f;

    Coroutine routine;

    public void BeginTravel(RunState run, LocationDefinition from, LocationDefinition to, bool destinationVisited)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(TravelRoutine(to, destinationVisited));
    }

    IEnumerator TravelRoutine(LocationDefinition to, bool destinationVisited)
    {
        // 현재 전장에 서 있는 영웅 유닛 수집
        var heroes = new List<Hero>();
        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
            if (u is Hero h) heroes.Add(h);

        float speed = travelSpeed * (destinationVisited ? revisitSpeedMultiplier : 1f);

        var destCenter = new Vector3(to.worldPosition.x, to.worldPosition.y, 0f);
        Vector3[] slots = BattleController.DefaultHeroSlots(heroes.Count);

        var starts = new Vector3[heroes.Count];
        var ends = new Vector3[heroes.Count];
        float maxDist = 0f;
        for (int i = 0; i < heroes.Count; i++)
        {
            starts[i] = heroes[i].transform.position;
            ends[i] = destCenter + slots[i];
            maxDist = Mathf.Max(maxDist, Vector3.Distance(starts[i], ends[i]));
        }

        float duration = Mathf.Max(0.4f, maxDist / Mathf.Max(0.1f, speed));
        float total = duration + Mathf.Max(0, heroes.Count - 1) * departStagger;
        float elapsed = 0f;

        while (elapsed < total)
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < heroes.Count; i++)
            {
                if (heroes[i] == null) continue;

                float k = Mathf.Clamp01((elapsed - i * departStagger) / duration);
                Vector3 pos = Vector3.Lerp(starts[i], ends[i], k);

                // 이동 중에만 걷는 통통거림
                if (k > 0f && k < 1f)
                    pos.y += Mathf.Abs(Mathf.Sin((Time.time + i * 0.37f) * bobFrequency)) * bobHeight;

                heroes[i].transform.position = pos;
            }
            yield return null;
        }

        // 도착 지점 정렬
        for (int i = 0; i < heroes.Count; i++)
            if (heroes[i] != null) heroes[i].transform.position = ends[i];

        routine = null;
        RunManager.Instance.CompleteTravel();
    }
}
