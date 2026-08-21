using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 전투 준비(배치) 화면의 인벤토리 패널 — [장비] 탭.
/// 보유(미장착) 장비 전체를 종류별로 집계해 표시 (동일 장비는 xN).
/// 드롭 판정 (우선순위):
///   1) 파티 장비 패널의 영웅 장비칸(HeroEquipSlotUI) 위 → 그 칸에 장착, 점유 칸이면 교체
///   2) 전장의 영웅 몸통 근처 → 빈 칸에 장착
/// 전투 중에는 표시되지 않음 (GDD 8: 전투 중 장비 변경 불가).
/// </summary>
public class InventoryPanel : MonoBehaviour
{
    [Tooltip("파티 장비 패널 (장착 후 표시 갱신용)")]
    public PartyEquipPanel partyPanel;

    [Tooltip("전장의 영웅 몸통에 직접 드롭할 때의 판정 반경")]
    public float heroDropRadius = 1.1f;

    readonly List<EquipmentSlot> slots = new List<EquipmentSlot>();
    readonly List<EquipmentDefinition> displayed = new List<EquipmentDefinition>();
    static readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    Camera cam;

    void Awake()
    {
        // 에디터에서 만든 자식 슬롯을 자동 수집 — 하이어라키 순서 = 슬롯 순서
        slots.Clear();
        var found = GetComponentsInChildren<EquipmentSlot>(true);
        for (int i = 0; i < found.Length; i++)
        {
            found[i].panel = this;
            found[i].index = i;
            slots.Add(found[i]);
        }
    }

    void OnEnable()
    {
        Refresh();
    }

    /// <summary>배치 진입/장비 변동 시 호출 — 인벤토리 전체를 종류별 집계로 다시 그림</summary>
    public void Refresh()
    {
        RunState run = RunManager.Instance != null ? RunManager.Instance.Run : null;

        displayed.Clear();
        var counts = new Dictionary<EquipmentDefinition, int>();
        if (run != null)
        {
            foreach (var item in run.inventory)
            {
                if (item == null) continue;
                if (counts.TryGetValue(item, out int c)) counts[item] = c + 1;
                else { counts[item] = 1; displayed.Add(item); } // 첫 등장 순서 유지
            }
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < displayed.Count) slots[i].SetItem(displayed[i], counts[displayed[i]]);
            else slots[i].SetItem(null, 0);
        }
    }

    /// <summary>슬롯의 장비를 드롭. 성공 시 true (EquipmentSlot이 드래그 종료 시 호출).</summary>
    public bool TryEquipAt(int slotIndex, PointerEventData e)
    {
        if (slotIndex < 0 || slotIndex >= displayed.Count) return false;

        RunState run = RunManager.Instance.Run;
        EquipmentDefinition item = displayed[slotIndex];

        bool equipped = false;
        HeroRunInstance target = null;

        // 1순위: UI의 영웅 장비칸 위에 드롭 (점유 칸이면 기존 장비 탈착·교체)
        HeroEquipSlotUI slotUI = FindHeroSlotUnderPointer(e);
        if (slotUI != null && slotUI.Hero != null)
        {
            equipped = run.EquipAt(slotUI.Hero, item, slotUI.SlotIndex);
            target = slotUI.Hero;
        }
        // 2순위: 전장의 영웅 몸통 근처에 드롭 → 빈 칸에 장착
        else
        {
            Hero bodyHero = FindHeroAt(ScreenToWorld(e.position));
            if (bodyHero != null && bodyHero.Runtime != null)
            {
                equipped = run.Equip(bodyHero.Runtime, item);
                target = bodyHero.Runtime;
            }
        }

        if (!equipped) return false; // 대상 없음 / 칸 가득참 / 전투 중 → 아이템 제자리로

        // 준비 단계 장착은 즉시 스탯 반영 (전투 시작 전이므로 안전)
        Hero unit = FindHeroUnit(target);
        if (unit != null) unit.Init(target);

        if (partyPanel != null) partyPanel.Refresh();
        Refresh(); // 개수/목록 갱신 (교체로 회수된 장비도 다시 표시됨)
        return true;
    }

    /// <summary>드롭 지점의 UI 레이캐스트로 영웅 장비칸 탐색</summary>
    HeroEquipSlotUI FindHeroSlotUnderPointer(PointerEventData e)
    {
        if (EventSystem.current == null) return null;
        raycastResults.Clear();
        EventSystem.current.RaycastAll(e, raycastResults);
        foreach (var r in raycastResults)
        {
            var slot = r.gameObject.GetComponentInParent<HeroEquipSlotUI>();
            if (slot != null) return slot;
        }
        return null;
    }

    Hero FindHeroAt(Vector3 world)
    {
        Hero best = null;
        float bestDist = heroDropRadius;
        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
        {
            if (!(u is Hero h)) continue;
            float d = Vector2.Distance(u.transform.position, world);
            if (d <= bestDist)
            {
                bestDist = d;
                best = h;
            }
        }
        return best;
    }

    Hero FindHeroUnit(HeroRunInstance inst)
    {
        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
            if (u is Hero h && h.Runtime == inst) return h;
        return null;
    }

    Vector3 ScreenToWorld(Vector2 screenPos)
    {
        if (cam == null) cam = Camera.main;
        Vector3 w = cam != null ? cam.ScreenToWorldPoint((Vector3)screenPos) : Vector3.zero;
        w.z = 0f;
        return w;
    }
}