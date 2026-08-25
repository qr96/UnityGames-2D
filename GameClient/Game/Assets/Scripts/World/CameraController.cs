using UnityEngine;

/// <summary>
/// 페이즈에 따라 카메라를 부드럽게 이동/줌.
///  - 탐험/야영지/배치/전투: 현재 장소로 프레이밍
///  - 이동 연출: 카메라 고정 (전환은 TravelController가 SnapTo로 처리)
/// </summary>
public class CameraController : MonoBehaviour
{
    public float exploreSize = 10.5f;
    public float battleSize = 8f;
    public float moveSpeed = 3f;

    Camera cam;
    Vector3 targetPos;
    float targetSize;

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
        targetSize = size;
        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = targetPos;
            cam.orthographicSize = size;
        }
    }

    void SetTarget(Vector2 pos, float size)
    {
        targetPos = new Vector3(pos.x, pos.y, -10f);
        targetSize = size;
    }

    void LateUpdate()
    {
        if (cam == null) return;
        float t = 1f - Mathf.Exp(-moveSpeed * Time.deltaTime);
        cam.transform.position = Vector3.Lerp(cam.transform.position, targetPos, t);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, t);
    }
}