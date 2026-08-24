using UnityEngine;

/// <summary>
/// 페이즈에 따라 카메라를 부드럽게 이동/줌 (GDD 9: 지도 화면 전환 대신 카메라 줌).
///  - 탐험: 줌아웃, 현재 장소와 인접 장소들이 보이도록 프레이밍
///  - 배치/전투: 현재 장소 위치로 줌인 (전투장은 장소의 월드 좌표 위에 열림)
///  - 전리품/결과 등: 프레이밍 유지
/// ※ 영웅들이 길을 따라 걷는 이동 연출(GDD 11)은 다음 단계에서 추가.
/// </summary>
public class CameraController : MonoBehaviour
{
    public float exploreSize = 24f;
    public float battleSize = 8f;
    public float travelSize = 11f; // 이동 연출 중 파티 추적 줌
    public float moveSpeed = 3f;

    Camera cam;
    Vector3 targetPos;
    float targetSize;
    bool followParty; // 이동 연출 중에는 파티 중심을 추적
    Rect panBounds;   // 둘러보기 이동 한계 (공개된 장소 범위 + 여유)
    bool hasPanBounds;

    void Start()
    {
        cam = Camera.main;
        if (cam != null)
        {
            targetPos = cam.transform.position;
            targetSize = cam.orthographicSize;
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnPhaseChanged += OnPhase;
            OnPhase(RunManager.Instance.Phase);
        }
    }

    void OnDestroy()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnPhaseChanged -= OnPhase;
    }

    void OnPhase(RunPhase phase)
    {
        RunManager rm = RunManager.Instance;
        if (rm == null || rm.World == null || rm.World.Current == null) return;

        Vector2 center = rm.World.Current.worldPosition;
        followParty = phase == RunPhase.Travel;

        switch (phase)
        {
            case RunPhase.Travel:
                targetSize = travelSize; // 위치는 LateUpdate에서 파티 중심 추적
                break;

            case RunPhase.Explore:
                // 현재 + 인접 장소들의 중심으로 프레이밍
                Vector2 sum = center;
                int n = 1;
                foreach (var adj in rm.World.GetReachable())
                {
                    if (adj == null) continue;
                    sum += adj.worldPosition;
                    n++;
                }
                SetTarget(sum / n, exploreSize);
                ComputePanBounds(rm);
                break;

            case RunPhase.Camp:
            case RunPhase.Placement:
            case RunPhase.Battle:
                SetTarget(center, battleSize);
                break;

                // Loot / Recruit / RunClear / RunFailed: 현재 프레이밍 유지
        }
    }

    /// <summary>탐험 중 드래그로 주변 둘러보기 (WorldMapView가 호출). 공개 영역 안으로 클램프.</summary>
    public void PanBy(Vector2 worldDelta)
    {
        targetPos += (Vector3)worldDelta;
        if (hasPanBounds)
        {
            targetPos.x = Mathf.Clamp(targetPos.x, panBounds.xMin, panBounds.xMax);
            targetPos.y = Mathf.Clamp(targetPos.y, panBounds.yMin, panBounds.yMax);
        }
        // 손가락에 1:1로 붙는 느낌 — 즉시 반영 (러프 없이)
        if (cam == null) cam = Camera.main;
        if (cam != null) cam.transform.position = targetPos;
    }

    /// <summary>둘러보기 한계 = 공개된(방문+인접) 장소들의 범위 + 여유</summary>
    void ComputePanBounds(RunManager rm)
    {
        hasPanBounds = false;
        WorldState ws = rm.World;
        if (ws == null) return;

        bool any = false;
        Vector2 min = Vector2.zero, max = Vector2.zero;
        foreach (var loc in ws.world.AllLocations)
        {
            if (!ws.IsVisited(loc) && !ws.CanMoveTo(loc)) continue;
            if (!any) { min = max = loc.worldPosition; any = true; }
            else
            {
                min = Vector2.Min(min, loc.worldPosition);
                max = Vector2.Max(max, loc.worldPosition);
            }
        }
        if (!any) return;

        const float pad = 12f;
        panBounds = new Rect(min.x - pad, min.y - pad, (max.x - min.x) + pad * 2f, (max.y - min.y) + pad * 2f);
        hasPanBounds = true;
    }

    void SetTarget(Vector2 pos, float size)
    {
        targetPos = new Vector3(pos.x, pos.y, -10f);
        targetSize = size;
    }

    void LateUpdate()
    {
        if (cam == null) return;

        // 이동 연출: 파티 중심을 따라감 (GDD 9: 카메라가 영웅들에게 가까워짐)
        if (followParty)
        {
            var heroes = UnitRegistry.GetAll(Team.Hero);
            if (heroes.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                foreach (var h in heroes) sum += h.transform.position;
                Vector3 c = sum / heroes.Count;
                targetPos = new Vector3(c.x, c.y, -10f);
            }
        }

        float t = 1f - Mathf.Exp(-moveSpeed * Time.deltaTime);
        cam.transform.position = Vector3.Lerp(cam.transform.position, targetPos, t);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, t);
    }
}