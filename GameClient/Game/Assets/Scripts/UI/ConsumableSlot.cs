using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 소모품 바의 슬롯 1칸. 아이콘을 전장으로 드래그해서 사용.
/// 빈 칸은 드래그 취소, 사용 후 아이콘은 제자리 복귀.
/// 드래그 중에는 아이콘에 정렬 오버라이드 Canvas를 붙여 모든 UI 위에 표시.
/// </summary>
public class ConsumableSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public ConsumableBar bar;
    [HideInInspector] public int index;
    public Image icon;   // 드래그되는 콘텐츠
    public Text label;

    bool filled;
    Canvas dragCanvas;

    void Awake()
    {
        // 인스펙터 연결이 비어 있으면 자식에서 자동 탐색 ("Icon" 오브젝트 + 그 안의 Text)
        if (icon == null)
        {
            var t = transform.Find("Icon");
            if (t != null) icon = t.GetComponent<Image>();
        }
        if (label == null) label = GetComponentInChildren<Text>(true);
        if (icon != null) icon.raycastTarget = false; // 드래그 이벤트는 슬롯 프레임이 받음
    }

    public void SetFilled(bool value, string itemName)
    {
        filled = value;
        if (icon != null) icon.gameObject.SetActive(value);
        if (label != null) label.text = value ? itemName : "";
        ResetIcon();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (!filled)
        {
            e.pointerDrag = null;
            return;
        }
        BeginDragOverlay();
    }

    public void OnDrag(PointerEventData e)
    {
        if (icon != null) icon.rectTransform.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        bar.TryUseAt(index, e.position);
        ResetIcon();
        EndDragOverlay();
    }

    /// <summary>드래그 중 아이콘을 최상위에 렌더링 (다른 패널에 가려지지 않게)</summary>
    void BeginDragOverlay()
    {
        if (icon == null || dragCanvas != null) return;
        dragCanvas = icon.gameObject.AddComponent<Canvas>();
        dragCanvas.overrideSorting = true;
        dragCanvas.sortingOrder = 1000;
    }

    void EndDragOverlay()
    {
        if (dragCanvas != null)
        {
            Destroy(dragCanvas);
            dragCanvas = null;
        }
    }

    void ResetIcon()
    {
        if (icon != null) icon.rectTransform.anchoredPosition = Vector2.zero;
    }
}