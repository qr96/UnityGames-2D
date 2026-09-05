using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 영웅 관리 패널 (장비 관리 개편 ①) — 로비 장비 관리의 본진.
/// 구조: 보유 영웅 목록 → 공통 헤더(이름/요약) → 탭 2개
///   [정보] 스탯/액티브/특성 + 장착 요약 (영입 카드와 동일 문법)
///   [장비] 장착 슬롯 줄(무기+자유3, 축약명) + 상세 줄 + 보관소 목록
/// 조작: 드래그 — 보관소 행 → 슬롯(빈=장착, 점유=그 칸과 교체), 슬롯 → 보관소=해제.
///   탭(클릭)은 정보 표시 전용 (상세 줄에 전체 이름).
/// 로비 장착 변경은 즉시 영구 반영 (EquipService — 장비 영속 v1).
/// UI 구성/연결은 LobbySceneBuilder가 담당.
/// </summary>
public class HeroManagePanel : MonoBehaviour
{
    [System.Serializable]
    public class InfoView
    {
        public Text[] statValues = new Text[4];
        public Text[] statSubs = new Text[4];
        public Text activeText;
        public Text traitText;
        public Text equipSummaryText; // "검 · 장비 1 / 3" — 상세는 장비 탭
    }

    [Tooltip("데이터 소스 (비워두면 자동 탐색)")]
    public LobbyController lobby;

    [Header("공통 (빌더가 자동 연결)")]
    public Transform listRoot;
    public GameObject entryTemplate;
    public Text headerText;          // "브란  Lv.1 / 10   HP 115 · 공격 9"
    public Button infoTabButton;
    public Button equipTabButton;
    public GameObject infoRoot;
    public GameObject equipRoot;
    public Button dismissButton;

    [Header("[정보] 탭")]
    public InfoView info = new InfoView();

    [Header("[장비] 탭")]
    public LobbyEquipSlotUI[] equipSlots = new LobbyEquipSlotUI[4]; // [0]=무기
    public RectTransform slotStripArea; // 슬롯 줄 배경 — 이 영역 아무 데나 드롭 = 스마트 장착
    public Text detailLine;          // 선택/드래그 결과 안내 (전체 이름)
    public Text storageTitle;        // "보관소 (n)"
    public Transform storageListRoot;
    public GameObject storageRowTemplate;

    const string SubColor = "#8f9bb3";
    const string TraitColor = "#f2cc66";
    const string WarnColor = "#e08a8a";

    static readonly Color TabOn = new Color(0.30f, 0.45f, 0.85f, 1f);
    static readonly Color TabOff = new Color(0.14f, 0.16f, 0.24f, 1f);

    readonly List<GameObject> spawnedEntries = new List<GameObject>();
    readonly List<GameObject> spawnedRows = new List<GameObject>();
    static readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    public OwnedHero SelectedHero { get; private set; }

