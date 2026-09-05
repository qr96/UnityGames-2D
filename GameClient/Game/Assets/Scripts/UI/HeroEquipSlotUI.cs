using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 파티 장비 패널의 슬롯 1칸 (특정 영웅의 특정 장비칸).
/// - 드롭 대상: 인벤토리 아이템을 이 위에 드롭하면 장착 (점유 칸이면 교체)
/// - 드래그 소스: 장착된 장비를 잡아서 다른 영웅 칸/영웅/인벤토리로 이동 가능
///   (드래그 중에는 임시 고스트가 최상위에 표시됨)
/// 이 컴포넌트의 Image가 Raycast Target이어야 드롭 판정이 동작 (기본값 유지).
/// </summary>
public class HeroEquipSlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Text label;
    [HideInInspector] public PartyEquipPanel owner;

    [Tooltip("무기 전용 슬롯 (무기 스펙 v2) — 빌더가 설정. 무기만 장착/이동 가능")]
    public bool isWeaponSlot;

    static readonly Color FilledColor = new Color(0.30f, 0.45f, 0.85f, 0.95f);
    static readonly Color EmptyColor = new Color(0.12f, 0.13f, 0.19f, 0.90f);
    static readonly Color WeaponFilledColor = new Color(0.80f, 0.62f, 0.25f, 0.95f); // 무기 = 금색 계열
    static readonly Color WeaponEmptyColor = new Color(0.24f, 0.20f, 0.12f, 0.90f);

    public HeroRunInstance Hero { get; private set; }
    public int SlotIndex { get; private set; }

    public bool HasItem => Hero != null &&
        (isWeaponSlot ? Hero.weapon != null : SlotIndex < Hero.equipment.Count);

    /// <summary>슬롯의 현재 장비 (무기 슬롯이면 무기)</summary>
    public EquipmentDefinition Item =>
        !HasItem ? null : (isWeaponSlot ? Hero.weapon : Hero.equipment[SlotIndex]);

    Image frame;
    RectTransform ghost;

    void Awake()
    {
        frame = GetComponent<Image>();
        if (label == null) label = GetComponentInChildren<Text>(true);
    }

    public void Bind(HeroRunInstance hero, int slotIndex)
    {
        Hero = hero;
        SlotIndex = slotIndex;
        RefreshView();
    }

    public void RefreshView()
    {
        bool filled = HasItem;
        if (label != null)
        {
            // 빈 무기 슬롯은 "무기" 자리 표시 — 슬롯 성격을 드러냄
            label.text = filled ? Item.displayName : (isWeaponSlot ? "무기" : "");
            label.color = filled ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }
        if (frame != null)
            frame.color = isWeaponSlot
                ? (filled ? WeaponFilledColor : WeaponEmptyColor)
                : (filled ? FilledColor : EmptyColor);
    }

    // ---------- 장착된 장비 드래그 (영웅 간 이전 / 탈착) ----------

    public void OnBeginDrag(PointerEventData e)
    {
        if (!HasItem || owner == null)
        {
            e.pointerDrag = null; // 빈 칸은 드래그 취소
            return;
        }
        CreateGhost(e.position);
    }

    public void OnDrag(PointerEventData e)
    {
        if (ghost != null) ghost.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        DestroyGhost();
        if (owner != null) owner.TryMoveEquipped(this, e);
    }

    /// <summary>드래그 고스트 생성 — 최상위 캔버스에 임시 표시, 레이캐스트는 통과</summary>
    void CreateGhost(Vector2 position)
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null || !HasItem) return;

        var go = new GameObject("EquipDragGhost", typeof(Image));
        go.transform.SetParent(parentCanvas.rootCanvas.transform, false);
        var img = go.GetComponent<Image>();
        img.color = isWeaponSlot ? WeaponFilledColor : FilledColor;
        img.raycastTarget = false;
        if (frame != null)
        {
            img.sprite = frame.sprite;
            img.type = frame.type;
        }

        ghost = go.GetComponent<RectTransform>();
        ghost.sizeDelta = ((RectTransform)transform).rect.size;
        ghost.position = position;

        var textGO = new GameObject("Label", typeof(Text));
        textGO.transform.SetParent(go.transform, false);
        var t = textGO.GetComponent<Text>();
        if (label != null)
        {
            t.font = label.font;
            t.fontSize = label.fontSize;
            t.fontStyle = label.fontStyle;
        }
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        t.text = Item != null ? Item.displayName : "";
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var over = go.AddComponent<Canvas>();
        over.overrideSorting = true;
        over.sortingOrder = 1000; // 모든 UI 위에 표시
    }

    void DestroyGhost()
    {
        if (ghost != null)
        {
            Destroy(ghost.gameObject);
            ghost = null;
        }
    }
}