using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 출정 패널 (GDD 9: 출정 → 파티 선택(3명) → 확인 → 월드 진입).
/// 해금 영웅 목록에서 탭으로 선택/해제, 정확히 3명(해금이 3명 미만이면 전원)일 때 출발 가능.
/// 출발 시 선택을 SortieData에 담고 게임 씬 로드.
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
        public HeroDefinition def;
        public GameObject root;
        public Image background;
    }

    readonly List<Entry> entries = new List<Entry>();
    readonly List<HeroDefinition> selected = new List<HeroDefinition>();
    int requiredCount = 3;

    public void Open()
    {
        if (lobby == null)
            lobby = Object.FindFirstObjectByType<LobbyController>();
        if (lobby == null)
        {
            Debug.LogError("[SortiePanel] LobbyController를 찾을 수 없습니다.");
            return;
        }

        gameObject.SetActive(true);
        selected.Clear();
        RebuildList();
        RefreshVisuals();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>출발 버튼 (빌더가 연결)</summary>
    public void OnClickDepart()
    {
        if (selected.Count != requiredCount) return;

        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError($"[SortiePanel] 씬 '{gameSceneName}'을(를) 로드할 수 없습니다. " +
                           "File > Build Settings(또는 Build Profiles)의 Scene List에 게임 씬을 추가하고, " +
                           "SortiePanel의 gameSceneName이 씬 파일명과 같은지 확인하세요.");
            return;
        }

        var ids = new List<string>();
        foreach (var def in selected) ids.Add(def.id);
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

        List<HeroDefinition> unlocked = lobby.Profile.GetUnlockedHeroes(lobby.heroDatabase);
        requiredCount = Mathf.Min(RunState.StartPartySize, unlocked.Count);

        foreach (HeroDefinition def in unlocked)
        {
            GameObject root = Instantiate(entryTemplate, entryTemplate.transform.parent);
            root.name = $"Entry_{def.id}";
            root.SetActive(true);

            var entry = new Entry
            {
                def = def,
                root = root,
                background = root.GetComponent<Image>(),
            };
            entries.Add(entry);

            Text label = root.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = $"{def.displayName}   ({Role(def)})";

            Button button = root.GetComponent<Button>();
            HeroDefinition captured = def;
            if (button != null)
                button.onClick.AddListener(() => Toggle(captured));
        }
    }

    void Toggle(HeroDefinition def)
    {
        if (selected.Contains(def)) selected.Remove(def);
        else if (selected.Count < requiredCount) selected.Add(def);
        RefreshVisuals();
    }

    void RefreshVisuals()
    {
        foreach (var e in entries)
            if (e.background != null)
                e.background.color = selected.Contains(e.def) ? SelectedColor : NormalColor;

        if (countText != null)
            countText.text = $"파티 선택  {selected.Count} / {requiredCount}";
        if (departButton != null)
            departButton.interactable = selected.Count == requiredCount;
    }

    static string Role(HeroDefinition d) =>
        d.isHealer ? "힐러" : (d.attackRange >= 2.5f ? "원거리" : "근접");
}
