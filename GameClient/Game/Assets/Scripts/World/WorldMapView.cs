using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 탐험 화면의 장소 라벨(UI) + 이동 입력.
/// 지형은 WorldEnvironment가 상시 표시하고, 여기서는 탐험 중에만
/// 각 공개 장소 위에 스크린 스페이스 UI 라벨을 띄우고(줌아웃 상태에서 '어떤 장소인지' 표시),
/// 이동 가능한 장소에는 지면 하이라이트 링을 깔며, 탭 시 이동을 처리한다.
/// 색: 현재 = 하늘색 / 이동 가능 = 노랑 / 방문함 = 회색
/// </summary>
public class WorldMapView : MonoBehaviour
{
    [Tooltip("라벨을 붙일 캔버스 (비워두면 자동 탐색)")]
    public Canvas canvas;

    [Tooltip("장소 탭 판정 반경 (월드 단위, 줌아웃 기준으로 넉넉하게)")]
    public float tapRadius = 3f;

    [Tooltip("라벨의 월드 기준 오프셋 (지형 위쪽)")]
    public Vector2 labelWorldOffset = new Vector2(0f, 4.5f);

    [Tooltip("이 픽셀 이상 움직이면 탭이 아닌 둘러보기(팬)로 판정")]
    public float dragThresholdPixels = 30f;

    [Tooltip("팬 카메라 (비워두면 자동 탐색)")]
    public CameraController cameraController;

    static readonly Color CurrentColor = new Color(0.55f, 0.85f, 1f);
    static readonly Color ReachableColor = new Color(1f, 0.85f, 0.35f);
    static readonly Color VisitedColor = new Color(0.65f, 0.68f, 0.75f);

    class Entry
    {
        public LocationDefinition loc;
        public Text label;
        public GameObject ring; // 이동 가능 하이라이트 (월드)
    }

    readonly List<Entry> entries = new List<Entry>();
    static readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    bool visible;
    Camera cam;

    bool pressed;
    bool panning;
    Vector2 pressScreenPos;
    Vector2 lastScreenPos;

    static Font cachedFont;
    static bool fontSearched;

    void Start()
    {
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (cameraController == null)
            cameraController = Object.FindFirstObjectByType<CameraController>();

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
        visible = phase == RunPhase.Explore;
        if (visible) Rebuild();
        else Clear();
    }

    // ---------- 이동/둘러보기 입력 ----------
    // 짧게 탭 = 인접 장소로 이동 / 일정 거리 이상 드래그 = 카메라 팬(주변 둘러보기)

    void Update()
    {
        if (!visible) return;

        RunManager rm = RunManager.Instance;
        if (rm == null || rm.World == null) return;

        Pointer pointer = Pointer.current;
        if (pointer == null) return;
        if (cam == null) cam = Camera.main;

        Vector2 screenPos = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame)
        {
            pressed = !IsPointerOverUI(screenPos);
            panning = false;
            pressScreenPos = screenPos;
            lastScreenPos = screenPos;
            return;
        }

        if (!pressed) return;

        if (pointer.press.isPressed)
        {
            // 임계 거리 이상 움직이면 탭 취소 → 팬으로 전환
            if (!panning && Vector2.Distance(screenPos, pressScreenPos) > dragThresholdPixels)
                panning = true;

            if (panning && cameraController != null && cam != null)
            {
                Vector2 screenDelta = lastScreenPos - screenPos; // 지도 끌기 방향
                float worldPerPixel = cam.orthographicSize * 2f / Screen.height;
                cameraController.PanBy(screenDelta * worldPerPixel);
            }
            lastScreenPos = screenPos;
            return;
        }

        if (pointer.press.wasReleasedThisFrame)
        {
            if (!panning) TryTravelTap(screenPos, rm);
            pressed = false;
            panning = false;
        }
    }

    void TryTravelTap(Vector2 screenPos, RunManager rm)
    {
        Vector3 world = ScreenToWorld(screenPos);

        LocationDefinition best = null;
        float bestDist = tapRadius;
        foreach (var loc in rm.World.GetReachable())
        {
            float d = Vector2.Distance(loc.worldPosition, world);
            if (d <= bestDist)
            {
                bestDist = d;
                best = loc;
            }
        }

        if (best != null)
            rm.TravelTo(best);
    }

    // ---------- 라벨/하이라이트 ----------

    void Rebuild()
    {
        Clear();

        RunManager rm = RunManager.Instance;
        WorldState ws = rm != null ? rm.World : null;
        if (ws == null || canvas == null) return;

        // 공개된 장소: 방문 + 현재 인접 (GDD 6)
        var revealed = new HashSet<LocationDefinition>();
        foreach (var loc in ws.world.AllLocations)
            if (ws.IsVisited(loc)) revealed.Add(loc);
        foreach (var adj in ws.GetReachable())
            revealed.Add(adj);

        foreach (var loc in revealed)
        {
            bool isCurrent = ws.Current == loc;
            bool reachable = ws.CanMoveTo(loc);

            var entry = new Entry { loc = loc };

            // 스크린 스페이스 라벨
            entry.label = MakeLabel(loc.displayName,
                isCurrent ? CurrentColor : (reachable ? ReachableColor : VisitedColor),
                isCurrent || reachable ? 34 : 28);

            // 이동 가능 하이라이트 링 (지면)
            if (reachable)
            {
                entry.ring = new GameObject($"ReachableRing_{loc.id}");
                entry.ring.transform.SetParent(transform, false);
                entry.ring.transform.position = loc.worldPosition;
                UnitFactory.MakeVisual(entry.ring.transform, UnitFactory.Circle,
                    new Color(1f, 0.85f, 0.35f, 0.18f), 5.5f, sortingOrder: -7);
            }

            entries.Add(entry);
        }
    }

    void LateUpdate()
    {
        if (!visible || cam == null && (cam = Camera.main) == null) return;

        foreach (var e in entries)
        {
            if (e.label == null) continue;
            Vector3 worldPos = (Vector3)(e.loc.worldPosition + labelWorldOffset);
            e.label.transform.position = cam.WorldToScreenPoint(worldPos);
        }
    }

    void Clear()
    {
        foreach (var e in entries)
        {
            if (e.label != null) Destroy(e.label.gameObject);
            if (e.ring != null) Destroy(e.ring);
        }
        entries.Clear();
    }

    // ---------- 헬퍼 ----------

    Text MakeLabel(string content, Color color, int fontSize)
    {
        var go = new GameObject("LocLabel_" + content, typeof(Text), typeof(Shadow));
        go.transform.SetParent(canvas.transform, false);

        var t = go.GetComponent<Text>();
        t.font = GetFont();
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = color;
        t.text = content;
        t.raycastTarget = false; // 탭이 월드로 통과하도록
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        var shadow = go.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(2f, -2f);

        go.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 60f);
        return t;
    }

    static Font GetFont()
    {
        if (fontSearched) return cachedFont;
        fontSearched = true;
        try { cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (cachedFont == null)
        {
            try { cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return cachedFont;
    }

    bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(ped, uiRaycastResults);
        return uiRaycastResults.Count > 0;
    }

    Vector3 ScreenToWorld(Vector2 screenPos)
    {
        if (cam == null) cam = Camera.main;
        Vector3 w = cam != null ? cam.ScreenToWorldPoint((Vector3)screenPos) : Vector3.zero;
        w.z = 0f;
        return w;
    }
}