using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 에디터 메뉴 [Tools > GrabProto > 로비 씬 구성]
/// 현재 열린 씬(로비용 빈 씬)에 카메라 / LobbyController / 드래그 / 고정 UI를 한 번에 생성.
/// Assets/GameData의 HeroDatabase가 있으면 자동 연결.
/// 생성 후에는 일반 씬 오브젝트 — 배치/디자인은 에디터에서 자유롭게 수정.
/// </summary>
public static class LobbySceneBuilder
{
    static Font font;
    static float uiScale = 1f; // 캔버스 기준 해상도 보정 (1080 폭 설계 기준)

    /// <summary>설계 좌표(1080 기준)를 씬 캔버스의 기준 해상도에 맞게 보정</summary>
    static float S(float value) => value * uiScale;

    static void UpdateUiScale(Canvas canvas)
    {
        var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
        uiScale = (scaler != null && scaler.referenceResolution.x > 0f)
            ? scaler.referenceResolution.x / 1080f
            : 1f;
    }

    [MenuItem("Tools/GrabProto/로비 씬 구성")]
    public static void Build()
    {
        if (Object.FindFirstObjectByType<LobbyController>() != null)
        {
            EditorUtility.DisplayDialog("로비 씬 구성", "이미 LobbyController가 있는 씬입니다.", "확인");
            return;
        }

        font = LoadFont();

        // ---- 카메라 ----
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            cam = go.GetComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 7f;
        cam.transform.position = new Vector3(0f, 0.5f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.09f, 0.08f);

        // ---- 로비 ----
        var lobbyGO = new GameObject("Lobby");
        var lobby = lobbyGO.AddComponent<LobbyController>();
        lobby.heroDatabase = AssetDatabase.LoadAssetAtPath<HeroDatabase>("Assets/GameData/Heroes/HeroDatabase.asset");
        if (lobby.heroDatabase == null)
            Debug.LogWarning("[LobbySceneBuilder] HeroDatabase 에셋이 없습니다. 먼저 [게임 데이터 에셋 생성]을 실행하면 연결됩니다.");

        new GameObject("LobbyCameraDrag").AddComponent<LobbyCameraDrag>();

        // ---- 고정 UI (GDD 4: 영웅 관리 / 출정 / 설정) ----
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        if (Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var hud = canvasGO.AddComponent<LobbyHUD>();
        Transform root = canvasGO.transform;

        // 타이틀 (좌상단)
        Text title = MakeText(root, "Grab Hero", 46);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f);
        trt.pivot = new Vector2(0f, 1f);
        trt.anchoredPosition = new Vector2(40f, -40f);
        trt.sizeDelta = new Vector2(500f, 80f);
        title.alignment = TextAnchor.MiddleLeft;

        // 설정 (우상단)
        Button settings = MakeButton(root, "SettingsButton", "설정", new Color(0.25f, 0.27f, 0.33f), 30);
        var srt = settings.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(1f, 1f);
        srt.anchoredPosition = new Vector2(-40f, -36f);
        srt.sizeDelta = new Vector2(150f, 84f);
        Wire(settings, hud, nameof(LobbyHUD.OnClickSettings));

        // 영웅 관리 / 출정 (하단)
        Button manage = MakeButton(root, "HeroManageButton", "영웅 관리", new Color(0.25f, 0.32f, 0.5f), 36);
        var mrt = manage.GetComponent<RectTransform>();
        mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0f);
        mrt.anchoredPosition = new Vector2(-250f, 120f);
        mrt.sizeDelta = new Vector2(420f, 130f);
        Wire(manage, hud, nameof(LobbyHUD.OnClickHeroManage));

        Button sortie = MakeButton(root, "SortieButton", "출정 ▶", new Color(0.22f, 0.62f, 0.40f), 38);
        var sortRT = sortie.GetComponent<RectTransform>();
        sortRT.anchorMin = sortRT.anchorMax = new Vector2(0.5f, 0f);
        sortRT.anchoredPosition = new Vector2(250f, 120f);
        sortRT.sizeDelta = new Vector2(420f, 130f);
        Wire(sortie, hud, nameof(LobbyHUD.OnClickSortie));

