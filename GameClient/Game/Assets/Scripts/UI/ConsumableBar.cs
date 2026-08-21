using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 전투 중 하단 소모품 바 (4칸). 보유 소모품이 '오른쪽부터' 채워짐.
/// 현재 소모품은 포션 1종 — 전투당 지급 개수만큼 칸에 표시 (GDD 4).
///
/// 사용 규칙:
///  - 전장에 던지면 무조건 소모 — 범위 안의 영웅만 회복 (없으면 낭비)
///  - 바(슬롯) 위에 도로 놓는 경우에만 복구 (소모 없음)
///  - 드래그 중 범위 원 표시: 초록 = 회복 대상 있음 / 회색 = 이대로 던지면 낭비 (경고용)
///
/// 초기화 주의: 이 오브젝트는 전투 중에만 활성화되므로, 비활성 상태에서
/// SetPotions가 먼저 호출될 수 있음 → OnEnable에서 슬롯 수집/표시를 항상 보정.
/// </summary>
public class ConsumableBar : MonoBehaviour
{
    [Header("포션 효과 (GDD 4 — 수치 미정, 튜닝값)")]
    public float healAmount = 40f;
    public float healRadius = 1.8f;

    static readonly Color RangeOkColor = new Color(0.45f, 1f, 0.6f, 0.28f);
    static readonly Color RangeNoTargetColor = new Color(0.7f, 0.7f, 0.7f, 0.16f);

    readonly List<ConsumableSlot> slots = new List<ConsumableSlot>();
    static readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    int potionCount;
    Camera cam;
    Transform rangeIndicator;
    SpriteRenderer rangeSprite;

    void Awake()
    {
        EnsureSlots();
    }

    void OnEnable()
    {
        EnsureSlots();
        Render(); // 비활성 중에 SetPotions가 호출됐어도 켜질 때 표시를 맞춤
    }

    void OnDisable()
    {
        HideRange();
    }

    /// <summary>자식 슬롯 자동 수집 — 하이어라키 순서 = 왼쪽→오른쪽</summary>
    void EnsureSlots()
    {
        if (slots.Count > 0) return;
        var found = GetComponentsInChildren<ConsumableSlot>(true);
        for (int i = 0; i < found.Length; i++)
        {
            found[i].bar = this;
            found[i].index = i;
            slots.Add(found[i]);
        }
    }

    /// <summary>전투 시작 시 지급 개수 설정 (BattleController가 호출)</summary>
    public void SetPotions(int count)
    {
        potionCount = count;
        Render();
    }

    /// <summary>슬롯의 포션 투척 시도. 사용됐을 때만 true (바에 도로 놓으면 취소).</summary>
    public bool TryUseAt(int slotIndex, PointerEventData e)
    {
        HideRange();
        if (potionCount <= 0 || !IsFilled(slotIndex)) return false;

        // 바(슬롯) 위에 도로 놓기 = 복구 (유일한 취소 경로)
        if (IsPointerOverBar(e)) return false;

        // 전장에 던지면 무조건 소모 — 범위 안의 영웅만 회복 (없으면 낭비)
        Vector3 world = ScreenToWorld(e.position);
        foreach (Unit u in GetHeroesInRange(world))
            u.Heal(healAmount);

        potionCount--;
        Render();
        return true;
    }

    // ---------- 투척 범위 표시 (드래그 중 ConsumableSlot이 호출) ----------

    public void ShowRange(Vector2 screenPos)
    {
        if (rangeIndicator == null) CreateRangeIndicator();

        Vector3 world = ScreenToWorld(screenPos);
        rangeIndicator.gameObject.SetActive(true);
        rangeIndicator.position = world;
        rangeIndicator.localScale = Vector3.one * (healRadius * 2f);

        if (rangeSprite != null)
            rangeSprite.color = GetHeroesInRange(world).Count > 0 ? RangeOkColor : RangeNoTargetColor;
    }

    public void HideRange()
    {
        if (rangeIndicator != null)
            rangeIndicator.gameObject.SetActive(false);
    }

    /// <summary>회복 범위 원 (자리표시자 — 아트 시 이 함수만 교체)</summary>
    void CreateRangeIndicator()
    {
        var go = new GameObject("PotionRangeIndicator");
        rangeSprite = UnitFactory.MakeVisual(go.transform, UnitFactory.Circle, RangeOkColor, 1f, sortingOrder: 3);
        rangeIndicator = go.transform;
    }

    // ---------- 내부 ----------

    List<Unit> GetHeroesInRange(Vector3 world)
    {
        var list = new List<Unit>();
        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
        {
            if (Vector2.Distance(u.transform.position, world) <= healRadius)
                list.Add(u);
        }
        return list;
    }

    bool IsPointerOverBar(PointerEventData e)
    {
        if (EventSystem.current == null) return false;
        raycastResults.Clear();
        EventSystem.current.RaycastAll(e, raycastResults);
        foreach (var r in raycastResults)
        {
            if (r.gameObject.GetComponentInParent<ConsumableBar>() == this)
                return true;
        }
        return false;
    }

    bool IsFilled(int slotIndex)
    {
        int shown = Mathf.Min(potionCount, slots.Count);
        return slotIndex >= slots.Count - shown;
    }

    /// <summary>오른쪽부터 채워서 표시</summary>
    void Render()
    {
        int shown = Mathf.Min(potionCount, slots.Count);
        for (int i = 0; i < slots.Count; i++)
            slots[i].SetFilled(i >= slots.Count - shown, "포션");
    }

    Vector3 ScreenToWorld(Vector2 screenPos)
    {
        if (cam == null) cam = Camera.main;
        Vector3 w = cam != null ? cam.ScreenToWorldPoint((Vector3)screenPos) : Vector3.zero;
        w.z = 0f;
        return w;
    }
}