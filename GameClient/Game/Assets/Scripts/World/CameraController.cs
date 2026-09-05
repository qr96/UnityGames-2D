using UnityEngine;

/// <summary>
/// 페이즈에 따라 카메라를 부드럽게 이동/줌.
///  - 탐험/야영지/배치/전투: 현재 장소로 프레이밍
///  - 이동 연출: 카메라 고정 (전환은 TravelController가 SnapTo로 처리)
/// </summary>
public class CameraController : MonoBehaviour
{
    [Tooltip("페이즈 줌 값 — 3:4 기준 세로 반높이 (다른 비율에서는 가로 폭이 유지되도록 자동 환산)")]
    public float exploreSize = 10.5f;
    public float battleSize = 8f;
    public float moveSpeed = 3f;

    // 해상도 대응: 줌 값을 '3:4에서의 세로 반높이'로 해석 → 가로 반폭 = 값 × 0.75 불변.
    // 실제 orthographicSize = 반폭 / 현재 aspect — 창이 세로로 길어지면 자동으로 멀어짐.
    const float RefAspect = 3f / 4f;

    Camera cam;
    Vector3 targetPos;
    float targetHalfWidth; // 목표 가로 반폭 (비율 무관 불변량)

    float TargetOrthoSize => cam != null && cam.aspect > 0f
        ? targetHalfWidth / cam.aspect
        : targetHalfWidth / RefAspect;

    void Start()
    {
        cam = Camera.main;
        if (cam != null)
        {
            targetPos = cam.transform.position;
            targetHalfWidth = cam.orthographicSize * cam.aspect; // 현재 프레이밍의 반폭 승계
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

        switch (phase)
        {
            case RunPhase.Explore:
                SetTarget(center, exploreSize);
                break;

            case RunPhase.Camp:
            case RunPhase.Placement:
            case RunPhase.Battle:
                SetTarget(center, battleSize);
                break;

                // Travel: 카메라 고정 (TravelController가 암전 중 SnapTo로 전환)
                // Loot / Recruit / RunClear / RunFailed: 현재 프레이밍 유지
        }
    }

    /// <summary>즉시 이동 (러프 없이) — 암전 중 맵 전환용 (TravelController가 호출)</summary>
    public void SnapTo(Vector2 pos, float size)
    {
        targetPos = new Vector3(pos.x, pos.y, -10f);
        targetHalfWidth = size * RefAspect; // 3:4 기준 값 → 반폭
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = targetPos;
            cam.orthographicSize = TargetOrthoSize;
        }
    }

    void SetTarget(Vector2 pos, float size)
    {
        targetPos = new Vector3(pos.x, pos.y, -10f);
        targetHalfWidth = size * RefAspect; // 3:4 기준 값 → 반폭
    }

    void LateUpdate()
    {
        if (cam == null) return;
        float t = 1f - Mathf.Exp(-moveSpeed * Time.deltaTime);
        cam.transform.position = Vector3.Lerp(cam.transform.position, targetPos, t);
        // 목표를 매 프레임 현재 비율로 환산 — 창 리사이즈에도 즉시 대응
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, TargetOrthoSize, t);
    }
}