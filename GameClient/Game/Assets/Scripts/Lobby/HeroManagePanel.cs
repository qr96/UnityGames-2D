using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 영웅 관리 패널 (GDD 3: 영웅 관리 → 목록 → 선택 → 상세).
/// 초기 버전은 정보 확인만 — 장비(유물) 장착은 시스템 확정 후 추가.
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

    readonly List<GameObject> spawnedEntries = new List<GameObject>();

    public void Open()
    {
        if (lobby == null)
            lobby = Object.FindFirstObjectByType<LobbyController>();
        if (lobby == null)
        {
            Debug.LogError("[HeroManagePanel] LobbyController를 찾을 수 없습니다.");
            return;
        }

        gameObject.SetActive(true);
        RebuildList();
        if (detailText != null)
            detailText.text = "영웅을 선택하세요.";
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    void RebuildList()
    {
        foreach (var go in spawnedEntries)
            if (go != null) Destroy(go);
        spawnedEntries.Clear();

        if (listRoot == null || entryTemplate == null) return;

        List<HeroDefinition> unlocked = lobby.Profile.GetUnlockedHeroes(lobby.heroDatabase);
        foreach (HeroDefinition def in unlocked)
        {
            GameObject entry = Instantiate(entryTemplate, entryTemplate.transform.parent);
            entry.name = $"Entry_{def.id}";
            entry.SetActive(true);
            spawnedEntries.Add(entry);

            Text label = entry.GetComponentInChildren<Text>(true);
            if (label != null) label.text = $"{def.displayName}   ({Role(def)})";

            Button button = entry.GetComponent<Button>();
            HeroDefinition captured = def; // 클로저 캡처
            if (button != null)
                button.onClick.AddListener(() => ShowDetail(captured));
        }
    }

    void ShowDetail(HeroDefinition def)
    {
        if (detailText == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{def.displayName}  ({Role(def)})");
        sb.AppendLine();
        sb.AppendLine($"체력          {def.maxHP:0}");
        sb.AppendLine($"공격력        {def.attack:0}");
        sb.AppendLine($"공격 사거리   {def.attackRange:0.0}");
        sb.AppendLine($"공격 주기     {def.attackInterval:0.0}초");
        sb.AppendLine($"이동 속도     {def.moveSpeed:0.0}");
        sb.AppendLine($"기본 공격     공격력의 {def.basicAttackPercent:0}%");
        if (def.skill != null)
        {
            sb.AppendLine();
            sb.AppendLine($"스킬          {def.skill.displayName} (쿨타임 {def.skill.cooldown:0}초)");
        }
        detailText.text = sb.ToString();
    }

    static string Role(HeroDefinition d) => HeroClassUtil.Korean(d.heroClass);
}