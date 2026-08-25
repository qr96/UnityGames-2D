using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 탐험 화면의 방향 선택 UI (상/하/좌/우).
/// 각 방향 슬롯에 다음 장소의 이름 + 상태 문자열(previewText)을 표기.
/// 길이 없거나 이미 방문한 방향(일방통행)은 숨김. 버튼 탭 → 이동.
/// </summary>
public class ExploreDirectionPanel : MonoBehaviour
{
    [Serializable]
    public class DirectionSlot
    {
        public Direction direction;
        public GameObject root;    // 슬롯 전체 (버튼)
        public Text nameText;      // "▲ 평원"
        public Text previewText;   // "바람이 거세다"
    }

    public DirectionSlot[] slots = new DirectionSlot[0];

    void Awake()
    {
        // 버튼 → 이동 연결 (런타임)
        foreach (var slot in slots)
        {
            if (slot == null || slot.root == null) continue;
            Button button = slot.root.GetComponent<Button>();
            Direction captured = slot.direction;
            if (button != null)
                button.onClick.AddListener(() => RunManager.Instance.TravelInDirection(captured));
        }
    }

    void OnEnable()
    {
        Refresh();
    }

    /// <summary>탐험 진입 시 호출 — 방향별 이동 가능 여부/미리보기 갱신</summary>
    public void Refresh()
    {
        WorldState ws = RunManager.Instance != null ? RunManager.Instance.World : null;

        foreach (var slot in slots)
        {
            if (slot == null || slot.root == null) continue;

            LocationDefinition dest = ws != null ? ws.GetAvailableExit(slot.direction) : null;
            bool available = dest != null;
            slot.root.SetActive(available);
            if (!available) continue;

            if (slot.nameText != null)
                slot.nameText.text = $"{DirectionUtil.Arrow(slot.direction)} {dest.displayName}";
            if (slot.previewText != null)
                slot.previewText.text = dest.previewText;
        }
    }
}