        // ---- 영웅 관리 패널 ----
        BuildHeroManageUIInternal(canvas, hud);

        // ---- 출정 패널 ----
        BuildSortieUIInternal(canvas, hud);

        EditorSceneManager.MarkSceneDirty(lobbyGO.scene);
        Debug.Log("[LobbySceneBuilder] 로비 씬 구성 완료. 배치/색은 에디터에서 자유롭게 수정하세요.");
    }

    [MenuItem("Tools/GrabProto/로비 영웅 관리 UI 생성")]
    public static void BuildHeroManageUI()
    {
        font = LoadFont();

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        LobbyHUD hud = canvas != null ? canvas.GetComponent<LobbyHUD>() : null;
        if (canvas == null || hud == null)
        {
            EditorUtility.DisplayDialog("영웅 관리 UI", "Canvas/LobbyHUD가 없습니다. 먼저 [로비 씬 구성]을 실행하세요.", "확인");
            return;
        }
        if (canvas.transform.Find("HeroManagePanel") != null)
        {
            EditorUtility.DisplayDialog("영웅 관리 UI", "이미 HeroManagePanel이 있습니다.", "확인");
            return;
        }

        BuildHeroManageUIInternal(canvas, hud);
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[LobbySceneBuilder] 영웅 관리 UI 생성 완료.");
    }

    [MenuItem("Tools/GrabProto/로비 목록 스크롤 적용")]
    public static void AddListScrolls()
    {
        int applied = 0;

        var manage = Object.FindFirstObjectByType<HeroManagePanel>(FindObjectsInactive.Include);
        if (manage != null && manage.listRoot != null && RetrofitScroll((RectTransform)manage.listRoot))
            applied++;

        var sortie = Object.FindFirstObjectByType<SortiePanel>(FindObjectsInactive.Include);
        if (sortie != null && sortie.listRoot != null && RetrofitScroll((RectTransform)sortie.listRoot))
            applied++;

        if (applied > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[LobbySceneBuilder] 목록 스크롤 적용 완료 ({applied}개 패널).");
        }
        else
        {
            EditorUtility.DisplayDialog("목록 스크롤",
                "적용할 목록이 없습니다 (패널 없음 또는 이미 적용됨).", "확인");
        }
    }

    /// <summary>
    /// 기존 목록(ListRoot)에 스크롤 구조를 씌움:
    /// ListRoot 자리에 ListScroll(ScrollRect + RectMask2D)을 만들고
    /// ListRoot를 그 안의 콘텐츠로 옮김 (자동 높이). 기존 항목/배치는 보존.
    /// </summary>
    static bool RetrofitScroll(RectTransform listRoot)
    {
        if (listRoot.GetComponentInParent<ScrollRect>(true) != null) return false; // 이미 적용됨

        // 스크롤 컨테이너 — ListRoot의 기존 자리/크기를 그대로 차지
        var scrollGO = new GameObject("ListScroll",
            typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollGO.transform.SetParent(listRoot.parent, false);
        scrollGO.transform.SetSiblingIndex(listRoot.GetSiblingIndex());
        scrollRT.anchorMin = listRoot.anchorMin;
        scrollRT.anchorMax = listRoot.anchorMax;
        scrollRT.pivot = listRoot.pivot;
        scrollRT.anchoredPosition = listRoot.anchoredPosition;
        scrollRT.sizeDelta = listRoot.sizeDelta;

        // ListRoot → 콘텐츠로: 위쪽 고정 + 가로 스트레치 + 높이는 내용에 맞게 자동
        listRoot.SetParent(scrollGO.transform, false);
        listRoot.anchorMin = new Vector2(0f, 1f);
        listRoot.anchorMax = new Vector2(1f, 1f);
        listRoot.pivot = new Vector2(0.5f, 1f);
        listRoot.anchoredPosition = Vector2.zero;
        listRoot.sizeDelta = Vector2.zero;
        var fitter = listRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.content = listRoot;
        scroll.viewport = scrollRT;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        EditorUtility.SetDirty(scrollGO);
        return true;
    }

    [MenuItem("Tools/GrabProto/로비 출정 UI 생성")]
    public static void BuildSortieUI()
    {
        font = LoadFont();

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        LobbyHUD hud = canvas != null ? canvas.GetComponent<LobbyHUD>() : null;
        if (canvas == null || hud == null)
        {
            EditorUtility.DisplayDialog("출정 UI", "Canvas/LobbyHUD가 없습니다. 먼저 [로비 씬 구성]을 실행하세요.", "확인");
            return;
        }
        if (canvas.transform.Find("SortiePanel") != null)
        {
            EditorUtility.DisplayDialog("출정 UI", "이미 SortiePanel이 있습니다.", "확인");
            return;
        }

        BuildSortieUIInternal(canvas, hud);
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[LobbySceneBuilder] 출정 UI 생성 완료.");
    }

    /// <summary>출정 패널 (파티 3명 선택 → 출발) 생성 및 연결 — 캔버스 기준 해상도에 맞게 크기 보정</summary>
    static void BuildSortieUIInternal(Canvas canvas, LobbyHUD hud)
    {
        UpdateUiScale(canvas);

        var panelGO = new GameObject("SortiePanel", typeof(Image));
        panelGO.transform.SetParent(canvas.transform, false);
        var panelImg = panelGO.GetComponent<Image>();
        panelImg.color = new Color(0.06f, 0.07f, 0.10f, 0.97f);
        panelImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        panelImg.type = Image.Type.Sliced;
        var prt = panelGO.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(S(960f), S(1500f));

        var panel = panelGO.AddComponent<SortiePanel>();
        panel.lobby = Object.FindFirstObjectByType<LobbyController>();

        // 제목
        Text title = MakeText(panelGO.transform, "출정 — 파티를 선택하세요", S(42f));
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -S(70f));
        trt.sizeDelta = new Vector2(S(800f), S(80f));

        // 닫기
        Button close = MakeButton(panelGO.transform, "CloseButton", "✕", new Color(0.55f, 0.22f, 0.25f), S(34f));
        var crt = close.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-S(16f), -S(16f));
        crt.sizeDelta = new Vector2(S(76f), S(76f));
        Wire(close, panel, nameof(SortiePanel.Close));

        // 목록
        var listGO = new GameObject("ListRoot", typeof(RectTransform), typeof(VerticalLayoutGroup));
        listGO.transform.SetParent(panelGO.transform, false);
        var lrt = listGO.GetComponent<RectTransform>();
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f);
        lrt.pivot = new Vector2(0.5f, 1f);
        lrt.anchoredPosition = new Vector2(0f, -S(150f));
        lrt.sizeDelta = new Vector2(S(840f), S(900f));
        var layout = listGO.GetComponent<VerticalLayoutGroup>();
        layout.spacing = S(14f);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Button entry = MakeButton(listGO.transform, "EntryTemplate", "영웅 이름", new Color(0.16f, 0.20f, 0.30f), S(32f));
        entry.GetComponent<RectTransform>().sizeDelta = new Vector2(S(820f), S(110f));
        entry.gameObject.SetActive(false);

        // 선택 카운트
        Text count = MakeText(panelGO.transform, "파티 선택  0 / 3", S(34f));
        var cntRT = count.rectTransform;
        cntRT.anchorMin = cntRT.anchorMax = new Vector2(0.5f, 0f);
        cntRT.pivot = new Vector2(0.5f, 0f);
        cntRT.anchoredPosition = new Vector2(0f, S(200f));
        cntRT.sizeDelta = new Vector2(S(700f), S(70f));

        // 출발 버튼
        Button depart = MakeButton(panelGO.transform, "DepartButton", "출발 ▶", new Color(0.22f, 0.62f, 0.40f), S(38f));
        var drt = depart.GetComponent<RectTransform>();
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0f);
        drt.pivot = new Vector2(0.5f, 0f);
        drt.anchoredPosition = new Vector2(0f, S(60f));
        drt.sizeDelta = new Vector2(S(520f), S(120f));
        Wire(depart, panel, nameof(SortiePanel.OnClickDepart));

        // 연결
        panel.listRoot = listGO.transform;
        panel.entryTemplate = entry.gameObject;
        panel.countText = count;
        panel.departButton = depart;
        hud.sortiePanel = panel;
        RetrofitScroll(lrt); // 목록 스크롤 (16인 로스터 대응)
        panelGO.SetActive(false);

        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(hud);
    }

    /// <summary>영웅 관리 패널 (목록 위 / 상세 아래) 생성 및 연결</summary>
    static void BuildHeroManageUIInternal(Canvas canvas, LobbyHUD hud)
    {
        // 패널 본체
        var panelGO = new GameObject("HeroManagePanel", typeof(Image));
        panelGO.transform.SetParent(canvas.transform, false);
        var panelImg = panelGO.GetComponent<Image>();
        panelImg.color = new Color(0.06f, 0.07f, 0.10f, 0.97f);
        panelImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        panelImg.type = Image.Type.Sliced;
        var prt = panelGO.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(960f, 1560f);

        var panel = panelGO.AddComponent<HeroManagePanel>();
        panel.lobby = Object.FindFirstObjectByType<LobbyController>();

        // 제목
        Text title = MakeText(panelGO.transform, "영웅 관리", 44);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -70f);
        trt.sizeDelta = new Vector2(600f, 80f);

        // 닫기
        Button close = MakeButton(panelGO.transform, "CloseButton", "✕", new Color(0.55f, 0.22f, 0.25f), 34);
        var crt = close.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-16f, -16f);
        crt.sizeDelta = new Vector2(76f, 76f);
        Wire(close, panel, nameof(HeroManagePanel.Close));

        // 목록 (위쪽) — 항목 템플릿 복제 방식
        var listGO = new GameObject("ListRoot", typeof(RectTransform), typeof(VerticalLayoutGroup));
        listGO.transform.SetParent(panelGO.transform, false);
        var lrt = listGO.GetComponent<RectTransform>();
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f);
        lrt.pivot = new Vector2(0.5f, 1f);
        lrt.anchoredPosition = new Vector2(0f, -150f);
        lrt.sizeDelta = new Vector2(840f, 780f);
        var layout = listGO.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Button entry = MakeButton(listGO.transform, "EntryTemplate", "영웅 이름", new Color(0.16f, 0.20f, 0.30f), 32);
        entry.GetComponent<RectTransform>().sizeDelta = new Vector2(820f, 110f);
        entry.gameObject.SetActive(false);

        // 상세 (아래쪽)
        Text detail = MakeText(panelGO.transform, "영웅을 선택하세요.", 32);
        detail.alignment = TextAnchor.UpperLeft;
        var drt = detail.rectTransform;
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0f);
        drt.pivot = new Vector2(0.5f, 0f);
        drt.anchoredPosition = new Vector2(0f, 60f);
        drt.sizeDelta = new Vector2(820f, 540f);

        // 연결
        panel.listRoot = listGO.transform;
        panel.entryTemplate = entry.gameObject;
        panel.detailText = detail;
        hud.heroManagePanel = panel;
        RetrofitScroll(lrt); // 목록 스크롤 (16인 로스터 대응)
        panelGO.SetActive(false);

        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(hud);
    }

    // ---------- 생성 헬퍼 ----------

    static Button MakeButton(Transform parent, string name, string label, Color color, float fontSize)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;

        Text t = MakeText(go.transform, label, fontSize);
        var rt = t.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }

    static Text MakeText(Transform parent, string content, float fontSize)
    {
        var go = new GameObject("Text", typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = Mathf.RoundToInt(fontSize);
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = content;
        t.raycastTarget = false;
        return t;
    }

    static void Wire(Button btn, Object target, string methodName)
    {
        UnityEventTools.AddPersistentListener(btn.onClick,
            (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), target, methodName));
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