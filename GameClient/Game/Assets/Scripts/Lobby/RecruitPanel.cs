using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 영입 상점 패널 (영입 스펙 v1).
///   · 후보 3칸 — 스탯/액티브/특성 전부 공개 (HeroInfoText)
///   · 영입 버튼: 골드 차감 + 로스터 편입. 빈 칸/골드 부족/로스터 가득이면 비활성
///   · 갱신은 원정 종료 시 자동 (수동 리롤 없음) — 이 패널은 표시/영입만 담당
/// UI 참조는 LobbySceneBuilder가 자동 연결.
/// </summary>
public class RecruitPanel : MonoBehaviour
{
    [Tooltip("데이터 소스 (비워두면 자동 탐색)")]
    public LobbyController lobby;

    [Header("UI 연결 (빌더가 자동 연결)")]
    public Text goldText;
    public Text[] slotInfoTexts = new Text[RecruitShop.CandidateCount];
    public Button[] recruitButtons = new Button[RecruitShop.CandidateCount];

    public void Open()
    {
        if (lobby == null)
            lobby = Object.FindFirstObjectByType<LobbyController>();

        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    // 빌더의 영구 리스너는 인자를 넘길 수 없어 슬롯별 메서드로 연결
    public void OnClickRecruit0() => Recruit(0);
    public void OnClickRecruit1() => Recruit(1);
    public void OnClickRecruit2() => Recruit(2);

    void Recruit(int index)
    {
        if (!RecruitShop.TryRecruit(index)) return;
        Refresh();
    }

    void Refresh()
    {
        if (goldText != null)
            goldText.text = $"골드  {GoldWallet.Gold}     보유 영웅  {HeroRoster.Heroes.Count} / {HeroRoster.MaxRoster}";

        var candidates = RecruitShop.Candidates;
        for (int i = 0; i < RecruitShop.CandidateCount; i++)
        {
            OwnedHero hero = i < candidates.Count ? candidates[i] : null;

            if (i < slotInfoTexts.Length && slotInfoTexts[i] != null)
                slotInfoTexts[i].text = hero != null
                    ? HeroInfoText.Build(hero)
                    : "빈 자리\n\n다음 원정 종료 시\n새 후보가 도착합니다.";

            if (i < recruitButtons.Length && recruitButtons[i] != null)
            {
                recruitButtons[i].interactable = RecruitShop.CanRecruit(i);
                Text label = recruitButtons[i].GetComponentInChildren<Text>(true);
                if (label != null)
                    label.text = hero != null ? $"영입  ({RecruitShop.Price} 골드)" : "-";
            }
        }
    }
}
