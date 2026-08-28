using UnityEngine;

/// <summary>
/// 로비 고정 UI의 버튼 허브 (GDD 4: 영웅 관리 / 출정 / 설정 — 드래그와 무관하게 화면 고정).
/// ①단계에서는 셸만 — 각 기능은 이후 단계에서 채움.
/// </summary>
public class LobbyHUD : MonoBehaviour
{
    public HeroManagePanel heroManagePanel; // 빌더가 자동 연결
    public SortiePanel sortiePanel;          // 빌더가 자동 연결
    public RecruitPanel recruitPanel;        // 빌더가 자동 연결 (영입 스펙 v1)

    public void OnClickRecruit()
    {
        if (recruitPanel != null) recruitPanel.Open();
        else Debug.LogWarning("[Lobby] RecruitPanel이 연결되지 않았습니다 — [로비 영입 UI 생성] 메뉴를 실행하세요.");
    }

    public void OnClickHeroManage()
    {
        if (heroManagePanel != null) heroManagePanel.Open();
        else Debug.LogWarning("[Lobby] HeroManagePanel이 연결되지 않았습니다 — [로비 영웅 관리 UI 생성] 메뉴를 실행하세요.");
    }

    public void OnClickSortie()
    {
        if (sortiePanel != null) sortiePanel.Open();
        else Debug.LogWarning("[Lobby] SortiePanel이 연결되지 않았습니다 — [로비 출정 UI 생성] 메뉴를 실행하세요.");
    }

    public void OnClickSettings()
    {
        Debug.Log("[Lobby] 설정 — 추후 구현 예정");
    }
}