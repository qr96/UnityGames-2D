using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 씬(에디터)에서 제작한 UI와 런 흐름(RunManager)을 잇는 허브.
/// Canvas에 부착하고, 인스펙터에서 각 패널/텍스트를 연결.
/// 페이즈에 따라 패널을 켜고 끄며, 버튼의 OnClick은 아래 OnClick* 메서드에 연결.
/// 연결이 비어 있어도(null) 에러 없이 동작 — 아직 안 만든 패널은 그냥 표시되지 않음.
///
/// 인벤토리는 팝업: 준비 화면의 [인벤토리] 버튼으로 열고 닫기 버튼으로 닫음.
/// 전투 승리 → 전리품 확인 직후의 준비 화면에서는 자동으로 열림.
/// (영입 UI는 시스템 보류 중이라 없음 — 방식 확정 시 추가)
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("공통")]
    public Text phaseLabel;

    [Header("씬 전환")]
    [Tooltip("런 종료(클리어/실패) 후 복귀할 로비 씬 이름")]
    public string lobbySceneName = "Lobby";

    [Header("탐험 화면")]
    public ExploreDirectionPanel exploreDirectionPanel; // 상하좌우 방향 선택
    public GameObject mapButton;                        // 지도 열기 버튼
    public MapPanel mapPanel;                           // 지도 팝업

    [Header("계단 화면 (Explore 중 계단 위에서만 표시)")]
    public GameObject stairsPanel;   // [귀환]/[내려가기] 버튼 묶음
    public GameObject descendButton; // 내려가기 — 마지막 층 계단에서는 숨김 (CanDescend)

    [Header("전투 준비 화면")]
    public GameObject startButton;
    public GameObject inventoryButton;
    public InventoryPanel inventoryPanel;      // 팝업 (닫힌 상태가 기본)
    public PartyEquipPanel partyEquipPanel;    // 인벤토리와 함께 열리고 닫힘

    [Header("야영지 화면")]
    public GameObject campPanel; // [휴식]/[떠나기] 버튼 묶음

    [Header("전투 중 화면")]
    public ConsumableBar consumableBar;

    [Header("전리품 팝업")]
    public GameObject lootPanel;
    public Text lootText;

    [Header("런 결과 (클리어/실패 공용)")]
    public GameObject resultPanel;
    public Text resultText;

    RunPhase prevPhase = RunPhase.None;

    void Start()
    {
        RunManager rm = RunManager.Instance;
        if (rm == null)
        {
            Debug.LogError("[GameHUD] RunManager가 없습니다. 부트스트랩 오브젝트가 씬에 있는지 확인하세요.");
            return;
        }
        rm.OnPhaseChanged += Apply;
        Apply(rm.Phase); // 구독 전에 이미 시작된 페이즈 반영
    }

    void OnDestroy()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnPhaseChanged -= Apply;
    }

    void Apply(RunPhase phase)
    {
        SetActive(startButton, phase == RunPhase.Placement);
        SetActive(inventoryButton, phase == RunPhase.Placement);
        SetActive(consumableBar != null ? consumableBar.gameObject : null, phase == RunPhase.Battle);
        SetActive(exploreDirectionPanel != null ? exploreDirectionPanel.gameObject : null, phase == RunPhase.Explore);
        SetActive(mapButton, phase == RunPhase.Explore);
        if (phase != RunPhase.Explore && mapPanel != null) mapPanel.Close();

        // 계단 (명세 5·20): 확보 후에는 Explore 중 위치와 무관하게 [귀환]/[하강] 상시 표시
        RunManager rmForStairs = RunManager.Instance;
        bool secured = rmForStairs != null && rmForStairs.CanReturn;
        SetActive(stairsPanel, secured);
        SetActive(descendButton, secured && rmForStairs.CanDescend);
        SetActive(campPanel, phase == RunPhase.Camp);
        SetActive(lootPanel, phase == RunPhase.Loot);
        SetActive(resultPanel, phase == RunPhase.RunClear || phase == RunPhase.RunFailed);

        // 인벤토리는 팝업 — 준비 화면이 아니면 항상 닫힘
        if (phase != RunPhase.Placement)
            CloseInventory();

        RunManager rm = RunManager.Instance;
        RunState run = rm.Run;
        if (run == null)
        {
            prevPhase = phase;
            return;
        }

        LocationDefinition loc = rm.World != null ? rm.World.Current : null;
        string locName = loc != null ? loc.displayName : "";

        switch (phase)
        {
            case RunPhase.Explore:
                string regionName = "";
                if (rm.World != null && loc != null)
                {
                    var region = rm.World.world.GetRegionOf(loc);
                    if (region != null) regionName = region.regionName + " · ";
                }
                if (rm.CanDescend)
                    SetLabel($"{regionName}{locName}\n계단 확보 · 귀환/하강 가능");
                else if (rm.CanReturn)
                    SetLabel($"{regionName}{locName}\n외부 통로 개방 · 귀환 가능");
                else
                    SetLabel($"{regionName}어디로 갈까요?");
                if (exploreDirectionPanel != null) exploreDirectionPanel.Refresh();
                break;

            case RunPhase.Travel:
                // 정보 미노출 장소로 이동 중이면 이름을 가림 (노드 규칙 — 전투 클리어 시 인접 노출)
                string dest = MaskedName(rm, rm.TravelDestination);
                SetLabel($"이동 중 — {dest}(으)로 향하는 길");
                break;

            case RunPhase.Camp:
                SetLabel($"{locName} — 모닥불 곁에서 잠시 쉬어갑니다");
                break;

            case RunPhase.Placement:
                SetLabel($"전투 준비 — {locName}");
                // 전리품 확인 직후 넘어온 준비 화면이면 인벤토리 자동 열기
                if (prevPhase == RunPhase.Loot) OpenInventory();
                else CloseInventory();
                break;

            case RunPhase.Battle:
                SetLabel(""); // 전투 중 상단 라벨 제거 (UI 피드백 — 상태 바가 대체, 전투 영역 확보)
                break;

            case RunPhase.Loot:
                SetLabel("전투 승리!");
                if (lootText != null) lootText.text = BuildLootText(rm);
                break;

            case RunPhase.Recruit:
                SetLabel("동료 영입 (보류 중 — UI 미구현)");
                break;

            case RunPhase.RunClear:
                SetLabel("런 클리어! — 전리품을 가지고 귀환합니다");
                if (resultText != null) resultText.text = BuildClearText(rm);
                break;

            case RunPhase.RunFailed:
                SetLabel("런 실패");
                if (resultText != null) resultText.text = "파티가 전멸했습니다.\n출전 영웅과 이번 원정에서 획득한 전리품이 소멸합니다.";
                break;
        }

        prevPhase = phase;
    }

    // ---------- 인벤토리 팝업 ----------

    void OpenInventory()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.gameObject.SetActive(true);
            inventoryPanel.Refresh();
        }
        if (partyEquipPanel != null)
        {
            partyEquipPanel.gameObject.SetActive(true);
            partyEquipPanel.Refresh();
        }
    }

    void CloseInventory()
    {
        if (inventoryPanel != null) inventoryPanel.gameObject.SetActive(false);
        if (partyEquipPanel != null) partyEquipPanel.gameObject.SetActive(false);
    }

    // ---------- 버튼 OnClick 연결용 ----------

    public void OnClickStartBattle() => RunManager.Instance.BeginCombat();
    public void OnClickConfirmLoot() => RunManager.Instance.ConfirmLoot();
    public void OnClickNewRun()
    {
        // 확정: 클리어/실패 모두 로비 복귀.
        // 로비 씬을 로드할 수 없으면(빌드 목록 미등록 — 게임 씬 단독 개발 시) 즉시 재시작 폴백.
        if (Application.CanStreamedLevelBeLoaded(lobbySceneName))
            UnityEngine.SceneManagement.SceneManager.LoadScene(lobbySceneName);
        else
            RunManager.Instance.StartDefaultRun();
    }
    public void OnClickOpenInventory() => OpenInventory();
    public void OnClickCloseInventory() => CloseInventory();
    public void OnClickRest() => RunManager.Instance.RestAtCamp();
    public void OnClickLeaveCamp() => RunManager.Instance.LeaveCamp();
    public void OnClickOpenMap() { if (mapPanel != null) mapPanel.Open(); }
    public void OnClickCloseMap() { if (mapPanel != null) mapPanel.Close(); }
    public void OnClickDescendStairs() => RunManager.Instance.DescendStairs();
    public void OnClickReturnFromStairs() => RunManager.Instance.ReturnFromStairs();

    /// <summary>정보 미노출 장소 이름 가림 — 방향/지도 패널에서도 이 헬퍼 사용 권장.</summary>
    public static string MaskedName(RunManager rm, LocationDefinition loc)
    {
        if (loc == null) return "";
        bool known = rm != null && rm.World != null && rm.World.IsRevealed(loc);
        return known ? loc.displayName : "???";
    }

    // ---------- 텍스트 빌더 ----------

    string BuildLootText(RunManager rm)
    {
        var drops = rm.LastDrops;
        if (drops.Count == 0) return "획득한 장비가 없습니다.";

        var counts = new Dictionary<string, int>();
        foreach (var d in drops)
            counts[d.displayName] = counts.TryGetValue(d.displayName, out int c) ? c + 1 : 1;

        var sb = new System.Text.StringBuilder();
        foreach (var kv in counts)
            sb.AppendLine($"{kv.Key}  x{kv.Value}");
        return sb.ToString();
    }

    string BuildClearText(RunManager rm)
    {
        var names = rm.LastUnlockedHeroNames;
        string unlockLine = names.Count > 0
            ? "영구 해금: " + string.Join(", ", names)
            : "새로 해금된 영웅은 없습니다.";
        RunManager rmGold = RunManager.Instance;
        string goldLine = rmGold != null && rmGold.Run != null && rmGold.Run.goldEarned > 0
            ? $"\n획득 골드  +{rmGold.Run.goldEarned}"
            : "";
        return unlockLine + goldLine + "\n확보한 장비는 보관소와 영웅에게 유지됩니다.";
    }

    void SetLabel(string text)
    {
        if (phaseLabel != null) phaseLabel.text = text;
    }

    static void SetActive(GameObject go, bool value)
    {
        if (go != null && go.activeSelf != value) go.SetActive(value);
    }
}