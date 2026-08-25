using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 월드 지형 레이어 — 각 장소의 '실제 맵(플레이 영역)'을 월드 좌표 위에 상시 존재시킴.
/// GDD 9·10: 별도 지도 화면이 아니라 실제 세계를 카메라 줌으로 봄.
///           게임적으로는 Arena지만 시각적으로는 실제 Location처럼 보이게.
///
/// - 표시 규칙(개편): 화면에는 항상 '한 맵만' 존재 — 현재 장소의 지형/길만 보임.
///   이동 연출 중에는 출발 맵 → (암전 중 ShowOnly로 교체) → 도착 맵.
///   지나온 곳의 기록은 지도 팝업(MapPanel)이 담당.
/// - 최적화: 카메라 시야(+여유)에 들어오는 지형/길만 활성화 (컬링)
/// - 지형은 결정적(장소 id 시드)으로 배치 → 세계는 고정 (GDD 7)
/// - 자리표시자 비주얼: 타입별 바닥 + 가장자리 장식(경계 숨김 암시 — 숲=나무, 폐허=잔해...).
///   아트가 들어오면 BuildEnvironment만 프리팹 방식으로 교체.
/// </summary>
public class WorldEnvironment : MonoBehaviour
{
    [Tooltip("장소 지형의 반크기 (전투장 스폰 영역보다 약간 크게)")]
    public Vector2 arenaExtents = new Vector2(5.5f, 7.5f);

    [Tooltip("카메라 시야 컬링 여유 거리")]
    public float cullingMargin = 8f;

    class EnvEntry
    {
        public LocationDefinition loc;
        public GameObject root;
        public Rect rect;
    }

    class RoadEntry
    {
        public LocationDefinition a, b;
        public GameObject root;
        public Rect rect;
    }

    readonly List<EnvEntry> envs = new List<EnvEntry>();
    readonly List<RoadEntry> roads = new List<RoadEntry>();

    Camera cam;
    bool built;
    LocationDefinition visibleLocation; // 화면에 표시되는 유일한 맵

