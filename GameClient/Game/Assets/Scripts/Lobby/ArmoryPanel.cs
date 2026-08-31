using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보관소 패널 (장비 영속 v1) — 로비에서 미장착 장비 목록 열람 (읽기 전용).
/// 장착 변경은 원정 중 전투 준비 화면에서 (로비 장착 변경은 추후 확장 후보).
/// UI 참조는 LobbySceneBuilder [로비 보관소 UI 생성]이 자동 연결.
/// </summary>
public class ArmoryPanel : MonoBehaviour
{
    [Header("UI 연결 (빌더가 자동 연결)")]
    public Text titleText;
    public Transform listRoot;       // ScrollRect의 Content
    public GameObject entryTemplate; // 비활성 템플릿 (Image + Text)

    readonly List<GameObject> spawned = new List<GameObject>();

    public void Open()
    {
        gameObject.SetActive(true);
        Rebuild();

        var scroll = listRoot != null ? listRoot.GetComponentInParent<ScrollRect>(true) : null;
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    void Rebuild()
    {
        foreach (var go in spawned)
            if (go != null) Destroy(go);
        spawned.Clear();

        var items = Armory.Items;
        if (titleText != null)
            titleText.text = $"보관소  ({items.Count})";

        if (listRoot == null || entryTemplate == null) return;

        // 특별 장비를 위로 (내용 파악 편의 — 정렬만, 데이터 순서는 유지)
        var sorted = new List<EquipmentDefinition>(items);
        sorted.Sort((a, b) => (b != null && b.isSpecial ? 1 : 0).CompareTo(a != null && a.isSpecial ? 1 : 0));

        foreach (var item in sorted)
        {
            if (item == null) continue;
            GameObject entry = Instantiate(entryTemplate, entryTemplate.transform.parent);
            entry.SetActive(true);
            spawned.Add(entry);

            Text label = entry.GetComponentInChildren<Text>(true);
            if (label != null) label.text = item.displayName;
        }
    }
}