    public void Open()
    {
        if (lobby == null)
            lobby = Object.FindFirstObjectByType<LobbyController>();

        gameObject.SetActive(true);
        SelectedHero = null;
        OpenTab(0);
        RebuildList();
        RefreshAll();

        var scroll = listRoot != null ? listRoot.GetComponentInParent<ScrollRect>(true) : null;
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    /// <summary>[보관소] 버튼 진입점 — 장비 탭을 바로 연다</summary>
    public void OpenEquipTab()
    {
        Open();
        OpenTab(1);
    }

    public void Close() => gameObject.SetActive(false);

    // ---------- 탭 ----------

    public void OnClickInfoTab() => OpenTab(0);
    public void OnClickEquipTab() => OpenTab(1);

    void OpenTab(int index)
    {
        if (infoRoot != null) infoRoot.SetActive(index == 0);
        if (equipRoot != null) equipRoot.SetActive(index == 1);
        if (infoTabButton != null) infoTabButton.image.color = index == 0 ? TabOn : TabOff;
        if (equipTabButton != null) equipTabButton.image.color = index == 1 ? TabOn : TabOff;
    }

    // ---------- 해고 ----------

    public void OnClickDismiss()
    {
        if (SelectedHero == null) return;
        if (!HeroRoster.Dismiss(SelectedHero)) return; // 장비는 보관소 회수
        SelectedHero = null;
        RebuildList();
        RefreshAll();
    }

    // ---------- 목록 ----------

    void RebuildList()
    {
        foreach (var go in spawnedEntries)
            if (go != null) Destroy(go);
        spawnedEntries.Clear();

        if (listRoot == null || entryTemplate == null) return;

        foreach (OwnedHero hero in HeroRoster.Heroes)
        {
            GameObject entry = Instantiate(entryTemplate, entryTemplate.transform.parent);
            entry.name = $"Entry_{hero.heroId}";
            entry.SetActive(true);
            spawnedEntries.Add(entry);

            Text label = entry.GetComponentInChildren<Text>(true);
            if (label != null) label.text = HeroInfoText.ListLabel(hero);

            Button button = entry.GetComponent<Button>();
            OwnedHero captured = hero;
            if (button != null)
                button.onClick.AddListener(() => Select(captured));
        }
    }

    void Select(OwnedHero hero)
    {
        SelectedHero = hero;
        RefreshAll();
    }

    // ---------- 갱신 ----------

    void RefreshAll()
    {
        var hero = SelectedHero;
        if (dismissButton != null) dismissButton.interactable = hero != null;

        // 헤더
        if (headerText != null)
        {
            if (hero == null)
            {
                headerText.text = $"영웅을 선택하세요   <color={SubColor}><size=24>보유 {HeroRoster.Heroes.Count} / {HeroRoster.MaxRoster}</size></color>";
            }
            else
            {
                string name = hero.definition != null ? hero.definition.displayName : hero.heroId;
                headerText.text = $"<b>{name}</b>  Lv.{hero.level} <color={SubColor}><size=22>/ {OwnedHero.MaxLevel}</size></color>" +
                    $"    <color={SubColor}>HP {hero.MaxHP:0} · 공격 {hero.Attack:0.#}</color>";
            }
        }

        RefreshInfoTab(hero);
        RefreshEquipTab(hero);
    }

    void RefreshInfoTab(OwnedHero hero)
    {
        if (hero == null)
        {
            for (int i = 0; i < 4; i++) { Set(info.statValues[i], "-"); Set(info.statSubs[i], ""); }
            Set(info.activeText, "-");
            Set(info.traitText, "-");
            Set(info.equipSummaryText, "-");
            return;
        }

        Set(info.statValues[0], $"{hero.MaxHP:0}");
        Set(info.statSubs[0], $"최대 {hero.stats.hpLv10:0}");
        Set(info.statValues[1], $"{hero.Attack:0.#}");
        Set(info.statSubs[1], $"최대 {hero.stats.attackLv10:0.#}");
        Set(info.statValues[2], $"{hero.CritChance:0.#}%");
        Set(info.statSubs[2], "");
        Set(info.statValues[3], $"{hero.CritDamage:0}%");
        Set(info.statSubs[3], "");

        if (hero.activeSkill != null)
        {
            string mode = hero.activeSkill.activation == SkillActivation.OnRelease ? "내려놓기" : "자동";
            Set(info.activeText,
                $"<b>{hero.activeSkill.displayName}</b>   <color={SubColor}>{mode} · " +
                $"{HeroInfoText.WeaponReqKorean(hero.activeSkill.weaponRequirement)} · 쿨 {hero.activeSkill.cooldown:0}초</color>");
        }
        else Set(info.activeText, "-");

        string traitName = TraitCatalog.DisplayName(hero.traitId);
        Set(info.traitText, string.IsNullOrEmpty(traitName)
            ? "-"
            : $"<color={TraitColor}><b>{traitName}</b></color>   <color={SubColor}>{TraitCatalog.Description(hero.traitId)}</color>");

        string weaponPart = hero.weapon != null
            ? EquipmentGenerator.ShortName(hero.weapon)
            : $"<color={WarnColor}>무기 없음</color>";
        Set(info.equipSummaryText,
            $"{weaponPart} <color={SubColor}>· 장비 {hero.equipment.Count} / {HeroRunInstance.MaxEquipSlots} — 자세한 내용은 [장비] 탭</color>");
    }

    void RefreshEquipTab(OwnedHero hero)
    {
        foreach (var slot in equipSlots)
            if (slot != null) { slot.owner = this; slot.RefreshView(); }

        // 보관소 목록 (최신 획득 위)
        foreach (var go in spawnedRows)
            if (go != null) Destroy(go);
        spawnedRows.Clear();

        var items = Armory.Items;
        if (storageTitle != null) storageTitle.text = $"보관소  ({items.Count})";

        if (storageListRoot != null && storageRowTemplate != null)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i] == null) continue;
                GameObject row = Instantiate(storageRowTemplate, storageRowTemplate.transform.parent);
                row.SetActive(true);
                spawnedRows.Add(row);

