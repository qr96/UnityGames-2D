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
    public Text startFloorText; // 출발 층 표시 (버튼 라벨 — 던전 명세: 개방된 진입 포인트 선택)

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
        startFloorIndex = 0; // 열 때마다 1층부터 (기본)
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
        SortieData.startFloor = CurrentStartFloor(); // 출발 층 (1층 또는 개방된 진입 포인트)

        SceneManager.LoadScene(gameSceneName);
    }

    // ---------- 출발 층 선택 (던전 명세: 개방된 외부 진입 포인트) ----------

    int startFloorIndex;

    int CurrentStartFloor()
    {
        var options = DungeonProgress.GetStartFloorOptions();
        if (options.Count == 0) return 1;
        startFloorIndex = Mathf.Clamp(startFloorIndex, 0, options.Count - 1);
        return options[startFloorIndex];
    }

    /// <summary>출발 층 버튼 (빌더가 연결) — 개방된 진입 포인트를 순환 선택</summary>
    public void OnClickCycleStartFloor()
    {
        var options = DungeonProgress.GetStartFloorOptions();
        if (options.Count <= 1) return; // 1층뿐 — 선택지 없음
        startFloorIndex = (startFloorIndex + 1) % options.Count;
        RefreshVisuals();
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
        if (startFloorText != null)
        {
            int floor = CurrentStartFloor();
            int optionCount = DungeonProgress.GetStartFloorOptions().Count;
            startFloorText.text = floor == 1 ? "출발: 지하 1층 (입구)" : $"출발: 지하 {floor}층 (외부 통로)";
            if (optionCount > 1) startFloorText.text += "  ▸"; // 선택지 있음 표시
        }
    }
}