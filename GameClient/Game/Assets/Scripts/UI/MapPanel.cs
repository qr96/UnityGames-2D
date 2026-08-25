using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 지도 팝업 — 이번 런에서 지나온 장소들을 경로 순서대로 표시 (일방통행 여정의 기록).
/// 방문 장소 = 점 + 이름, 이동 경로 = 선. 현재 위치는 강조색.
/// 방문 위치들의 실제 월드 좌표를 패널 영역에 맞게 축소해 배치.
/// </summary>
public class MapPanel : MonoBehaviour
{
    [Header("UI 연결 (빌더가 자동 연결)")]
    public RectTransform content;   // 점/선이 배치되는 영역
    public GameObject dotTemplate;  // 비활성 템플릿 (Image + 자식 Text)

    static readonly Color CurrentColor = new Color(0.45f, 0.8f, 1f);
    static readonly Color VisitedColor = new Color(0.6f, 0.63f, 0.7f);
    static readonly Color LineColor = new Color(1f, 1f, 1f, 0.35f);

    readonly List<GameObject> spawned = new List<GameObject>();

    public void Open()
    {
        gameObject.SetActive(true);
        Rebuild();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    void Rebuild()
    {
        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();

        WorldState ws = RunManager.Instance != null ? RunManager.Instance.World : null;
        if (ws == null || content == null || dotTemplate == null) return;

        IReadOnlyList<LocationDefinition> path = ws.VisitedPath;
        if (path.Count == 0) return;

        // 방문 위치들의 월드 좌표 → 패널 로컬 좌표로 스케일링
        Vector2 min = path[0].worldPosition, max = path[0].worldPosition;
        foreach (var loc in path)
        {
            min = Vector2.Min(min, loc.worldPosition);
            max = Vector2.Max(max, loc.worldPosition);
        }
        Vector2 size = Vector2.Max(max - min, Vector2.one * 0.01f);
        Vector2 area = content.rect.size * 0.82f; // 여백
        float scale = Mathf.Min(area.x / size.x, area.y / size.y);
        Vector2 center = (min + max) * 0.5f;

        Vector2 ToLocal(LocationDefinition loc) => (loc.worldPosition - center) * scale;

        // 이동 경로 선 (점보다 먼저 = 아래에 깔림)
        for (int i = 1; i < path.Count; i++)
            SpawnLine(ToLocal(path[i - 1]), ToLocal(path[i]));

        // 방문 장소 점 + 이름
        foreach (var loc in path)
            SpawnDot(loc, ToLocal(loc), ws.Current == loc);
    }

    void SpawnDot(LocationDefinition loc, Vector2 localPos, bool isCurrent)
    {
        GameObject dot = Instantiate(dotTemplate, content);
        dot.name = $"Dot_{loc.id}";
        dot.SetActive(true);
        spawned.Add(dot);

        var rt = dot.GetComponent<RectTransform>();
        rt.anchoredPosition = localPos;

        var img = dot.GetComponent<Image>();
        if (img != null) img.color = isCurrent ? CurrentColor : VisitedColor;

        var label = dot.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = loc.displayName;
            label.color = isCurrent ? CurrentColor : Color.white;
        }
    }

    void SpawnLine(Vector2 a, Vector2 b)
    {
        var go = new GameObject("PathLine", typeof(Image));
        go.transform.SetParent(content, false);
        spawned.Add(go);

        var img = go.GetComponent<Image>();
        img.color = LineColor;
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.sizeDelta = new Vector2(Vector2.Distance(a, b), 5f);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
    }
}
