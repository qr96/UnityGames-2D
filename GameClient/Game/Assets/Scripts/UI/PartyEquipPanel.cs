using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 전투 준비 화면의 파티 장비 패널.
/// 파티원마다 [이름 + 장비 슬롯 3칸] 항목을 표시 — 항목은 entryTemplate을 복제해 생성.
/// 템플릿은 씬 오브젝트라서 스타일(크기/색/배치)을 에디터에서 자유롭게 수정 가능.
///
/// 장착된 장비 드래그 처리 (HeroEquipSlotUI가 호출):
///   1) 다른 영웅의 장비칸에 드롭 → 이전 (점유 칸이면 서로 교환)
///   2) 인벤토리 패널 위에 드롭 → 탈착 (인벤토리로 복귀)
///   3) 전장의 영웅 몸통에 드롭 → 그 영웅의 빈 칸으로 이전
/// </summary>
public class PartyEquipPanel : MonoBehaviour
{
    [Tooltip("파티원 1명 표시용 템플릿 (비활성 상태로 자식에 배치)")]
    public GameObject entryTemplate;

    [Tooltip("탈착 시 갱신할 인벤토리 패널 (비워두면 자동 탐색)")]
    public InventoryPanel inventoryPanel;

    [Tooltip("전장의 영웅 몸통에 직접 드롭할 때의 판정 반경")]
    public float heroDropRadius = 1.1f;

    readonly List<GameObject> spawned = new List<GameObject>();
    static readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    Camera cam;

    void OnEnable()
    {
        Refresh();
    }

    /// <summary>파티/장비 변동 시 호출 — 항목 전체 재구성 (파티 최대 5명이라 부담 없음)</summary>
    public void Refresh()
    {
        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();

        RunState run = RunManager.Instance != null ? RunManager.Instance.Run : null;
        if (run == null || entryTemplate == null) return;

        foreach (HeroRunInstance heroInst in run.party)
        {
            GameObject entry = Instantiate(entryTemplate, entryTemplate.transform.parent);
            entry.name = $"Entry_{heroInst.definition.displayName}";
            entry.SetActive(true);
            spawned.Add(entry);

            // 이름 표시 (템플릿의 "Name" Text)
            Transform nameT = entry.transform.Find("Name");
            Text nameText = nameT != null ? nameT.GetComponent<Text>() : null;
            if (nameText != null)
            {
                nameText.text = heroInst.definition.displayName;
                nameText.color = heroInst.definition.color;
            }

            // 슬롯 바인딩 — 무기 슬롯(isWeaponSlot)과 자유 슬롯(하이어라키 순서 = 0,1,2) 분리
            HeroEquipSlotUI[] slotUIs = entry.GetComponentsInChildren<HeroEquipSlotUI>(true);
            int freeIndex = 0;
            foreach (HeroEquipSlotUI slotUI in slotUIs)
            {
                slotUI.owner = this;
                if (slotUI.isWeaponSlot)
                    slotUI.Bind(heroInst, 0);
                else if (freeIndex < HeroRunInstance.MaxEquipSlots)
                    slotUI.Bind(heroInst, freeIndex++);
            }
        }
    }

    /// <summary>장착된 장비의 드롭 처리 (HeroEquipSlotUI.OnEndDrag가 호출). 성공 시 true.</summary>
    public bool TryMoveEquipped(HeroEquipSlotUI fromSlot, PointerEventData e)
    {
        RunState run = RunManager.Instance != null ? RunManager.Instance.Run : null;
        if (run == null || fromSlot == null || !fromSlot.HasItem) return false;

        HeroRunInstance fromHero = fromSlot.Hero;
        int fromIndex = fromSlot.SlotIndex;

        // ---- 드롭 지점 판정 (UI 레이캐스트 우선) ----
        HeroEquipSlotUI targetSlot = null;
        bool overInventory = false;

        if (EventSystem.current != null)
        {
            raycastResults.Clear();
            EventSystem.current.RaycastAll(e, raycastResults);
            foreach (var r in raycastResults)
            {
                if (targetSlot == null)
                {
                    var s = r.gameObject.GetComponentInParent<HeroEquipSlotUI>();
                    if (s != null && s != fromSlot) { targetSlot = s; continue; }
                }
                if (r.gameObject.GetComponentInParent<InventoryPanel>() != null)
                    overInventory = true;
            }
        }

        bool moved = false;
        HeroRunInstance otherHero = null;
        bool fromWeapon = fromSlot.isWeaponSlot;

        if (targetSlot != null && targetSlot.Hero != null)
        {
            // 1) 다른 장비칸 — 무기는 무기칸끼리만, 일반 장비는 자유칸끼리만 (무기 스펙 v2)
            if (fromWeapon && targetSlot.isWeaponSlot)
            {
                moved = run.MoveWeapon(fromHero, targetSlot.Hero); // 교환 (빈 칸이면 이전)
                otherHero = targetSlot.Hero;
            }
            else if (!fromWeapon && !targetSlot.isWeaponSlot)
            {
                moved = run.MoveEquipped(fromHero, fromIndex, targetSlot.Hero, targetSlot.SlotIndex);
                otherHero = targetSlot.Hero;
            }
            // 타입 불일치 → moved=false → 제자리 복귀
        }
        else if (overInventory)
        {
            // 2) 인벤토리 위 → 탈착 (무기 탈착 = 기본 공격 불가 상태 허용)
            moved = fromWeapon ? run.UnequipWeapon(fromHero) : run.Unequip(fromHero, fromIndex);
        }
        else
        {
            // 3) 전장의 영웅 몸통 → 무기는 그 영웅의 무기칸으로(교환), 장비는 빈 자유칸으로
            Hero body = FindHeroAt(ScreenToWorld(e.position));
            if (body != null && body.Runtime != null && body.Runtime != fromHero)
            {
                moved = fromWeapon
                    ? run.MoveWeapon(fromHero, body.Runtime)
                    : run.MoveEquipped(fromHero, fromIndex, body.Runtime, body.Runtime.equipment.Count);
                otherHero = body.Runtime;
            }
        }

        if (!moved)
        {
            fromSlot.RefreshView(); // 실패 → 제자리 표시 복구
            return false;
        }

        // 스탯 즉시 재계산 (준비 단계이므로 안전)
        ReinitHeroUnit(fromHero);
        if (otherHero != null && otherHero != fromHero) ReinitHeroUnit(otherHero);

        Refresh();
        RefreshInventory();
        return true;
    }

    // ---------- 내부 헬퍼 ----------

    void ReinitHeroUnit(HeroRunInstance inst)
    {
        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
        {
            if (u is Hero h && h.Runtime == inst)
            {
                h.Init(inst);
                return;
            }
        }
    }

    void RefreshInventory()
    {
        if (inventoryPanel == null)
            inventoryPanel = Object.FindFirstObjectByType<InventoryPanel>(FindObjectsInactive.Include);
        if (inventoryPanel != null && inventoryPanel.gameObject.activeInHierarchy)
            inventoryPanel.Refresh();
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

    Vector3 ScreenToWorld(Vector2 screenPos)
    {
        if (cam == null) cam = Camera.main;
        Vector3 w = cam != null ? cam.ScreenToWorldPoint((Vector3)screenPos) : Vector3.zero;
        w.z = 0f;
        return w;
    }
}