using UnityEngine;
using UnityEngine.UI;

/// <summary>드래그 고스트 공용 생성기 — 최상위 정렬, 레이캐스트 통과 (로비/게임 공유 가능)</summary>
public static class DragGhost
{
    public static RectTransform Create(Transform source, string text, Color color, Vector2 size, Vector2 position)
    {
        Canvas parentCanvas = source.GetComponentInParent<Canvas>();
        if (parentCanvas == null) return null;

        var go = new GameObject("DragGhost", typeof(Image));
        go.transform.SetParent(parentCanvas.rootCanvas.transform, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.position = position;

        var textGO = new GameObject("Label", typeof(Text));
        textGO.transform.SetParent(go.transform, false);
        var t = textGO.GetComponent<Text>();
        Text template = source.GetComponentInChildren<Text>(true);
        if (template != null) t.font = template.font;
        t.fontSize = 24;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        t.text = text;
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var over = go.AddComponent<Canvas>();
        over.overrideSorting = true;
        over.sortingOrder = 1000;
        return rt;
    }
}