                var rowUI = row.GetComponent<LobbyStorageRowUI>();
                if (rowUI != null)
                {
                    rowUI.owner = this;
                    rowUI.Bind(items[i]);
                }
            }
        }

        ShowDetailLine(hero == null ? "영웅을 선택하세요" : "");
    }

    // ---------- 장비 탭 상호작용 (슬롯/행이 호출) ----------

    public void ShowDetailLine(string text)
    {
        if (detailLine != null) detailLine.text = text;
    }

    public void OnSlotClicked(LobbyEquipSlotUI slot)
    {
        var item = slot != null ? slot.Item : null;
        ShowDetailLine(item != null ? item.displayName : "- 비어 있음 -");
    }

    /// <summary>보관소 행 드래그 종료 — 슬롯 위: 장착/교체 (무기칸엔 무기만)</summary>
    public void OnRowDragEnd(LobbyStorageRowUI row, PointerEventData e)
    {
        if (row == null || row.item == null) return;
        if (SelectedHero == null)
        {
            ShowDetailLine("영웅을 먼저 선택하세요");
            return;
        }

        string itemName = row.item.displayName; // Refresh로 행이 파괴되기 전에 확보
        bool ok = false;
        string fail = null;

        // 1) 특정 슬롯 위: 그 칸에 장착/교체 (정밀 조준은 '교체할 칸 지정'에만 필요)
        LobbyEquipSlotUI slot = FindUnderPointer<LobbyEquipSlotUI>(e);
        if (slot != null)
        {
            if (row.item is WeaponDefinition weapon)
            {
                ok = slot.isWeaponSlot && EquipService.EquipWeapon(SelectedHero, weapon);
                if (!ok) fail = "무기는 무기칸에만 장착할 수 있습니다";
            }
            else
            {
                ok = !slot.isWeaponSlot && EquipService.EquipGearAt(SelectedHero, row.item, slot.slotIndex);
                if (!ok) fail = "장비는 자유칸에만 장착할 수 있습니다";
            }
        }
        // 2) 슬롯 줄 영역 아무 데나: 스마트 장착 — 무기는 무기칸(교체), 장비는 빈 칸 (드롭 정밀도 부담 제거)
        else if (slotStripArea != null &&
                 RectTransformUtility.RectangleContainsScreenPoint(slotStripArea, e.position, e.pressEventCamera))
        {
            if (row.item is WeaponDefinition weapon)
                ok = EquipService.EquipWeapon(SelectedHero, weapon);
            else
            {
                ok = EquipService.EquipGearAt(SelectedHero, row.item, int.MaxValue); // 빈 칸에 추가
                if (!ok) fail = "장비칸이 가득 — 교체할 칸 위에 놓아주세요";
            }
        }
        else return; // 슬롯 줄 밖 — 아무 일 없음

        if (ok)
        {
            RefreshAll();
            ShowDetailLine(itemName); // 상세 줄은 정보 전용 (접두어 없이 전체 이름)
        }
        else if (fail != null)
        {
            ShowDetailLine(fail);
        }
    }

    /// <summary>장착 슬롯 드래그 종료 — 보관소 목록 위: 해제</summary>
    public void OnSlotDragEnd(LobbyEquipSlotUI slot, PointerEventData e)
    {
        if (slot == null || slot.Item == null || SelectedHero == null) return;

        // 보관소 영역 위인지 판정 (스크롤/행 어디든)
        bool overStorage = false;
        if (EventSystem.current != null && storageListRoot != null)
        {
            raycastResults.Clear();
            EventSystem.current.RaycastAll(e, raycastResults);
            foreach (var r in raycastResults)
            {
                if (r.gameObject.transform.IsChildOf(storageListRoot.parent.parent)) // Scroll 루트 기준
                {
                    overStorage = true;
                    break;
                }
            }
        }
        if (!overStorage) return;

        string name = slot.Item.displayName;
        bool ok = slot.isWeaponSlot
            ? EquipService.UnequipWeapon(SelectedHero)
            : EquipService.UnequipGear(SelectedHero, slot.slotIndex);

        if (ok)
        {
            RefreshAll();
            ShowDetailLine($"{name} — 보관소로 이동");
        }
    }

    T FindUnderPointer<T>(PointerEventData e) where T : Component
    {
        if (EventSystem.current == null) return null;
        raycastResults.Clear();
        EventSystem.current.RaycastAll(e, raycastResults);
        foreach (var r in raycastResults)
        {
            var c = r.gameObject.GetComponentInParent<T>();
            if (c != null) return c;
        }
        return null;
    }

    static void Set(Text t, string value)
    {
        if (t != null) t.text = value;
    }
}