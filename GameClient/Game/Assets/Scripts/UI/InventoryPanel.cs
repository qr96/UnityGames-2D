using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    [Header("목록 UI (빌더가 자동 연결) — 고정 격자 폐기, 보관 장비 전량 표시")]
    public Transform listRoot;       // ScrollRect의 Content
    public GameObject rowTemplate;   // 비활성 행 템플릿 (EquipmentSlot 포함)

    readonly List<GameObject> spawnedRows = new List<GameObject>();
    readonly List<EquipmentDefinition> displayed = new List<EquipmentDefinition>();
    static readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    Camera cam;

    void OnEnable()
    {
        Refresh();
    }

    /// <summary>
    /// 배치 진입/장비 변동 시 호출 — 보관 장비 '전량'을 목록 행으로 그림.
    /// (구 8칸 고정 격자는 9번째 아이템부터 표시가 잘리는 결함 — 무기 미표시의 원인)
    /// 최신 획득이 맨 위.
    /// </summary>
    public void Refresh()
    {
        foreach (var go in spawnedRows)
            if (go != null) Destroy(go);
        spawnedRows.Clear();
        displayed.Clear();

        RunState run = RunManager.Instance != null ? RunManager.Instance.Run : null;
        if (run == null || listRoot == null || rowTemplate == null) return;

        // 동일 인스턴스 집계 (생성 장비는 전부 개별이라 대부분 x1) — 최신 획득이 위로
        var counts = new Dictionary<EquipmentDefinition, int>();
        for (int i = run.inventory.Count - 1; i >= 0; i--)
        {
            var item = run.inventory[i];
            if (item == null) continue;
            if (counts.TryGetValue(item, out int c)) counts[item] = c + 1;
            else { counts[item] = 1; displayed.Add(item); }
        }

        for (int i = 0; i < displayed.Count; i++)
        {
            GameObject row = Instantiate(rowTemplate, rowTemplate.transform.parent);
            row.SetActive(true);
            spawnedRows.Add(row);

            var slot = row.GetComponent<EquipmentSlot>();
            if (slot != null)
            {
                slot.panel = this;
                slot.index = i;
                slot.SetItem(displayed[i], counts[displayed[i]]);
            }
        }

        var scroll = listRoot.GetComponentInParent<ScrollRect>(true);
        if (scroll != null) scroll.verticalNormalizedPosition = 1f; // 열 때 맨 위 (최신)
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
        // 무기칸엔 무기만 — 일반 장비를 무기칸에 놓으면 거부 (무기 스펙 v2)
        HeroEquipSlotUI slotUI = FindHeroSlotUnderPointer(e);
        if (slotUI != null && slotUI.Hero != null)
        {
            if (slotUI.isWeaponSlot && !(item is WeaponDefinition))
                return false; // 타입 불일치 → 제자리 복귀
            equipped = run.EquipAt(slotUI.Hero, item, slotUI.SlotIndex); // 무기는 내부에서 무기칸으로 자동 라우팅
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