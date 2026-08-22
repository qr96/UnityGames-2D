using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 로비 좌우 드래그 (GDD 2: 용병단을 구경하기 위한 기능).
/// - 가로만 이동, 세로 이동 없음 (GDD 10: 자유 상하 카메라 이동 제외)
/// - 관성: 튕기면 미끄러지다 감속, 공간 끝에서 정지, 미끄러지는 중 다시 누르면 즉시 잡힘
/// - 카메라 시야가 로비 공간 밖으로 나가지 않게 클램프
/// </summary>
public class LobbyCameraDrag : MonoBehaviour
{
    [Tooltip("로비 가로 반폭 — LobbyController.spaceHalfWidth와 맞출 것")]
    public float halfWidth = 18f;

    [Header("관성")]
    public bool useInertia = true;
    [Tooltip("감속 강도 (클수록 빨리 멈춤)")]
    public float damping = 4f;
    [Tooltip("튕기기 최대 속도 (월드 단위/초)")]
    public float maxFlingSpeed = 40f;

    Camera cam;
    bool pressed;
    Vector2 lastScreenPos;
    float velocityX; // 관성 속도 (월드 단위/초)

    static readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    void Update()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        Vector2 screenPos = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame)
        {
            pressed = !IsPointerOverUI(screenPos);
            lastScreenPos = screenPos;
            velocityX = 0f; // 미끄러지는 중 다시 누르면 즉시 잡힘
            return;
        }

        if (pressed && pointer.press.isPressed)
        {
            float worldPerPixel = cam.orthographicSize * 2f / Screen.height;
            float deltaX = (lastScreenPos.x - screenPos.x) * worldPerPixel; // 공간 끌기 방향

            MoveBy(deltaX);

            // 순간 속도 추정 (약간 스무딩) — 놓는 순간 이 속도로 미끄러짐
            if (Time.deltaTime > 0f)
                velocityX = Mathf.Lerp(velocityX, deltaX / Time.deltaTime, 0.5f);

            lastScreenPos = screenPos;
            return;
        }

        if (pressed && pointer.press.wasReleasedThisFrame)
        {
            pressed = false;
            if (!useInertia) velocityX = 0f;
            velocityX = Mathf.Clamp(velocityX, -maxFlingSpeed, maxFlingSpeed);
            return;
        }

        // ---- 관성 활강 ----
        if (!pressed && useInertia && Mathf.Abs(velocityX) > 0.05f)
        {
            bool hitEdge = MoveBy(velocityX * Time.deltaTime);
            if (hitEdge) velocityX = 0f; // 공간 끝에 닿으면 정지
            else velocityX *= Mathf.Exp(-damping * Time.deltaTime);
        }
    }

    /// <summary>카메라를 X로 이동, 공간 한계에 클램프. 한계에 걸렸으면 true.</summary>
    bool MoveBy(float deltaX)
    {
        Vector3 pos = cam.transform.position;
        float viewHalf = cam.orthographicSize * cam.aspect;
        float limit = Mathf.Max(0f, halfWidth - viewHalf);

        float desired = pos.x + deltaX;
        pos.x = Mathf.Clamp(desired, -limit, limit);
        cam.transform.position = pos;

        return !Mathf.Approximately(desired, pos.x); // 클램프에 걸림 = 끝에 닿음
    }

    bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(ped, uiRaycastResults);
        return uiRaycastResults.Count > 0;
    }
}