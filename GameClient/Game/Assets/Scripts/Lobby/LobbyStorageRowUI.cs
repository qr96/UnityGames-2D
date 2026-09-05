using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 로비 [장비] 탭의 보관소 목록 행 1개 (장비 관리 개편 ①).
/// - 표시: [타입 뱃지] 전체 이름 (★특별은 금색 틴트)
/// - 탭: 상세 줄에 전체 이름 표시
/// - 드래그: 장착 슬롯 위에 놓으면 장착/교체 (판정은 HeroManagePanel)
/// </summary>
public class LobbyStorageRowUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public HeroManagePanel owner;
    [HideInInspector] public EquipmentDefinition item;
    public Text badge;
    public Text label;

    static readonly Color NormalFrame = new Color(0.14f, 0.17f, 0.25f, 0.92f);
    static readonly Color SpecialFrame = new Color(0.32f, 0.27f, 0.12f, 0.95f);

    Image frame;
    RectTransform ghost;

    void Awake()
    {
        frame = GetComponent<Image>();
    }

    public void Bind(EquipmentDefinition newItem)
    {
        item = newItem;
        if (badge != null)
            badge.text = item is WeaponDefinition ? "무기"
                : (EquipmentGenerator.IsVitalGear(item) ? "생명" : "힘");
        if (label != null) label.text = item != null ? item.displayName : "";
        if (frame != null) frame.color = item != null && item.isSpecial ? SpecialFrame : NormalFrame;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (owner != null && item != null) owner.ShowDetailLine(item.displayName);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (item == null || owner == null)
        {
            e.pointerDrag = null;
            return;
        }
        ghost = DragGhost.Create(transform, EquipmentGenerator.ShortName(item),
            item.isSpecial ? SpecialFrame : NormalFrame, new Vector2(150f, 70f), e.position);
    }

    public void OnDrag(PointerEventData e)
    {
        if (ghost != null) ghost.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (ghost != null) { Destroy(ghost.gameObject); ghost = null; }
        if (owner != null) owner.OnRowDragEnd(this, e);
    }
}