    void Start()
    {
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

    void OnPhase(RunPhase _)
    {
        RunManager rm = RunManager.Instance;
        WorldState ws = rm != null ? rm.World : null;
        if (ws == null) return;

        if (!built) Build(ws.world);

        // 페이즈 전환 시 = 현재 장소만 표시
        // (이동 연출 중 출발 맵 → 도착 맵 교체는 TravelController가 암전 중 ShowOnly로 처리)
        visibleLocation = ws.Current;
    }

    /// <summary>표시할 맵을 지정 (이동 연출의 암전 중 TravelController가 호출)</summary>
    public void ShowOnly(LocationDefinition loc)
    {
        visibleLocation = loc;
    }

    // ---------- 카메라 컬링 ----------

    void LateUpdate()
    {
        if (!built) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        float halfH = cam.orthographicSize + cullingMargin;
        float halfW = cam.orthographicSize * cam.aspect + cullingMargin;
        Vector2 c = cam.transform.position;
        var view = new Rect(c.x - halfW, c.y - halfH, halfW * 2f, halfH * 2f);

        foreach (var e in envs)
            SetActive(e.root, e.loc == visibleLocation && view.Overlaps(e.rect));

        // 길: 현재 맵에 붙은 것만 (걸어 나가고 들어오는 길이 보이도록)
        foreach (var r in roads)
            SetActive(r.root, (r.a == visibleLocation || r.b == visibleLocation) && view.Overlaps(r.rect));
    }

    static void SetActive(GameObject go, bool value)
    {
        if (go != null && go.activeSelf != value) go.SetActive(value);
    }

    // ---------- 지형 생성 (자리표시자 — 아트 교체 지점) ----------

    void Build(WorldDefinition world)
    {
        built = true;

        foreach (var loc in world.AllLocations)
            BuildEnvironment(loc);

        var drawn = new HashSet<string>();
        foreach (var a in world.AllLocations)
        {
            foreach (var (_, b) in a.Exits)
            {
                if (b == null) continue;
                string key = string.CompareOrdinal(a.id, b.id) < 0 ? a.id + "|" + b.id : b.id + "|" + a.id;
                if (!drawn.Add(key)) continue;
                BuildRoad(a, b);
            }
        }
    }

    void BuildEnvironment(LocationDefinition loc)
    {
        var root = new GameObject($"Env_{loc.id}");
        root.transform.SetParent(transform, false);
        root.transform.position = loc.worldPosition;

        // 바닥
        var ground = UnitFactory.MakeVisual(root.transform, UnitFactory.Square, GroundColor(loc.type), 1f, sortingOrder: -10);
        ground.transform.localScale = new Vector3(arenaExtents.x * 2f, arenaExtents.y * 2f, 1f);

        // 가장자리 장식 — 경계를 자연물처럼 암시 (GDD 10). 장소 id 시드로 항상 동일 배치.
        var rng = new System.Random(loc.id.GetHashCode());
        int decoCount = 12;
        for (int i = 0; i < decoCount; i++)
        {
            Vector2 p = EdgePoint(rng);
            bool square = loc.type == LocationType.Settlement; // 마을은 집(사각형)
            var deco = UnitFactory.MakeVisual(root.transform,
                square ? UnitFactory.Square : UnitFactory.Circle,
                DecoColor(loc.type), Mathf.Lerp(0.6f, 1.2f, (float)rng.NextDouble()), sortingOrder: -9);
            deco.transform.localPosition = p;
        }

        // 야영지 소품: 모닥불 + 텐트 + 짐 (야영지 기획 3·7 — 안전하고 조용한 장소)
        if (loc.type == LocationType.Camp)
        {
            var fire = UnitFactory.MakeVisual(root.transform, UnitFactory.Circle,
                new Color(1f, 0.55f, 0.2f), 0.7f, sortingOrder: -9);
            fire.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            var tent = UnitFactory.MakeVisual(root.transform, UnitFactory.Square,
                new Color(0.42f, 0.32f, 0.22f), 1.5f, sortingOrder: -9);
            tent.transform.localPosition = new Vector3(-2.4f, 2.6f, 0f);

            var bag = UnitFactory.MakeVisual(root.transform, UnitFactory.Circle,
                new Color(0.35f, 0.28f, 0.20f), 0.6f, sortingOrder: -9);
            bag.transform.localPosition = new Vector3(2.2f, 2.2f, 0f);
        }

        root.SetActive(false);

        float margin = 1.5f;
        envs.Add(new EnvEntry
        {
            loc = loc,
            root = root,
            rect = new Rect(
                loc.worldPosition.x - arenaExtents.x - margin,
                loc.worldPosition.y - arenaExtents.y - margin,
                (arenaExtents.x + margin) * 2f,
                (arenaExtents.y + margin) * 2f),
        });
    }

    Vector2 EdgePoint(System.Random rng)
    {
        // 사각 지형의 가장자리 근처 임의 지점 (안쪽으로 살짝)
        int side = rng.Next(4);
        float t = (float)rng.NextDouble() * 2f - 1f;
        float inset = 0.6f + (float)rng.NextDouble() * 0.7f;
        switch (side)
        {
            case 0: return new Vector2(t * arenaExtents.x, arenaExtents.y - inset);
            case 1: return new Vector2(t * arenaExtents.x, -arenaExtents.y + inset);
            case 2: return new Vector2(arenaExtents.x - inset, t * arenaExtents.y);
            default: return new Vector2(-arenaExtents.x + inset, t * arenaExtents.y);
        }
    }

    void BuildRoad(LocationDefinition a, LocationDefinition b)
    {
        Vector2 pa = a.worldPosition;
        Vector2 pb = b.worldPosition;

        var root = new GameObject($"Road_{a.id}_{b.id}");
        root.transform.SetParent(transform, false);
        root.transform.position = (pa + pb) * 0.5f;
        root.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg);

        var sr = UnitFactory.MakeVisual(root.transform, UnitFactory.Square,
            new Color(0.55f, 0.48f, 0.38f, 0.55f), 1f, sortingOrder: -8);
        sr.transform.localScale = new Vector3(Vector2.Distance(pa, pb), 0.7f, 1f);

        root.SetActive(false);

        var min = Vector2.Min(pa, pb) - Vector2.one * 2f;
        var max = Vector2.Max(pa, pb) + Vector2.one * 2f;
        roads.Add(new RoadEntry
        {
            a = a,
            b = b,
            root = root,
            rect = new Rect(min, max - min),
        });
    }

    static Color GroundColor(LocationType type)
    {
        switch (type)
        {
            case LocationType.Field: return new Color(0.16f, 0.28f, 0.17f); // 숲/평원
            case LocationType.Exploration: return new Color(0.22f, 0.20f, 0.18f); // 폐허/광산
            case LocationType.Settlement: return new Color(0.30f, 0.26f, 0.17f); // 마을
            case LocationType.Camp: return new Color(0.21f, 0.26f, 0.16f); // 야영지
            case LocationType.Landmark: return new Color(0.20f, 0.15f, 0.28f); // 랜드마크
            default: return new Color(0.2f, 0.2f, 0.2f);
        }
    }

    static Color DecoColor(LocationType type)
    {
        switch (type)
        {
            case LocationType.Field: return new Color(0.10f, 0.20f, 0.11f); // 나무
            case LocationType.Exploration: return new Color(0.35f, 0.33f, 0.30f); // 잔해/바위
            case LocationType.Settlement: return new Color(0.45f, 0.36f, 0.24f); // 집
            case LocationType.Camp: return new Color(0.12f, 0.18f, 0.11f);
            case LocationType.Landmark: return new Color(0.38f, 0.30f, 0.52f); // 석주
            default: return Color.gray;
        }
    }
}