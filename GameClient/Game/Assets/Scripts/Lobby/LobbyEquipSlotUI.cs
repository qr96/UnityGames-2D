using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 로비 영웅 관리 [장비] 탭의 장착 슬롯 1칸 (장비 관리 개편 ①).
/// - 표시: 축약명 (★검 / 힘 / 생명) — 전체 정보는 탭(클릭) 시 상세 줄에
/// - 드래그 소스: 잡아서 보관소 목록 위에 놓으면 해제
/// - 드롭 대상: 보관소 행을 이 위에 놓으면 장착/교체 (판정은 HeroManagePanel)
/// </summary>
public class LobbyEquipSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public HeroManagePanel owner;
    public bool isWeaponSlot;
    [HideInInspector] public int slotIndex; // 자유칸 0~2 (무기칸은 무시)
    public Text label;

    static readonly Color GearFilled = new Color(0.30f, 0.45f, 0.85f, 0.95f);
    static readonly Color GearEmpty = new Color(0.12f, 0.13f, 0.19f, 0.90f);
    static readonly Color WeaponFilled = new Color(0.80f, 0.62f, 0.25f, 0.95f);
    static readonly Color WeaponEmpty = new Color(0.24f, 0.20f, 0.12f, 0.90f);

    Image frame;
    RectTransform ghost;

    public EquipmentDefinition Item
    {
        get
        {
            var hero = owner != null ? owner.SelectedHero : null;
            if (hero == null) return null;
            if (isWeaponSlot) return hero.weapon;
            return slotIndex < hero.equipment.Count ? hero.equipment[slotIndex] : null;
        }
    }

    void Awake()
    {
        frame = GetComponent<Image>();
        if (label == null) label = GetComponentInChildren<Text>(true);
    }

    public void RefreshView()
    {
        var item = Item;
        bool filled = item != null;
        if (label != null)
        {
            label.text = filled ? EquipmentGenerator.ShortName(item) : (isWeaponSlot ? "무기" : "");
            label.color = filled ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }
        if (frame != null)
            frame.color = isWeaponSlot
                ? (filled ? WeaponFilled : WeaponEmpty)
                : (filled ? GearFilled : GearEmpty);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (owner != null) owner.OnSlotClicked(this);
    }

    // ---------- 드래그 (해제) ----------

    public void OnBeginDrag(PointerEventData e)
    {
        if (Item == null || owner == null)
        {
            e.pointerDrag = null;
            return;
        }
        ghost = DragGhost.Create(transform, EquipmentGenerator.ShortName(Item),
            isWeaponSlot ? WeaponFilled : GearFilled, ((RectTransform)transform).rect.size, e.position);
    }

    public void OnDrag(PointerEventData e)
    {
        if (ghost != null) ghost.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (ghost != null) { Destroy(ghost.gameObject); ghost = null; }
        if (owner != null) owner.OnSlotDragEnd(this, e);
    }
}
