using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 영웅 관리 패널 (영입 스펙 v1: 로스터 기반).
/// 보유 영웅(HeroRoster, 최대 8명) 목록 → 선택 → 상세(정보 전부 공개) + [해고] (환급 없음).
/// 목록 항목은 entryTemplate(씬 오브젝트) 복제 → 스타일은 에디터에서 수정 가능.
/// </summary>
public class HeroManagePanel : MonoBehaviour
{
    [Tooltip("데이터 소스 (비워두면 자동 탐색)")]
    public LobbyController lobby;

    [Header("UI 연결 (빌더가 자동 연결)")]
    public Transform listRoot;
    public GameObject entryTemplate; // 비활성 템플릿 (Button + Text)
    public Text detailText;
    public Button dismissButton;     // [해고] — 선택된 영웅 있을 때만 활성

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

    /// <summary>[해고] 버튼 (빌더가 연결) — 환급 없음 (영입 스펙 v1)</summary>
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
        if (detailText != null)
            detailText.text = selectedHero != null
                ? HeroInfoText.Build(selectedHero)
                : $"영웅을 선택하세요.  (보유 {HeroRoster.Heroes.Count} / {HeroRoster.MaxRoster})";

        if (dismissButton != null)
            dismissButton.interactable = selectedHero != null;
    }
}