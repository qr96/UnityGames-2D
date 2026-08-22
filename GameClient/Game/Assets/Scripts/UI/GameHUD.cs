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

    [Header("전투 준비 화면")]
    public GameObject startButton;
    public GameObject inventoryButton;
    public InventoryPanel inventoryPanel;      // 팝업 (닫힌 상태가 기본)
    public PartyEquipPanel partyEquipPanel;    // 인벤토리와 함께 열리고 닫힘

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
                SetLabel($"{regionName}이동할 장소를 선택하세요");
                break;

            case RunPhase.Travel:
                string dest = rm.TravelDestination != null ? rm.TravelDestination.displayName : "";
                SetLabel($"이동 중 — {dest}(으)로 향하는 길");
                break;

            case RunPhase.Placement:
                SetLabel($"전투 준비 — {locName}");
                // 전리품 확인 직후 넘어온 준비 화면이면 인벤토리 자동 열기
                if (prevPhase == RunPhase.Loot) OpenInventory();
                else CloseInventory();
                break;

            case RunPhase.Battle:
                SetLabel($"전투 — {locName}");
                break;

            case RunPhase.Loot:
                SetLabel("전투 승리!");
                if (lootText != null) lootText.text = BuildLootText(rm);
                break;

            case RunPhase.Recruit:
                SetLabel("동료 영입 (보류 중 — UI 미구현)");
                break;

            case RunPhase.RunClear:
                SetLabel("런 클리어! — 거인의 무덤 정복");
                if (resultText != null) resultText.text = BuildClearText(rm);
                break;

            case RunPhase.RunFailed:
                SetLabel("런 실패");
                if (resultText != null) resultText.text = "파티가 전멸했습니다.\n이번 런의 파티와 장비는 소멸합니다.";
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
        return unlockLine + "\n이번 런의 장비는 소멸합니다.";
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