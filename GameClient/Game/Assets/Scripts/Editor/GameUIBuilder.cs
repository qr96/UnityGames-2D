using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 에디터 메뉴 [Tools > GrabProto > 게임 UI 생성] — 씬에 게임 UI 전체를 한 번에 생성.
/// 생성 후에는 일반 씬 오브젝트이므로 위치/크기/색을 에디터에서 자유롭게 수정하면 됨.
/// GameHUD의 모든 참조와 버튼 OnClick, 부트스트랩의 hud 필드까지 자동 연결.
/// 이미 생성되어 있으면 중복 생성하지 않고 중단.
/// </summary>
public static class GameUIBuilder
{
    static Font font;

    [MenuItem("Tools/GrabProto/게임 UI 생성", false, 11)]
    public static void Build()
    {
        font = LoadFont();

        // ---- Canvas / EventSystem 확보 ----
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1440f); // 3:4 세로 (화면 비율 개편)
            scaler.matchWidthOrHeight = 0.5f;
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
        }
        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        if (canvas.transform.Find("PhaseLabel") != null)
        {
            EditorUtility.DisplayDialog("게임 UI 생성", "이미 생성된 UI가 있습니다.\n다시 만들려면 기존 UI 오브젝트를 삭제한 뒤 실행하세요.", "확인");
            return;
        }

        GameHUD hud = canvas.GetComponent<GameHUD>();
        if (hud == null) hud = canvas.gameObject.AddComponent<GameHUD>();

        Transform root = canvas.transform;

        // ================= 공통 =================

        // 상단 페이즈 라벨
        Text phaseLabel = MakeText(root, "PhaseLabel", "전투 준비", 44);
        SetAnchored(phaseLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1000f, 90f));
        hud.phaseLabel = phaseLabel;

        // ================= 전투 준비 화면 =================

        // 인벤토리 패널 (하단)
        var invGO = MakeImage(root, "InventoryPanel", new Color(0.06f, 0.07f, 0.10f, 0.95f), rounded: true);
        var invRT = invGO.GetComponent<RectTransform>();
        invRT.anchorMin = invRT.anchorMax = new Vector2(0.5f, 0f);
        invRT.pivot = new Vector2(0.5f, 0f);
        invRT.anchoredPosition = new Vector2(0f, 20f);
        invRT.sizeDelta = new Vector2(1020f, 470f);
        var invPanel = invGO.AddComponent<InventoryPanel>();
        hud.inventoryPanel = invPanel;

        // [장비] 탭
        var tabGO = MakeImage(invGO.transform, "Tab_장비", new Color(0.30f, 0.45f, 0.85f), rounded: true);
        var tabRT = tabGO.GetComponent<RectTransform>();
        tabRT.anchorMin = tabRT.anchorMax = new Vector2(0f, 1f);
        tabRT.pivot = new Vector2(0f, 1f);
        tabRT.anchoredPosition = new Vector2(24f, -14f);
        tabRT.sizeDelta = new Vector2(150f, 60f);
        StretchText(MakeText(tabGO.transform, "Label", "장비", 30));

        // 장비 슬롯 그리드 (2행 x 4열, GridLayoutGroup — 슬롯 수/간격 자유 조정 가능)
        var gridGO = new GameObject("SlotGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGO.transform.SetParent(invGO.transform, false);
        var gridRT = gridGO.GetComponent<RectTransform>();
        SetAnchored(gridRT, new Vector2(0.5f, 0.5f), new Vector2(0f, -35f), new Vector2(760f, 370f));
        var grid = gridGO.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(170f, 170f);
        grid.spacing = new Vector2(22f, 22f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < 8; i++)
            MakeItemSlot<EquipmentSlot>(gridGO.transform, $"EquipSlot{i}", null, 24);

        // 전투 시작 버튼 (인벤토리 패널 위)
        Button startBtn = MakeButton(root, "StartButton", "전투 시작 ▶", new Color(0.22f, 0.62f, 0.40f), 38);
        SetAnchored(startBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 555f), new Vector2(480f, 100f));
        WireButton(startBtn, hud, nameof(GameHUD.OnClickStartBattle));
        hud.startButton = startBtn.gameObject;

        // ================= 전투 중 화면 =================

        var barGO = new GameObject("ConsumableBar", typeof(RectTransform));
        barGO.transform.SetParent(root, false);
        var barRT = barGO.GetComponent<RectTransform>();
        SetAnchored(barRT, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(680f, 150f));
        var bar = barGO.AddComponent<ConsumableBar>();
        hud.consumableBar = bar;

        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        const float slotSize = 140f, gap = 24f;
        float totalW = 4 * slotSize + 3 * gap;
        for (int i = 0; i < 4; i++)
        {
            var slot = MakeItemSlot<ConsumableSlot>(barGO.transform, $"ConsumableSlot{i}", knob, 26);
            var srt = slot.GetComponent<RectTransform>();
            SetAnchored(srt, new Vector2(0.5f, 0.5f),
                new Vector2(-totalW / 2f + slotSize / 2f + i * (slotSize + gap), 0f),
                new Vector2(slotSize, slotSize));
        }

        // ================= 팝업 (비활성으로 생성) =================

        // 전리품 팝업
        var lootGO = MakeImage(root, "LootPanel", new Color(0.06f, 0.07f, 0.10f, 0.96f), rounded: true);
        SetAnchored(lootGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 560f));
        var lootTitle = MakeText(lootGO.transform, "Title", "획득 아이템", 42);
        SetAnchored(lootTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 210f), new Vector2(600f, 80f));
        Text lootText = MakeText(lootGO.transform, "LootText", "", 34);
        SetAnchored(lootText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(620f, 280f));
        Button lootBtn = MakeButton(lootGO.transform, "ConfirmButton", "확인", new Color(0.30f, 0.45f, 0.85f), 34);
        SetAnchored(lootBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(460f, 100f));
        WireButton(lootBtn, hud, nameof(GameHUD.OnClickConfirmLoot));
        hud.lootPanel = lootGO;
        hud.lootText = lootText;
        lootGO.SetActive(false);

        // 결과 패널
        var resultGO = MakeImage(root, "ResultPanel", new Color(0.06f, 0.07f, 0.10f, 0.96f), rounded: true);
        SetAnchored(resultGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 520f));
        Text resultText = MakeText(resultGO.transform, "ResultText", "", 36);
        SetAnchored(resultText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(660f, 280f));
        Button newRunBtn = MakeButton(resultGO.transform, "NewRunButton", "새 런 시작", new Color(0.30f, 0.45f, 0.85f), 34);
        SetAnchored(newRunBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(500f, 100f));
        WireButton(newRunBtn, hud, nameof(GameHUD.OnClickNewRun));
        hud.resultPanel = resultGO;
        hud.resultText = resultText;
        resultGO.SetActive(false);

        // ---- 파티 장비 패널 ----
        BuildPartyEquipPanelInternal(canvas, hud);

        // ---- 인벤토리 열기/닫기 버튼 ----
        BuildInventoryButtonsInternal(canvas, hud);

        // ---- 탐험 / 야영지 / 계단 (풀빌드에 포함 — 개별 메뉴는 재생성용) ----
        BuildExploreUI();
        BuildCampUI();
        BuildStairsUI();

        // ---- 부트스트랩 hud 자동 연결 ----
        var bootstrap = Object.FindFirstObjectByType<DevBootstrap>();
        if (bootstrap != null)
        {
            bootstrap.hud = hud;
            EditorUtility.SetDirty(bootstrap);
        }

        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[GameUIBuilder] 게임 UI 생성 완료. 위치/크기/색은 에디터에서 자유롭게 수정하세요.");
    }

    [MenuItem("Tools/GrabProto/게임 UI 패치 (누락 보완)", false, 12)]
    public static void PatchGameUI()
    {
        font = LoadFont();

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        GameHUD hud = canvas != null ? canvas.GetComponent<GameHUD>() : null;
        if (canvas == null || hud == null)
        {
            EditorUtility.DisplayDialog("게임 UI 패치", "Canvas/GameHUD가 없습니다. 먼저 [게임 UI 생성]을 실행하세요.", "확인");
            return;
        }

        ApplyCanvasStandard(canvas); // 3:4 전환 — 기존 씬 마이그레이션 겸용

        var applied = new System.Collections.Generic.List<string>();
        if (canvas.transform.Find("PartyEquipPanel") == null) { BuildPartyEquipPanel(); applied.Add("장비 패널"); }
        if (canvas.transform.Find("InventoryButton") == null) { BuildInventoryButtons(); applied.Add("인벤토리 버튼"); }
        if (canvas.transform.Find("ExploreDirectionPanel") == null) { BuildExploreUI(); applied.Add("탐험 UI"); }
        if (canvas.transform.Find("CampPanel") == null) { BuildCampUI(); applied.Add("야영지 패널"); }
        if (canvas.transform.Find("StairsPanel") == null) { BuildStairsUI(); applied.Add("계단 패널"); }

        if (applied.Count > 0)
        {
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log($"[GameUIBuilder] 패치 적용: {string.Join(", ", applied)}");
            EditorUtility.DisplayDialog("게임 UI 패치", $"적용됨:\n- {string.Join("\n- ", applied)}", "확인");
        }
        else
        {
            EditorUtility.DisplayDialog("게임 UI 패치", "누락된 요소 없음 — 최신 상태입니다.", "확인");
        }
    }

    [MenuItem("Tools/GrabProto/재생성/게임/장비 패널", false, 111)]
    public static void BuildPartyEquipPanel()
    {
        font = LoadFont();

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("영웅 장비 패널", "씬에 Canvas가 없습니다. 먼저 [게임 UI 생성]을 실행하세요.", "확인");
            return;
        }
        GameHUD hud = canvas.GetComponent<GameHUD>();
        if (hud == null)
        {
            EditorUtility.DisplayDialog("영웅 장비 패널", "Canvas에 GameHUD가 없습니다. 먼저 [게임 UI 생성]을 실행하세요.", "확인");
            return;
        }
        if (canvas.transform.Find("PartyEquipPanel") != null)
        {
            EditorUtility.DisplayDialog("영웅 장비 패널", "이미 PartyEquipPanel이 있습니다. 다시 만들려면 기존 것을 삭제한 뒤 실행하세요.", "확인");
            return;
        }

        BuildPartyEquipPanelInternal(canvas, hud);
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[GameUIBuilder] 영웅 장비 패널 생성 완료.");
    }

    /// <summary>파티 장비 패널 + 파티원 항목 템플릿 생성 및 연결</summary>
    static void BuildPartyEquipPanelInternal(Canvas canvas, GameHUD hud)
    {
        var panelGO = new GameObject("PartyEquipPanel", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        panelGO.transform.SetParent(canvas.transform, false);
        var prt = panelGO.GetComponent<RectTransform>();
        SetAnchored(prt, new Vector2(0.5f, 0f), new Vector2(0f, 690f), new Vector2(900f, 340f));

        var layout = panelGO.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var panel = panelGO.AddComponent<PartyEquipPanel>();

        // ---- 파티원 항목 템플릿 (비활성 — 스타일은 에디터에서 수정) ----
        var entry = MakeImage(panelGO.transform, "EntryTemplate", new Color(0.06f, 0.07f, 0.10f, 0.85f), rounded: true);
        entry.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 330f);

        Text nameText = MakeText(entry.transform, "Name", "이름", 26);
        SetAnchored(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(150f, 40f));

        for (int i = 0; i < 3; i++)
        {
            var slot = MakeImage(entry.transform, $"Slot{i}", new Color(0.12f, 0.13f, 0.19f, 0.90f), rounded: true);
            SetAnchored(slot.GetComponent<RectTransform>(), new Vector2(0.5f, 1f),
                new Vector2(0f, -90f - i * 90f), new Vector2(140f, 80f));
            slot.AddComponent<HeroEquipSlotUI>();
            StretchText(MakeText(slot.transform, "Label", "", 22));
        }
        entry.SetActive(false);
        panel.entryTemplate = entry;

        // ---- 연결 ----
        hud.partyEquipPanel = panel;
        if (hud.inventoryPanel != null)
        {
            hud.inventoryPanel.partyPanel = panel;
            EditorUtility.SetDirty(hud.inventoryPanel);
        }
    }

    [MenuItem("Tools/GrabProto/재생성/게임/인벤토리 버튼", false, 112)]
    public static void BuildInventoryButtons()
    {
        font = LoadFont();

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        GameHUD hud = canvas != null ? canvas.GetComponent<GameHUD>() : null;
        if (canvas == null || hud == null)
        {
            EditorUtility.DisplayDialog("인벤토리 버튼", "Canvas/GameHUD가 없습니다. 먼저 [게임 UI 생성]을 실행하세요.", "확인");
            return;
        }
        if (hud.inventoryPanel == null)
        {
            EditorUtility.DisplayDialog("인벤토리 버튼", "GameHUD에 inventoryPanel이 연결되어 있지 않습니다.", "확인");
            return;
        }
        if (canvas.transform.Find("InventoryButton") != null)
        {
            EditorUtility.DisplayDialog("인벤토리 버튼", "이미 InventoryButton이 있습니다.", "확인");
            return;
        }

        BuildInventoryButtonsInternal(canvas, hud);
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[GameUIBuilder] 인벤토리 열기/닫기 버튼 생성 완료.");
    }

    /// <summary>준비 화면의 [인벤토리] 버튼 + 인벤토리 패널 안의 [✕] 닫기 버튼 생성</summary>
    static void BuildInventoryButtonsInternal(Canvas canvas, GameHUD hud)
    {
        // 인벤토리 열기 버튼 (준비 화면 좌하단)
        Button invBtn = MakeButton(canvas.transform, "InventoryButton", "인벤토리", new Color(0.30f, 0.45f, 0.85f), 32);
        var irt = invBtn.GetComponent<RectTransform>();
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0f);
        irt.pivot = new Vector2(0f, 0f);
        irt.anchoredPosition = new Vector2(40f, 60f);
        irt.sizeDelta = new Vector2(280f, 100f);
        WireButton(invBtn, hud, nameof(GameHUD.OnClickOpenInventory));
        hud.inventoryButton = invBtn.gameObject;

        // 닫기 버튼 (인벤토리 패널 우상단)
        if (hud.inventoryPanel != null && hud.inventoryPanel.transform.Find("CloseButton") == null)
        {
            Button closeBtn = MakeButton(hud.inventoryPanel.transform, "CloseButton", "✕", new Color(0.55f, 0.22f, 0.25f), 34);
            var crt = closeBtn.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-14f, -14f);
            crt.sizeDelta = new Vector2(70f, 70f);
            WireButton(closeBtn, hud, nameof(GameHUD.OnClickCloseInventory));
        }
    }

    static float uiScale = 1f; // 캔버스 기준 해상도 보정 (1080 폭 설계 기준)
    static float S(float value) => value * uiScale;

    /// <summary>캔버스 표준 (화면 비율 개편: 3:4 세로 1080×1440) — 기존 씬에도 강제 적용</summary>
    static void ApplyCanvasStandard(Canvas canvas)
    {
        var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
        if (scaler == null) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1440f);
        scaler.matchWidthOrHeight = 0f; // 폭 기준 (S() 스케일과 일치)
        EditorUtility.SetDirty(scaler);
    }

    static void UpdateUiScale(Canvas canvas)
    {
        var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
        uiScale = (scaler != null && scaler.referenceResolution.x > 0f)
            ? scaler.referenceResolution.x / 1080f
            : 1f;
    }

    [MenuItem("Tools/GrabProto/재생성/게임/탐험 UI (방향+지도)", false, 113)]
    public static void BuildExploreUI()
    {
        font = LoadFont();

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        GameHUD hud = canvas != null ? canvas.GetComponent<GameHUD>() : null;
        if (canvas == null || hud == null)
        {
            EditorUtility.DisplayDialog("탐험 UI", "Canvas/GameHUD가 없습니다. 먼저 [게임 UI 생성]을 실행하세요.", "확인");
            return;
        }
        if (canvas.transform.Find("ExploreDirectionPanel") != null)
        {
            EditorUtility.DisplayDialog("탐험 UI", "이미 ExploreDirectionPanel이 있습니다.", "확인");
            return;
        }

        UpdateUiScale(canvas);

        // ---- 방향 선택 패널 (상/하/좌/우 4슬롯) ----
        var panelGO = new GameObject("ExploreDirectionPanel", typeof(RectTransform));
        panelGO.transform.SetParent(canvas.transform, false);
        var prt = panelGO.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = prt.offsetMax = Vector2.zero;

        var panel = panelGO.AddComponent<ExploreDirectionPanel>();
        panel.slots = new[]
        {
            MakeDirectionSlot(panelGO.transform, Direction.North, new Vector2(0.5f, 1f), new Vector2(0f, -S(220f)), new Vector2(S(560f), S(150f))),
            MakeDirectionSlot(panelGO.transform, Direction.South, new Vector2(0.5f, 0f), new Vector2(0f, S(220f)),  new Vector2(S(560f), S(150f))),
            MakeDirectionSlot(panelGO.transform, Direction.West,  new Vector2(0f, 0.5f), new Vector2(S(200f), 0f),  new Vector2(S(380f), S(170f))),
            MakeDirectionSlot(panelGO.transform, Direction.East,  new Vector2(1f, 0.5f), new Vector2(-S(200f), 0f), new Vector2(S(380f), S(170f))),
        };
        panelGO.SetActive(false);

        // ---- 지도 버튼 (우상단) ----
        Button mapBtn = MakeButton(canvas.transform, "MapButton", "지도", new Color(0.25f, 0.27f, 0.33f), 30);
        var mrt = mapBtn.GetComponent<RectTransform>();
        mrt.anchorMin = mrt.anchorMax = new Vector2(1f, 1f);
        mrt.pivot = new Vector2(1f, 1f);
        mrt.anchoredPosition = new Vector2(-S(30f), -S(30f));
        mrt.sizeDelta = new Vector2(S(160f), S(90f));
        WireButton(mapBtn, hud, nameof(GameHUD.OnClickOpenMap));
        mapBtn.gameObject.SetActive(false);

        // ---- 지도 팝업 ----
        var mapGO = MakeImage(canvas.transform, "MapPanel", new Color(0.06f, 0.07f, 0.10f, 0.97f), rounded: true);
        var maprt = mapGO.GetComponent<RectTransform>();
        maprt.anchorMin = maprt.anchorMax = new Vector2(0.5f, 0.5f);
        maprt.sizeDelta = new Vector2(S(940f), S(1400f));

        var mapPanel = mapGO.AddComponent<MapPanel>();

        Text mapTitle = MakeText(mapGO.transform, "Title", "지도 — 지나온 길", 40);
        var mtrt = mapTitle.rectTransform;
        mtrt.anchorMin = mtrt.anchorMax = new Vector2(0.5f, 1f);
        mtrt.anchoredPosition = new Vector2(0f, -S(64f));
        mtrt.sizeDelta = new Vector2(S(700f), S(80f));

        Button mapClose = MakeButton(mapGO.transform, "CloseButton", "✕", new Color(0.55f, 0.22f, 0.25f), 34);
        var mcrt = mapClose.GetComponent<RectTransform>();
        mcrt.anchorMin = mcrt.anchorMax = new Vector2(1f, 1f);
        mcrt.pivot = new Vector2(1f, 1f);
        mcrt.anchoredPosition = new Vector2(-S(16f), -S(16f));
        mcrt.sizeDelta = new Vector2(S(76f), S(76f));
        WireButton(mapClose, hud, nameof(GameHUD.OnClickCloseMap));

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(mapGO.transform, false);
        var content = contentGO.GetComponent<RectTransform>();
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
        content.anchoredPosition = new Vector2(0f, -S(40f));
        content.sizeDelta = new Vector2(S(840f), S(1150f));

        // 점 템플릿 (Image + 이름 라벨)
        var dotGO = MakeImage(content, "DotTemplate", Color.white, rounded: true);
        dotGO.GetComponent<RectTransform>().sizeDelta = new Vector2(S(52f), S(52f));
        Text dotLabel = MakeText(dotGO.transform, "Label", "이름", 24);
        var dlrt = dotLabel.rectTransform;
        dlrt.anchorMin = dlrt.anchorMax = new Vector2(0.5f, 0f);
        dlrt.pivot = new Vector2(0.5f, 1f);
        dlrt.anchoredPosition = new Vector2(0f, -S(6f));
        dlrt.sizeDelta = new Vector2(S(240f), S(50f));
        dotGO.SetActive(false);

        mapPanel.content = content;
        mapPanel.dotTemplate = dotGO;
        mapGO.SetActive(false);

        // ---- 연결 ----
        hud.exploreDirectionPanel = panel;
        hud.mapButton = mapBtn.gameObject;
        hud.mapPanel = mapPanel;

        EditorUtility.SetDirty(hud);
        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(mapPanel);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[GameUIBuilder] 탐험 UI(방향 선택 + 지도) 생성 완료.");
    }

    /// <summary>방향 슬롯 1개: 버튼 배경 + 이름 텍스트 + 미리보기 텍스트</summary>
    static ExploreDirectionPanel.DirectionSlot MakeDirectionSlot(
        Transform parent, Direction dir, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        Button btn = MakeButton(parent, $"Dir_{dir}", "", new Color(0.10f, 0.13f, 0.20f, 0.92f), 30);
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        // MakeButton이 만든 중앙 텍스트는 사용하지 않음 (이름/미리보기 2줄 구성)
        Text builtIn = btn.GetComponentInChildren<Text>();
        if (builtIn != null) Object.DestroyImmediate(builtIn.gameObject);

        Text nameText = MakeText(btn.transform, "NameText", "이름", 32);
        var nrt = nameText.rectTransform;
        nrt.anchorMin = new Vector2(0f, 0.5f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.offsetMin = new Vector2(S(16f), 0f);
        nrt.offsetMax = new Vector2(-S(16f), -S(8f));
        nameText.alignment = TextAnchor.MiddleCenter;

        Text preview = MakeText(btn.transform, "PreviewText", "미리보기", 24);
        preview.fontStyle = FontStyle.Normal;
        preview.color = new Color(0.8f, 0.8f, 0.85f);
        var pvrt = preview.rectTransform;
        pvrt.anchorMin = new Vector2(0f, 0f);
        pvrt.anchorMax = new Vector2(1f, 0.5f);
        pvrt.offsetMin = new Vector2(S(16f), S(8f));
        pvrt.offsetMax = new Vector2(-S(16f), 0f);
        preview.alignment = TextAnchor.MiddleCenter;

        return new ExploreDirectionPanel.DirectionSlot
        {
            direction = dir,
            root = btn.gameObject,
            nameText = nameText,
            previewText = preview,
        };
    }

    [MenuItem("Tools/GrabProto/재생성/게임/야영지 패널", false, 114)]
    public static void BuildCampUI()
    {
        font = LoadFont();

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        GameHUD hud = canvas != null ? canvas.GetComponent<GameHUD>() : null;
        if (canvas == null || hud == null)
        {
            EditorUtility.DisplayDialog("야영지 UI", "Canvas/GameHUD가 없습니다. 먼저 [게임 UI 생성]을 실행하세요.", "확인");
            return;
        }
        if (canvas.transform.Find("CampPanel") != null)
        {
            EditorUtility.DisplayDialog("야영지 UI", "이미 CampPanel이 있습니다.", "확인");
            return;
        }

        BuildCampUIInternal(canvas, hud);
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[GameUIBuilder] 야영지 UI 생성 완료.");
    }

    /// <summary>야영지 [휴식]/[떠나기] 버튼 생성 및 연결 — 캔버스 기준 해상도에 맞게 크기 보정</summary>
    static void BuildCampUIInternal(Canvas canvas, GameHUD hud)
    {
        UpdateUiScale(canvas);

        var panelGO = new GameObject("CampPanel", typeof(RectTransform));
        panelGO.transform.SetParent(canvas.transform, false);
        var prt = panelGO.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, S(200f));
        prt.sizeDelta = new Vector2(S(900f), S(140f));

        Button rest = MakeButton(panelGO.transform, "RestButton", "휴식", new Color(0.22f, 0.62f, 0.40f), 36);
        var rrt = rest.GetComponent<RectTransform>();
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f);
        rrt.anchoredPosition = new Vector2(-S(200f), 0f);
        rrt.sizeDelta = new Vector2(S(340f), S(120f));
        WireButton(rest, hud, nameof(GameHUD.OnClickRest));

        Button leave = MakeButton(panelGO.transform, "LeaveButton", "떠나기", new Color(0.30f, 0.33f, 0.42f), 36);
        var lrt = leave.GetComponent<RectTransform>();
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.anchoredPosition = new Vector2(S(200f), 0f);
        lrt.sizeDelta = new Vector2(S(340f), S(120f));
        WireButton(leave, hud, nameof(GameHUD.OnClickLeaveCamp));

        hud.campPanel = panelGO;
        panelGO.SetActive(false);
        EditorUtility.SetDirty(hud);
    }


    [MenuItem("Tools/GrabProto/재생성/게임/계단 패널", false, 115)]
    public static void BuildStairsUI()
    {
        font = LoadFont();

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        GameHUD hud = canvas != null ? canvas.GetComponent<GameHUD>() : null;
        if (canvas == null || hud == null)
        {
            EditorUtility.DisplayDialog("계단 UI", "Canvas/GameHUD가 없습니다. 먼저 [게임 UI 생성]을 실행하세요.", "확인");
            return;
        }
        if (canvas.transform.Find("StairsPanel") != null)
        {
            EditorUtility.DisplayDialog("계단 UI", "이미 StairsPanel이 있습니다. 다시 만들려면 기존 것을 삭제한 뒤 실행하세요.", "확인");
            return;
        }

        BuildStairsUIInternal(canvas, hud);
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[GameUIBuilder] 계단 UI 생성 완료.");
    }

    /// <summary>
    /// 계단 [내려가기]/[귀환] 버튼 생성 및 연결 (탐험 규칙 — 계단 발견 시 선택).
    /// Explore 중 방향 선택 패널과 함께 표시되므로, 남쪽 방향 슬롯(하단 y≈220)과
    /// 겹치지 않게 그 위(y=420)에 배치. 표시/숨김은 GameHUD가 계단 확보 여부(CanReturn)로 토글.
    /// </summary>
    static void BuildStairsUIInternal(Canvas canvas, GameHUD hud)
    {
        UpdateUiScale(canvas);

        var panelGO = new GameObject("StairsPanel", typeof(RectTransform));
        panelGO.transform.SetParent(canvas.transform, false);
        var prt = panelGO.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, S(420f));
        prt.sizeDelta = new Vector2(S(900f), S(140f));

        Button descend = MakeButton(panelGO.transform, "DescendButton", "내려가기 ▼", new Color(0.22f, 0.62f, 0.40f), 36);
        var drt = descend.GetComponent<RectTransform>();
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.anchoredPosition = new Vector2(-S(200f), 0f);
        drt.sizeDelta = new Vector2(S(340f), S(120f));
        WireButton(descend, hud, nameof(GameHUD.OnClickDescendStairs));

        Button ret = MakeButton(panelGO.transform, "ReturnButton", "귀환", new Color(0.30f, 0.45f, 0.85f), 36);
        var rrt2 = ret.GetComponent<RectTransform>();
        rrt2.anchorMin = rrt2.anchorMax = new Vector2(0.5f, 0.5f);
        rrt2.anchoredPosition = new Vector2(S(200f), 0f);
        rrt2.sizeDelta = new Vector2(S(340f), S(120f));
        WireButton(ret, hud, nameof(GameHUD.OnClickReturnFromStairs));

        hud.stairsPanel = panelGO;
        hud.descendButton = descend.gameObject;
        panelGO.SetActive(false);
        EditorUtility.SetDirty(hud);
    }

    // ================= 생성 헬퍼 =================

    /// <summary>슬롯 프레임 + Icon(Image) + Label(Text) 구조 생성</summary>
    static GameObject MakeItemSlot<T>(Transform parent, string name, Sprite iconSprite, int fontSize) where T : Component
    {
        var frame = MakeImage(parent, name, new Color(0.16f, 0.18f, 0.26f, 0.95f), rounded: true);
        frame.AddComponent<T>();

        var icon = MakeImage(frame.transform, "Icon", new Color(0.55f, 0.75f, 1f), rounded: iconSprite == null);
        if (iconSprite != null)
        {
            var img = icon.GetComponent<Image>();
            img.sprite = iconSprite;
            img.color = new Color(1f, 0.35f, 0.55f); // 포션 색
        }
        var iconRT = icon.GetComponent<RectTransform>();
        iconRT.anchorMin = Vector2.zero;
        iconRT.anchorMax = Vector2.one;
        iconRT.offsetMin = new Vector2(12f, 12f);
        iconRT.offsetMax = new Vector2(-12f, -12f);

        StretchText(MakeText(icon.transform, "Label", "", fontSize));
        return frame;
    }

    static GameObject MakeImage(Transform parent, string name, Color color, bool rounded)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        if (rounded)
        {
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
        }
        return go;
    }

    static Button MakeButton(Transform parent, string name, string label, Color color, int fontSize)
    {
        var go = MakeImage(parent, name, color, rounded: true);
        var btn = go.AddComponent<Button>();
        StretchText(MakeText(go.transform, "Label", label, fontSize));
        return btn;
    }

    static void WireButton(Button btn, GameHUD hud, string methodName)
    {
        UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick,
            (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), hud, methodName));
    }

    static Text MakeText(Transform parent, string name, string content, int fontSize)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = content;
        t.raycastTarget = false;
        return t;
    }

    static void SetAnchored(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void StretchText(Text t)
    {
        var rt = t.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Font LoadFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null)
        {
            try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return f;
    }
}