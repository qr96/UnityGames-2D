using UnityEngine;

/// <summary>
/// 가로 폭 고정 카메라 (해상도 대응) — 로비 등 '페이즈 줌이 없는 씬' 전용.
/// 게임 씬은 CameraController가 페이즈 줌 + 비율 환산을 담당하므로 이 컴포넌트를 붙이지 않는다 (경합).
/// UI(CanvasScaler 폭 기준)와 같은 규칙으로 월드를 스케일.
/// 문제: orthographicSize는 '세로 반높이'라 창이 세로로 길어지면 가로 시야가 잘림.
/// 해법: 월드 가로 반폭(worldHalfWidth)을 불변으로 두고 size = 반폭 / aspect 로 매 갱신 —
///   창이 위아래로 늘어나면 카메라가 자동으로 멀어지며(세로 시야 확장) 전장이 항상 가로에 맞음.
/// 기준: 3:4에서 size 8 = 반폭 6 (기존 프레이밍과 동일 — 스폰 영역 x±4 여유 포함).
/// </summary>
[RequireComponent(typeof(Camera))]
public class FixedWidthCamera : MonoBehaviour
{
    [Tooltip("항상 보이는 월드 가로 반폭 (3:4에서 size 8과 동일한 6이 기본)")]
    public float worldHalfWidth = 6f;

    Camera cam;
    float lastAspect = -1f;

    void Awake()
    {
        cam = GetComponent<Camera>();
        Apply();
    }

    void LateUpdate()
    {
        Apply(); // 매 프레임 적용 — 창 리사이즈 즉시 대응 (로비엔 카메라를 만지는 다른 스크립트 없음)
    }

    void Apply()
    {
        if (cam == null || !cam.orthographic) return;
        lastAspect = cam.aspect;
        if (lastAspect <= 0f) return;
        cam.orthographicSize = worldHalfWidth / lastAspect;
    }
}