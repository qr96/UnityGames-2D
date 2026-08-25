using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵 이동 연출 (개편):
///  ① 카메라 고정 — 영웅들이 선택한 길 방향으로 걸어 나감 (지형 가장자리 너머까지)
///  ② 페이드아웃
///  ③ (암전 중) 카메라를 도착 맵으로 스냅 + 영웅들을 '들어온 길' 입구에 배치
///  ④ 페이드인 — 도착한 맵이 드러남
///  ⑤ 영웅들이 길에서 걸어 들어와 대형 위치에 자리 잡음 → 도착 처리
/// 완료 시 RunManager.CompleteTravel() 호출. 전 구간 튜닝값.
/// </summary>
public class TravelController : MonoBehaviour
{
    [Header("걷기")]
    public float walkSpeed = 5.5f;
    public float departStagger = 0.08f; // 영웅별 출발 시차
    public float bobHeight = 0.12f;
    public float bobFrequency = 8f;

    [Header("전환")]
    public float exitDistance = 7.5f;     // 출발지 중심 → 길 방향으로 나가는 거리
    public float entryDistance = 7.5f;    // 도착지 중심 → 길 쪽 입장 시작 거리
    public float fadeTime = 0.22f;
    public float snapCameraSize = 10.5f;  // 암전 중 도착지 프레이밍

    [Tooltip("비워두면 자동 탐색")]
    public CameraController cameraController;
    [Tooltip("비워두면 자동 탐색")]
    public WorldEnvironment worldEnvironment;

    Coroutine routine;

    public void BeginTravel(RunState run, LocationDefinition from, LocationDefinition to, bool destinationVisited)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(TravelRoutine(from, to));
    }

    IEnumerator TravelRoutine(LocationDefinition from, LocationDefinition to)
    {
        if (cameraController == null)
            cameraController = UnityEngine.Object.FindFirstObjectByType<CameraController>();
        if (worldEnvironment == null)
            worldEnvironment = UnityEngine.Object.FindFirstObjectByType<WorldEnvironment>();

        // 현재 전장에 서 있는 영웅 유닛 수집
        var heroes = new List<Hero>();
        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
            if (u is Hero h) heroes.Add(h);

        Vector3 fromCenter = new Vector3(from.worldPosition.x, from.worldPosition.y, 0f);
        Vector3 toCenter = new Vector3(to.worldPosition.x, to.worldPosition.y, 0f);
        Vector3 roadDir = (toCenter - fromCenter).normalized;
        Vector3[] slots = BattleController.DefaultHeroSlots(heroes.Count);

        // ── ① 퇴장: 길을 따라 걸어 나감 (카메라 고정)
        Vector3 exitPoint = fromCenter + roadDir * exitDistance;
        yield return Walk(heroes, i => exitPoint + slots[i] * 0.3f);

        // ── ② 페이드아웃
        yield return ScreenFader.Get().Fade(1f, fadeTime);

        // ── ③ 전환 (암전 중): 도착 맵으로 표시 교체 + 카메라 스냅 + 들어온 길 입구에 배치
        if (worldEnvironment != null)
            worldEnvironment.ShowOnly(to);
        if (cameraController != null)
            cameraController.SnapTo(to.worldPosition, snapCameraSize);

        Vector3 entryPoint = toCenter - roadDir * entryDistance;
        for (int i = 0; i < heroes.Count; i++)
            if (heroes[i] != null)
                heroes[i].transform.position = entryPoint + slots[i] * 0.3f;

        // ── ④ 페이드인: 도착한 맵이 드러남
        yield return ScreenFader.Get().Fade(0f, fadeTime);

        // ── ⑤ 입장: 길에서 나와 대형 위치로
        yield return Walk(heroes, i => toCenter + slots[i]);

        routine = null;
        RunManager.Instance.CompleteTravel();
    }

    /// <summary>영웅들이 각자 목표 지점까지 걷기 (출발 시차 + 통통거림)</summary>
    IEnumerator Walk(List<Hero> heroes, Func<int, Vector3> getTarget)
    {
        int count = heroes.Count;
        var starts = new Vector3[count];
        var targets = new Vector3[count];
        var durations = new float[count];
        float total = 0f;

        for (int i = 0; i < count; i++)
        {
            if (heroes[i] == null) continue;
            starts[i] = heroes[i].transform.position;
            targets[i] = getTarget(i);
            durations[i] = Mathf.Max(0.05f, Vector3.Distance(starts[i], targets[i]) / Mathf.Max(0.1f, walkSpeed));
            total = Mathf.Max(total, i * departStagger + durations[i]);
        }

        float elapsed = 0f;
        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < count; i++)
            {
                if (heroes[i] == null) continue;
                float k = Mathf.Clamp01((elapsed - i * departStagger) / durations[i]);
                Vector3 pos = Vector3.Lerp(starts[i], targets[i], k);
                if (k > 0f && k < 1f)
                    pos.y += Mathf.Abs(Mathf.Sin((Time.time + i * 0.37f) * bobFrequency)) * bobHeight;
                heroes[i].transform.position = pos;
            }
            yield return null;
        }

        for (int i = 0; i < count; i++)
            if (heroes[i] != null) heroes[i].transform.position = targets[i];
    }
}