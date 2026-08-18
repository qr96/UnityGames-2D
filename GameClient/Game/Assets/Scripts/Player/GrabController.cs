using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 핵심 조작: 용사를 잡아서 옮기기. (Input System 기반)
/// Pointer.current가 마우스/터치를 모두 커버하므로 에디터와 모바일에서 동일하게 동작.
/// 터치 시작 지점 근처의 용사를 잡고, 드래그를 따라가게 하고, 놓으면 그 자리에서 AI 복귀.
/// </summary>
public class GrabController : MonoBehaviour
{
    [Tooltip("터치 지점에서 이 거리 안의 용사를 잡음 (판정을 후하게)")]
    public float grabRadius = 0.7f;

    Hero grabbed;
    Camera cam;

    static readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    void Update()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        // Pointer.current: 마우스와 (기본) 터치를 통합한 포인터
        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        // 잡고 있던 용사가 사망(비활성화)하면 손에서 놓침
        if (grabbed != null && !grabbed.gameObject.activeInHierarchy)
            grabbed = null;

        Vector2 screenPos = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame && !IsPointerOverUI(screenPos))
            TryGrab(ScreenToWorld(screenPos));

        if (grabbed == null) return;

        if (pointer.press.isPressed)
            grabbed.UpdateGrabPosition(ScreenToWorld(screenPos));

        if (pointer.press.wasReleasedThisFrame)
        {
            grabbed.Release();
            grabbed = null;
        }
    }

    void TryGrab(Vector3 world)
    {
        Hero best = null;
        float bestDist = grabRadius;

        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
        {
            if (!(u is Hero h)) continue;
            float d = Vector2.Distance(u.transform.position, world);
            if (d <= bestDist)
            {
                bestDist = d;
                best = h;
            }
        }

        if (best != null)
        {
            grabbed = best;
            best.Grab();
        }
    }

    Vector3 ScreenToWorld(Vector2 screen)
    {
        Vector3 w = cam.ScreenToWorldPoint(screen);
        w.z = 0f;
        return w;
    }

    /// <summary>
    /// 해당 스크린 좌표에 UI가 있는지 검사.
    /// EventSystem.RaycastAll 기반이라 입력 백엔드와 무관하게 안전하게 동작.
    /// </summary>
    bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(ped, uiRaycastResults);
        return uiRaycastResults.Count > 0;
    }
}
