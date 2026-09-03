using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 영웅 관리 패널 (영입 스펙 v1 + 상세 가독성 개편).
/// 보유 영웅(HeroRoster, 최대 8명) 목록 → 선택 → 상세 + [해고] (환급 없음).
/// 상세는 영입 카드와 동일 문법: 라벨 회색 소자 / 값 크게 / 특성·장비 이름 강조색.
/// 레벨은 "Lv.n / 최대" 표기 (최대 레벨 노출은 상세에서만 — 영입 카드는 Lv만).
/// UI 구성/연결은 LobbySceneBuilder가 담당.
/// </summary>
public class HeroManagePanel : MonoBehaviour
{
    [System.Serializable]
    public class DetailView
    {
        public Text nameText;                    // "브란  Lv.1 / 10" (볼드)
        public Text[] statValues = new Text[4];  // HP / 공격 / 치확 / 치피 값
        public Text[] statSubs = new Text[4];    // 보조줄 (최대치 — 치확/치피는 빈칸)
        public Text activeText;                  // 액티브 이름 + 조건
        public Text traitText;                   // 특성 이름(금색) + 설명(회색)
        public Text weaponText;                  // 장착 무기
        public Text equipText;                   // 장착 장비 목록 (최대 3줄)
    }

    [Tooltip("데이터 소스 (비워두면 자동 탐색)")]
    public LobbyController lobby;

    [Header("UI 연결 (빌더가 자동 연결)")]
    public Transform listRoot;
    public GameObject entryTemplate; // 비활성 템플릿 (Button + Text)
    public DetailView detail = new DetailView();
    public Button dismissButton;     // [해고] — 선택된 영웅 있을 때만 활성

    const string SubColor = "#8f9bb3";   // 보조 정보 회색
    const string TraitColor = "#f2cc66"; // 특성 이름 금색
    const string WarnColor = "#e08a8a";  // 경고 (무기 미장착)

    readonly List<GameObject> spawnedEntries = new List<GameObject>();
    OwnedHero selectedHero;

    public void Open()
    {
        if (lobby == null)
            lobby = Object.FindFirstObjectByType<LobbyController>();

        gameObject.SetActive(true);
        selectedHero = null;
        RebuildList();
        RefreshDetail();

        // 스크롤이 적용되어 있으면 열 때 맨 위로
        var scroll = listRoot != null ? listRoot.GetComponentInParent<ScrollRect>(true) : null;
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>[해고] 버튼 (빌더가 연결) — 환급 없음 (영입 스펙 v1). 장비는 보관소 회수.</summary>
    public void OnClickDismiss()
    {
        if (selectedHero == null) return;
        if (!HeroRoster.Dismiss(selectedHero)) return;

        selectedHero = null;
        RebuildList();
        RefreshDetail();
        // 참고: 로비를 배회 중인 배우(LobbyHeroActor)는 씬 재진입 시 갱신됨 (연출 전용)
    }

    // ---------- 내부 ----------

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
            OwnedHero captured = hero; // 클로저 캡처
            if (button != null)
                button.onClick.AddListener(() => Select(captured));
        }
    }

    void Select(OwnedHero hero)
    {
        selectedHero = hero;
        RefreshDetail();
    }

    void RefreshDetail()
    {
        if (dismissButton != null)
            dismissButton.interactable = selectedHero != null;

        var hero = selectedHero;
        if (hero == null)
        {
            Set(detail.nameText, $"영웅을 선택하세요   <color={SubColor}><size=22>보유 {HeroRoster.Heroes.Count} / {HeroRoster.MaxRoster}</size></color>");
            for (int i = 0; i < 4; i++) { Set(detail.statValues[i], "-"); Set(detail.statSubs[i], ""); }
            Set(detail.activeText, "-");
            Set(detail.traitText, "-");
            Set(detail.weaponText, "-");
            Set(detail.equipText, "-");
            return;
        }

        string name = hero.definition != null ? hero.definition.displayName : hero.heroId;
        Set(detail.nameText, $"{name}  Lv.{hero.level} <color={SubColor}><size=24>/ {OwnedHero.MaxLevel}</size></color>");

        Set(detail.statValues[0], $"{hero.MaxHP:0}");
        Set(detail.statSubs[0], $"최대 {hero.stats.hpLv10:0}");
        Set(detail.statValues[1], $"{hero.Attack:0.#}");
        Set(detail.statSubs[1], $"최대 {hero.stats.attackLv10:0.#}");
        Set(detail.statValues[2], $"{hero.CritChance:0.#}%");
        Set(detail.statSubs[2], "");
        Set(detail.statValues[3], $"{hero.CritDamage:0}%");
        Set(detail.statSubs[3], "");

        if (hero.activeSkill != null)
        {
            string mode = hero.activeSkill.activation == SkillActivation.OnRelease ? "내려놓기" : "자동";
            Set(detail.activeText,
                $"<b>{hero.activeSkill.displayName}</b>   <color={SubColor}>{mode} · " +
                $"{HeroInfoText.WeaponReqKorean(hero.activeSkill.weaponRequirement)} · 쿨 {hero.activeSkill.cooldown:0}초</color>");
        }
        else Set(detail.activeText, "-");

        string traitName = TraitCatalog.DisplayName(hero.traitId);
        Set(detail.traitText, string.IsNullOrEmpty(traitName)
            ? "-"
            : $"<color={TraitColor}><b>{traitName}</b></color>   <color={SubColor}>{TraitCatalog.Description(hero.traitId)}</color>");

        Set(detail.weaponText, hero.weapon != null
            ? hero.weapon.displayName
            : $"<color={WarnColor}>미장착 — 기본 공격 불가</color>");

        if (hero.equipment.Count == 0)
        {
            Set(detail.equipText, $"<color={SubColor}>없음 (0 / {HeroRunInstance.MaxEquipSlots})</color>");
        }
        else
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < hero.equipment.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(hero.equipment[i] != null ? hero.equipment[i].displayName : "-");
            }
            Set(detail.equipText, sb.ToString());
        }
    }

    static void Set(Text t, string value)
    {
        if (t != null) t.text = value;
    }
}