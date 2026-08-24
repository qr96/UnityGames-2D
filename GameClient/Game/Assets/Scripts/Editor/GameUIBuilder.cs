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

    [MenuItem("Tools/GrabProto/게임 UI 생성")]
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
            scaler.referenceResolution = new Vector2(1080f, 1920f);
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

    [MenuItem("Tools/GrabProto/영웅 장비 패널 생성")]
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
            EditorUtility.DisplayDialog("영웅 장비 패널", "이미 PartyEquipPanel이 있습니다.다시 만들려면 기존 것을 삭제한 뒤 실행하세요.", "확인");
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

    [MenuItem("Tools/GrabProto/인벤토리 버튼 생성")]
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

    static void UpdateUiScale(Canvas canvas)
    {
        var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
        uiScale = (scaler != null && scaler.referenceResolution.x > 0f)
            ? scaler.referenceResolution.x / 1080f
            : 1f;
    }

    [MenuItem("Tools/GrabProto/야영지 UI 생성")]
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