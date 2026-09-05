using UnityEngine;

/// <summary>
/// 경기장 영역 (배치/전투 중 유닛 이동 한계) — UI 존/화면 밖 진입 차단.
/// 문제: 유닛이 하단 포션 바·인벤토리 뒤나 좌우 가장자리로 들어가면
///   UI가 포인터를 가로채 잡기(드래그)가 불가능해짐.
/// 해법: 카메라 뷰 기준 사각형에서 UI 마진을 제외한 영역으로 모든 유닛을 클램프.
///   Unit.LateUpdate에서 호출 — 영웅/적/잡힌 영웅(드래그 추적) 전부 커버.
///
/// 마진 ※임시 (화면 비율 1080×1440 UI 기준 비율):
///   상단 = 라벨 아래, 좌우 = 여백, 하단 = 배치 중엔 인벤토리 위 / 전투 중엔 포션 바 위.
/// </summary>
public static class BattleArena
{
    // 마진 — 기준 해상도(1080×1440) 픽셀 단위 (※임시 — UI 배치 변경 시 함께 조정).
    // 실제 화면에서는 UI 스케일(폭 기준, CanvasScaler match=0)로 환산 →
    // 창 비율이 3:4가 아니어도 UI 실높이와 정확히 일치.
    const float RefWidth = 1080f;         // CanvasScaler referenceResolution.x
    const float TopRefPx = 160f;          // 상단 라벨(124) + 여유
    const float SideRefPx = 38f;          // 좌우 여백
    const float BottomCombatRefPx = 290f; // 포션 바(y110+150) + 여유
    const float BottomPlacementRefPx = 520f; // 인벤토리 패널(y20+470) + 여유

    static Camera cam;
    static int cachedFrame = -1;
    static Vector2 min, max;
    static bool valid;

    /// <summary>배치/전투 중에만 클램프 (탐험 연출 이동은 자유)</summary>
    public static bool ShouldClamp
    {
        get
        {
            var rm = RunManager.Instance;
            return rm != null &&
                (rm.Phase == RunPhase.Placement || rm.Phase == RunPhase.Battle);
        }
    }

    /// <summary>유닛 위치를 경기장 안으로 (LateUpdate에서 호출)</summary>
    public static Vector3 Clamp(Vector3 pos)
    {
        RefreshBounds();
        if (!valid) return pos;
        pos.x = Mathf.Clamp(pos.x, min.x, max.x);
        pos.y = Mathf.Clamp(pos.y, min.y, max.y);
        return pos;
    }

    static void RefreshBounds()
    {
        if (Time.frameCount == cachedFrame) return;
        cachedFrame = Time.frameCount;
        valid = false;

        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 c = cam.transform.position;

        bool placement = RunManager.Instance != null && RunManager.Instance.Phase == RunPhase.Placement;
        float bottomRefPx = placement ? BottomPlacementRefPx : BottomCombatRefPx;

        // 기준 픽셀 → 실제 화면 픽셀 (UI는 폭 기준 스케일) → 화면 비율 → 월드 거리
        float uiScale = Screen.width / RefWidth;
        float worldPerPxY = 2f * halfH / Screen.height;
        float worldPerPxX = 2f * halfW / Screen.width;

        min = new Vector2(
            c.x - halfW + SideRefPx * uiScale * worldPerPxX,
            c.y - halfH + bottomRefPx * uiScale * worldPerPxY);
        max = new Vector2(
            c.x + halfW - SideRefPx * uiScale * worldPerPxX,
            c.y + halfH - TopRefPx * uiScale * worldPerPxY);
        valid = min.x < max.x && min.y < max.y;
    }
}