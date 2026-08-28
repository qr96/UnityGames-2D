using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 출정 패널 (영입 스펙 v1: 로스터에서 1~5명 자유 선택 → 출발).
/// 보유 영웅(HeroRoster) 목록에서 탭으로 선택/해제 — 최소 1명, 최대 5명(MaxPartySize).
/// 출발 시 선택한 영웅의 heroId를 SortieData에 담고 게임 씬 로드.
/// </summary>
public class SortiePanel : MonoBehaviour
{
    [Tooltip("데이터 소스 (비워두면 자동 탐색)")]
    public LobbyController lobby;

    [Tooltip("게임(런) 씬 이름 — Build Settings에 등록되어 있어야 함")]
    public string gameSceneName = "Game";

    [Header("UI 연결 (빌더가 자동 연결)")]
    public Transform listRoot;
    public GameObject entryTemplate;
    public Text countText;
    public Button departButton;

    static readonly Color NormalColor = new Color(0.16f, 0.20f, 0.30f);
    static readonly Color SelectedColor = new Color(0.22f, 0.62f, 0.40f);

    class Entry
    {
        public OwnedHero hero;
        public GameObject root;
        public Image background;
    }

    readonly List<Entry> entries = new List<Entry>();
    readonly List<OwnedHero> selected = new List<OwnedHero>();
    int maxCount = RunState.MaxPartySize;

    public void Open()
    {
        if (lobby == null)
            lobby = Object.FindFirstObjectByType<LobbyController>();

        gameObject.SetActive(true);
        selected.Clear();
        RebuildList();
        RefreshVisuals();

        // 스크롤이 적용되어 있으면 열 때 맨 위로
        var scroll = listRoot != null ? listRoot.GetComponentInParent<ScrollRect>(true) : null;
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>출발 버튼 (빌더가 연결) — 최소 1명 선택 시 출발 가능</summary>
    public void OnClickDepart()
    {
        if (selected.Count < 1) return;

        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError($"[SortiePanel] 씬 '{gameSceneName}'을(를) 로드할 수 없습니다. " +
                           "File > Build Settings(또는 Build Profiles)의 Scene List에 게임 씬을 추가하고, " +
                           "SortiePanel의 gameSceneName이 씬 파일명과 같은지 확인하세요.");
            return;
        }

        var ids = new List<string>();
        foreach (var hero in selected) ids.Add(hero.heroId); // 보유 영웅 고유 id (영입 스펙 v1)
        SortieData.Set(ids);

        SceneManager.LoadScene(gameSceneName);
    }

    // ---------- 내부 ----------

    void RebuildList()
    {
        foreach (var e in entries)
            if (e.root != null) Destroy(e.root);
        entries.Clear();

        if (listRoot == null || entryTemplate == null) return;

        var roster = HeroRoster.Heroes;
        maxCount = Mathf.Min(RunState.MaxPartySize, roster.Count);

        foreach (OwnedHero hero in roster)
        {
            GameObject root = Instantiate(entryTemplate, entryTemplate.transform.parent);
            root.name = $"Entry_{hero.heroId}";
            root.SetActive(true);

            var entry = new Entry
            {
                hero = hero,
                root = root,
                background = root.GetComponent<Image>(),
            };
            entries.Add(entry);

            Text label = root.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = HeroInfoText.ListLabel(hero);

            Button button = root.GetComponent<Button>();
            OwnedHero captured = hero;
            if (button != null)
                button.onClick.AddListener(() => Toggle(captured));
        }
    }

    void Toggle(OwnedHero hero)
    {
        if (selected.Contains(hero)) selected.Remove(hero);
        else if (selected.Count < maxCount) selected.Add(hero);
        RefreshVisuals();
    }

    void RefreshVisuals()
    {
        foreach (var e in entries)
            if (e.background != null)
                e.background.color = selected.Contains(e.hero) ? SelectedColor : NormalColor;

        if (countText != null)
            countText.text = $"파티 선택  {selected.Count} / {maxCount}";
        if (departButton != null)
            departButton.interactable = selected.Count >= 1;
    }
}