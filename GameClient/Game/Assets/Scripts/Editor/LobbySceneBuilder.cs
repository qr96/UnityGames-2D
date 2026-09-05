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

    /// <summary>상세/카드 공용 — 회색 소자 라벨 (x=28 고정)</summary>
    static void MakeDetailLabel(Transform parent, Color color, string text, float y)
    {
        Text label = MakeText(parent, text, 24);
        label.color = color;
        label.alignment = TextAnchor.UpperLeft;
        PlaceTopLeft(label.rectTransform, 28f, y, 90f, 28f);
    }

    /// <summary>카드 내부 요소 배치 — 좌상단 기준 (x, y: 카드 위에서 아래로)</summary>
    static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(S(x), -S(y));
        rt.sizeDelta = new Vector2(S(w), S(h));
    }

    static void UpdateUiScale(Canvas canvas)
    {
        var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
        uiScale = (scaler != null && scaler.referenceResolution.x > 0f)
            ? scaler.referenceResolution.x / 1080f
            : 1f;
    }

    [MenuItem("Tools/GrabProto/로비 씬 구성", false, 1)]
    public static void Build()
    {
        // UI(캔버스+HUD)가 이미 있으면 중단 — 없으면 진행 (LobbyController 등 비UI 요소는 재사용)
        var existingCanvas = Object.FindFirstObjectByType<Canvas>();
        if (existingCanvas != null && existingCanvas.GetComponent<LobbyHUD>() != null)
        {
            EditorUtility.DisplayDialog("로비 씬 구성",
                "이미 구성된 씬입니다.\nUI를 다시 만들려면 Canvas를 삭제 후 재실행,\n누락분만 채우려면 [로비 UI 패치]를 사용하세요.", "확인");
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

        // ---- 로비 (있으면 재사용 — UI만 재구축하는 경우) ----
        var lobby = Object.FindFirstObjectByType<LobbyController>();
        if (lobby == null)
            lobby = new GameObject("Lobby").AddComponent<LobbyController>();
        var lobbyGO = lobby.gameObject;
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
        scaler.referenceResolution = new Vector2(1080f, 1440f); // 3:4 세로 (화면 비율 개편)
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
        Button manage = MakeButton(root, "HeroManageButton", "영웅 관리", new Color(0.25f, 0.32f, 0.5f), 30);
        var mrt = manage.GetComponent<RectTransform>();
        mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0f);
        mrt.anchoredPosition = new Vector2(130f, 90f);   // 하단 바 3번째 (화면 비율 개편)
        mrt.sizeDelta = new Vector2(250f, 110f);
        Wire(manage, hud, nameof(LobbyHUD.OnClickHeroManage));

        Button sortie = MakeButton(root, "SortieButton", "출정 ▶", new Color(0.22f, 0.62f, 0.40f), 30);
        var sortRT = sortie.GetComponent<RectTransform>();
        sortRT.anchorMin = sortRT.anchorMax = new Vector2(0.5f, 0f);
        sortRT.anchoredPosition = new Vector2(390f, 90f); // 하단 바 4번째
        sortRT.sizeDelta = new Vector2(250f, 110f);
        Wire(sortie, hud, nameof(LobbyHUD.OnClickSortie));

        // 영입 (하단 중앙 위 — 영입 스펙 v1)
        Button armoryBtn2 = MakeButton(root, "ArmoryButton", "보관소", new Color(0.32f, 0.42f, 0.38f), 30);
        var abrt2 = armoryBtn2.GetComponent<RectTransform>();
        abrt2.anchorMin = abrt2.anchorMax = new Vector2(0.5f, 0f);
        abrt2.anchoredPosition = new Vector2(-390f, 90f); // 하단 바 1번째
        abrt2.sizeDelta = new Vector2(250f, 110f);
        Wire(armoryBtn2, hud, nameof(LobbyHUD.OnClickArmory));

        Button recruitBtn = MakeButton(root, "RecruitButton", "영입", new Color(0.55f, 0.40f, 0.20f), 30);
        var rbrt = recruitBtn.GetComponent<RectTransform>();
        rbrt.anchorMin = rbrt.anchorMax = new Vector2(0.5f, 0f);
        rbrt.anchoredPosition = new Vector2(-130f, 90f); // 하단 바 2번째
        rbrt.sizeDelta = new Vector2(250f, 110f);
        Wire(recruitBtn, hud, nameof(LobbyHUD.OnClickRecruit));

        // ---- 영웅 관리 패널 ----
        BuildHeroManageUIInternal(canvas, hud);

        // ---- 출정 패널 ----
        BuildSortieUIInternal(canvas, hud);

        // ---- 영입 패널 ----
        BuildRecruitUIInternal(canvas, hud);

        // ---- 출발 지점 / 목록 스크롤 (풀빌드에 포함 — 개별 메뉴는 재생성용) ----
        // 보관소 열람 패널은 폐기 — [보관소] 버튼이 영웅 관리 [장비] 탭을 연다 (장비 관리 개편 ①)
        BuildStartFloorUI();
        ApplyListScrollsSilent(); // 풀빌드 중에는 다이얼로그 없이

        EditorSceneManager.MarkSceneDirty(lobbyGO.scene);
        Debug.Log("[LobbySceneBuilder] 로비 씬 구성 완료. 배치/색은 에디터에서 자유롭게 수정하세요.");
    }

    [MenuItem("Tools/GrabProto/로비 UI 패치 (누락 보완)", false, 2)]
    public static void PatchLobbyUI()
    {
        font = LoadFont();

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        LobbyHUD hud = canvas != null ? canvas.GetComponent<LobbyHUD>() : null;
        if (canvas == null || hud == null)
        {
            EditorUtility.DisplayDialog("로비 UI 패치", "Canvas/LobbyHUD가 없습니다. 먼저 [로비 씬 구성]을 실행하세요.", "확인");
            return;
        }

        ApplyCanvasStandard(canvas); // 3:4 전환 — 기존 씬 마이그레이션 겸용

        var applied = new System.Collections.Generic.List<string>();

        // 패널 단위 — 없으면 통째로 생성
        if (canvas.transform.Find("HeroManagePanel") == null) { BuildHeroManageUI(); applied.Add("영웅 관리 패널"); }
        if (canvas.transform.Find("SortiePanel") == null) { BuildSortieUI(); applied.Add("출정 패널"); }
        if (canvas.transform.Find("RecruitPanel") == null) { BuildRecruitUI(); applied.Add("영입 패널"); }
        if (canvas.transform.Find("ArmoryButton") == null)
        {
            Button armoryBtn = MakeButton(canvas.transform, "ArmoryButton", "보관소", new Color(0.32f, 0.42f, 0.38f), S(30f));
            var abrt = armoryBtn.GetComponent<RectTransform>();
            abrt.anchorMin = abrt.anchorMax = new Vector2(0.5f, 0f);
            abrt.anchoredPosition = new Vector2(-S(390f), S(90f));
            abrt.sizeDelta = new Vector2(S(250f), S(110f));
            Wire(armoryBtn, hud, nameof(LobbyHUD.OnClickArmory));
            applied.Add("보관소 버튼");
        }

        // 요소 단위 — 패널은 있는데 뒤에 추가된 조각이 빠진 경우
        var manage = Object.FindFirstObjectByType<HeroManagePanel>(FindObjectsInactive.Include);
        if (manage != null && manage.dismissButton == null && manage.transform.Find("DismissButton") == null)
        {
            PatchDismissButton(canvas);
            applied.Add("해고 버튼");
        }

        var sortie = Object.FindFirstObjectByType<SortiePanel>(FindObjectsInactive.Include);
        if (sortie != null && sortie.startFloorText == null && sortie.transform.Find("StartFloorButton") == null)
        {
            BuildStartFloorUI();
            applied.Add("출발 지점 버튼");
        }

        // 목록 스크롤 (RetrofitScroll이 자체 멱등)
        int scrolls = ApplyListScrollsSilent();
        if (scrolls > 0) applied.Add($"목록 스크롤 x{scrolls}");

        if (applied.Count > 0)
        {
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log($"[LobbySceneBuilder] 패치 적용: {string.Join(", ", applied)}");
            EditorUtility.DisplayDialog("로비 UI 패치", $"적용됨:\n- {string.Join("\n- ", applied)}", "확인");
        }
        else
        {
            EditorUtility.DisplayDialog("로비 UI 패치", "누락된 요소 없음 — 최신 상태입니다.", "확인");
        }
    }

    [MenuItem("Tools/GrabProto/재생성/로비/영웅 관리 패널", false, 101)]
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

    /// <summary>풀빌드/패치용 — 다이얼로그 없이 스크롤 적용, 적용 수 반환</summary>
    static int ApplyListScrollsSilent()
    {
        int applied = 0;
        var manage = Object.FindFirstObjectByType<HeroManagePanel>(FindObjectsInactive.Include);
        if (manage != null && manage.listRoot != null && RetrofitScroll((RectTransform)manage.listRoot))
            applied++;
        var sortie = Object.FindFirstObjectByType<SortiePanel>(FindObjectsInactive.Include);
        if (sortie != null && sortie.listRoot != null && RetrofitScroll((RectTransform)sortie.listRoot))
            applied++;
        return applied;
    }

    [MenuItem("Tools/GrabProto/재생성/로비/목록 스크롤", false, 102)]
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

    [MenuItem("Tools/GrabProto/재생성/로비/출정 패널", false, 103)]
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

    [MenuItem("Tools/GrabProto/재생성/로비/출발 지점 버튼", false, 105)]
    public static void BuildStartFloorUI()
    {
        font = LoadFont();

        var sortie = Object.FindFirstObjectByType<SortiePanel>(FindObjectsInactive.Include);
        if (sortie == null)
        {
            EditorUtility.DisplayDialog("출발 지점 UI", "SortiePanel이 없습니다. 먼저 [로비 씬 구성]을 실행하세요.", "확인");
            return;
        }
        if (sortie.startFloorText != null || sortie.transform.Find("StartFloorButton") != null)
        {
            EditorUtility.DisplayDialog("출발 지점 UI", "이미 출발 지점 버튼이 있습니다.", "확인");
            return;
        }

        Canvas canvas = sortie.GetComponentInParent<Canvas>(true);
        UpdateUiScale(canvas);

        // 출발 층 순환 버튼 — 출발 버튼 왼쪽 하단에 배치
        Button floorBtn = MakeButton(sortie.transform, "StartFloorButton",
            "출발: 지하 1층 (입구)", new Color(0.30f, 0.34f, 0.48f), S(26f));
        var frt = floorBtn.GetComponent<RectTransform>();
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0f);
        frt.pivot = new Vector2(0.5f, 0f);
        frt.anchoredPosition = new Vector2(0f, S(200f)); // 출발 버튼 바로 위 (겹침 해소)
        frt.sizeDelta = new Vector2(S(520f), S(85f));
        Wire(floorBtn, sortie, nameof(SortiePanel.OnClickCycleStartFloor));

        sortie.startFloorText = floorBtn.GetComponentInChildren<Text>(true);
        EditorUtility.SetDirty(sortie);
        EditorSceneManager.MarkSceneDirty(sortie.gameObject.scene);
        Debug.Log("[LobbySceneBuilder] 출발 지점 버튼 생성 완료.");
    }

    [MenuItem("Tools/GrabProto/재생성/로비/영입 패널", false, 106)]
    public static void BuildRecruitUI()
    {
        font = LoadFont();

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        LobbyHUD hud = canvas != null ? canvas.GetComponent<LobbyHUD>() : null;
        if (canvas == null || hud == null)
        {
            EditorUtility.DisplayDialog("영입 UI", "Canvas/LobbyHUD가 없습니다. 먼저 [로비 씬 구성]을 실행하세요.", "확인");
            return;
        }
        if (canvas.transform.Find("RecruitPanel") != null)
        {
            EditorUtility.DisplayDialog("영입 UI", "이미 RecruitPanel이 있습니다. 다시 만들려면 기존 것을 삭제한 뒤 실행하세요.", "확인");
            return;
        }

        UpdateUiScale(canvas);

        // HUD 영입 버튼 (기존 씬 패치 — 없으면 생성)
        if (canvas.transform.Find("RecruitButton") == null)
        {
            Button recruitBtn = MakeButton(canvas.transform, "RecruitButton", "영입", new Color(0.55f, 0.40f, 0.20f), S(30f));
            var rbrt = recruitBtn.GetComponent<RectTransform>();
            rbrt.anchorMin = rbrt.anchorMax = new Vector2(0.5f, 0f);
            rbrt.anchoredPosition = new Vector2(-S(130f), S(90f)); // 하단 바 2번째
            rbrt.sizeDelta = new Vector2(S(250f), S(110f));
            Wire(recruitBtn, hud, nameof(LobbyHUD.OnClickRecruit));
        }

        BuildRecruitUIInternal(canvas, hud);
        PatchDismissButton(canvas); // 기존 영웅 관리 패널에 [해고] 버튼 주입

        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[LobbySceneBuilder] 영입 UI 생성 완료 (+ 영웅 관리 패널에 해고 버튼 적용).");
    }

    /// <summary>영입 상점 패널 — 후보 3칸 (정보 전부 공개 + 영입 버튼) + 골드 표시</summary>
    static void BuildRecruitUIInternal(Canvas canvas, LobbyHUD hud)
    {
        UpdateUiScale(canvas);

        var panelGO = new GameObject("RecruitPanel", typeof(Image));
        panelGO.transform.SetParent(canvas.transform, false);
        var panelImg = panelGO.GetComponent<Image>();
        panelImg.color = new Color(0.06f, 0.07f, 0.10f, 0.97f);
        panelImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        panelImg.type = Image.Type.Sliced;
        var prt = panelGO.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(S(1020f), S(1370f)); // 3:4 화면(1440) 내 — 폰트 상향분 확보

        var panel = panelGO.AddComponent<RecruitPanel>();
        panel.lobby = Object.FindFirstObjectByType<LobbyController>();

        // 제목 / 닫기 / 골드
        Text title = MakeText(panelGO.transform, "영입 — 떠돌이 용병", S(42f));
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -S(70f));
        trt.sizeDelta = new Vector2(S(800f), S(80f));

        Button close = MakeButton(panelGO.transform, "CloseButton", "✕", new Color(0.55f, 0.22f, 0.25f), S(34f));
        var crt = close.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-S(16f), -S(16f));
        crt.sizeDelta = new Vector2(S(76f), S(76f));
        Wire(close, panel, nameof(RecruitPanel.Close));

        Text gold = MakeText(panelGO.transform, "골드  0", S(30f));
        var grt = gold.rectTransform;
        grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 1f);
        grt.anchoredPosition = new Vector2(0f, -S(135f));
        grt.sizeDelta = new Vector2(S(940f), S(50f));
        panel.goldText = gold;

        // 후보 카드 3장 — 가로형(전폭) 세로 스택 + 필드 분해 구조 (가독성 개편):
        //   이름(볼드) / 특성 뱃지(우상단) / 스탯 4칸(라벨·값·보조) / 액티브 / 특성 설명
        //   라벨·보조는 회색 소자, 값은 크고 흰색 — "찾아 읽는" 스캔형 배치
        string[] recruitHandlers =
        {
            nameof(RecruitPanel.OnClickRecruit0),
            nameof(RecruitPanel.OnClickRecruit1),
            nameof(RecruitPanel.OnClickRecruit2),
        };
        const float cardW = 940f, cardH = 350f, gap = 18f; // 폰트 상향분 반영
        Color subColor = new Color(0.56f, 0.61f, 0.70f);   // 라벨/보조 회색
        string[] statLabels = { "HP", "공격", "치확", "치피" };
        float[] statX = { 28f, 224f, 420f, 560f }; // 값 폭에 맞춘 열 위치

        for (int i = 0; i < 3; i++)
        {
            var card = new RecruitPanel.CandidateCard();

            var cardGO = new GameObject($"Candidate{i}", typeof(Image));
            cardGO.transform.SetParent(panelGO.transform, false);
            var cardImg = cardGO.GetComponent<Image>();
            cardImg.color = new Color(0.11f, 0.13f, 0.19f, 0.95f);
            cardImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            cardImg.type = Image.Type.Sliced;
            var cardRT = cardGO.GetComponent<RectTransform>();
            cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 1f);
            cardRT.pivot = new Vector2(0.5f, 1f);
            cardRT.anchoredPosition = new Vector2(0f, -S(200f + i * (cardH + gap)));
            cardRT.sizeDelta = new Vector2(S(cardW), S(cardH));

            // 이름 (좌상단, 볼드)
            Text nameT = MakeText(cardGO.transform, "이름", S(36f));
            nameT.fontStyle = FontStyle.Bold;
            nameT.alignment = TextAnchor.UpperLeft;
            PlaceTopLeft(nameT.rectTransform, 28f, 16f, 460f, 48f);
            card.nameText = nameT;

            // 스탯 4칸: 라벨(회색 소자) / 값(크게) / 보조(회색 소자)
            for (int s = 0; s < 4; s++)
            {
                Text label = MakeText(cardGO.transform, statLabels[s], S(24f));
                label.color = subColor;
                label.alignment = TextAnchor.UpperLeft;
                PlaceTopLeft(label.rectTransform, statX[s], 74f, 160f, 32f);

                Text value = MakeText(cardGO.transform, "-", S(36f));
                value.alignment = TextAnchor.UpperLeft;
                PlaceTopLeft(value.rectTransform, statX[s], 104f, 180f, 48f);
                card.statValues[s] = value;

                Text sub = MakeText(cardGO.transform, "", S(22f));
                sub.color = subColor;
                sub.alignment = TextAnchor.UpperLeft;
                PlaceTopLeft(sub.rectTransform, statX[s], 152f, 180f, 30f);
                card.statSubs[s] = sub;
            }

            // 액티브 줄 (라벨 회색 + 내용은 richText로 이름 강조/조건 회색)
            Text activeLabel = MakeText(cardGO.transform, "액티브", S(24f));
            activeLabel.color = subColor;
            activeLabel.alignment = TextAnchor.UpperLeft;
            PlaceTopLeft(activeLabel.rectTransform, 28f, 198f, 110f, 32f);

            Text activeT = MakeText(cardGO.transform, "-", S(27f));
            activeT.alignment = TextAnchor.UpperLeft;
            PlaceTopLeft(activeT.rectTransform, 148f, 195f, 540f, 38f);
            card.activeText = activeT;

            // 특성 줄 (액티브 줄과 동형: 라벨 회색 + 이름 금색·설명 회색은 richText)
            Text traitLabel = MakeText(cardGO.transform, "특성", S(24f));
            traitLabel.color = subColor;
            traitLabel.alignment = TextAnchor.UpperLeft;
            PlaceTopLeft(traitLabel.rectTransform, 28f, 240f, 110f, 32f);

            Text traitT = MakeText(cardGO.transform, "-", S(27f));
            traitT.alignment = TextAnchor.UpperLeft;
            PlaceTopLeft(traitT.rectTransform, 148f, 237f, 540f, 90f); // 긴 설명 2줄 허용
            card.traitText = traitT;

            // 영입 버튼: 오른쪽 (뱃지 아래)
            Button recruit = MakeButton(cardGO.transform, "RecruitButton",
                "영입", new Color(0.22f, 0.62f, 0.40f), S(24f));
            var rrt = recruit.GetComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = new Vector2(1f, 0f);
            rrt.pivot = new Vector2(1f, 0f);
            rrt.anchoredPosition = new Vector2(-S(28f), S(24f));
            rrt.sizeDelta = new Vector2(S(210f), S(110f));
            Wire(recruit, panel, recruitHandlers[i]);
            card.recruitButton = recruit;

            panel.cards[i] = card;
        }

        // 연결
        hud.recruitPanel = panel;
        panelGO.SetActive(false);

        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(hud);
    }

    /// <summary>기존 영웅 관리 패널에 [해고] 버튼 주입 (없을 때만) — 영입 스펙 v1</summary>
    static void PatchDismissButton(Canvas canvas)
    {
        var manage = Object.FindFirstObjectByType<HeroManagePanel>(FindObjectsInactive.Include);
        if (manage == null || manage.dismissButton != null) return;
        if (manage.transform.Find("DismissButton") != null) return;

        Button dismiss = MakeButton(manage.transform, "DismissButton",
            "해고 (환급 없음)", new Color(0.55f, 0.22f, 0.25f), S(28f));
        var drt = dismiss.GetComponent<RectTransform>();
        drt.anchorMin = drt.anchorMax = new Vector2(1f, 0f);
        drt.pivot = new Vector2(1f, 0f);
        drt.anchoredPosition = new Vector2(-S(30f), S(30f));
        drt.sizeDelta = new Vector2(S(320f), S(90f));
        Wire(dismiss, manage, nameof(HeroManagePanel.OnClickDismiss));

        manage.dismissButton = dismiss;
        EditorUtility.SetDirty(manage);
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
        prt.sizeDelta = new Vector2(S(960f), S(1240f)); // 3:4 화면(1440) 내

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
        lrt.sizeDelta = new Vector2(S(840f), S(660f)); // 패널 축소분 반영
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
        Text count = MakeText(panelGO.transform, "파티 선택  0 / 5", S(34f));
        var cntRT = count.rectTransform;
        cntRT.anchorMin = cntRT.anchorMax = new Vector2(0.5f, 0f);
        cntRT.pivot = new Vector2(0.5f, 0f);
        cntRT.anchoredPosition = new Vector2(0f, S(300f)); // 리스트 아래·출발지점 위
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
        // ---- 패널 본체 (장비 관리 개편 ①: 목록 + 헤더 + 탭 2개) ----
        var panelGO = new GameObject("HeroManagePanel", typeof(Image));
        panelGO.transform.SetParent(canvas.transform, false);
        var panelImg = panelGO.GetComponent<Image>();
        panelImg.color = new Color(0.06f, 0.07f, 0.10f, 0.97f);
        panelImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        panelImg.type = Image.Type.Sliced;
        var prt = panelGO.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(960f, 1240f);

        var panel = panelGO.AddComponent<HeroManagePanel>();

        Text title = MakeText(panelGO.transform, "영웅 관리", 42);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -60f);
        trt.sizeDelta = new Vector2(700f, 70f);

        Button close = MakeButton(panelGO.transform, "CloseButton", "✕", new Color(0.55f, 0.22f, 0.25f), 34);
        var crt = close.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 1f);
        crt.anchoredPosition = new Vector2(-16f, -16f);
        crt.sizeDelta = new Vector2(76f, 76f);
        Wire(close, panel, nameof(HeroManagePanel.Close));

        // ---- 보유 영웅 목록 (스크롤은 패치/풀빌드의 RetrofitScroll 담당) ----
        var listGO = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
        listGO.transform.SetParent(panelGO.transform, false);
        var lrt = listGO.GetComponent<RectTransform>();
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f);
        lrt.pivot = new Vector2(0.5f, 1f);
        lrt.anchoredPosition = new Vector2(0f, -120f);
        lrt.sizeDelta = new Vector2(840f, 300f);
        var layout = listGO.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Button entry = MakeButton(listGO.transform, "EntryTemplate", "영웅", new Color(0.16f, 0.20f, 0.30f), 30);
        entry.GetComponent<RectTransform>().sizeDelta = new Vector2(820f, 84f);
        entry.gameObject.SetActive(false);

        // ---- 공통 헤더 ----
        Text header = MakeText(panelGO.transform, "영웅을 선택하세요", 32);
        header.fontStyle = FontStyle.Bold;
        header.alignment = TextAnchor.MiddleLeft;
        var hrt = header.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = new Vector2(0f, -466f);
        hrt.sizeDelta = new Vector2(840f, 52f);
        panel.headerText = header;

        // ---- 탭 바 ----
        Button infoTab = MakeButton(panelGO.transform, "InfoTab", "정보", new Color(0.30f, 0.45f, 0.85f), 28);
        var itrt = infoTab.GetComponent<RectTransform>();
        itrt.anchorMin = itrt.anchorMax = new Vector2(0.5f, 1f);
        itrt.anchoredPosition = new Vector2(-320f, -528f);
        itrt.sizeDelta = new Vector2(200f, 56f);
        Wire(infoTab, panel, nameof(HeroManagePanel.OnClickInfoTab));
        panel.infoTabButton = infoTab;

        Button equipTab = MakeButton(panelGO.transform, "EquipTab", "장비", new Color(0.14f, 0.16f, 0.24f), 28);
        var etrt = equipTab.GetComponent<RectTransform>();
        etrt.anchorMin = etrt.anchorMax = new Vector2(0.5f, 1f);
        etrt.anchoredPosition = new Vector2(-108f, -528f);
        etrt.sizeDelta = new Vector2(200f, 56f);
        Wire(equipTab, panel, nameof(HeroManagePanel.OnClickEquipTab));
        panel.equipTabButton = equipTab;

        // ---- 탭 내용 박스 (공유 배경) ----
        var boxGO = new GameObject("TabBox", typeof(Image));
        boxGO.transform.SetParent(panelGO.transform, false);
        var boxImg = boxGO.GetComponent<Image>();
        boxImg.color = new Color(0.11f, 0.13f, 0.19f, 0.95f);
        boxImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        boxImg.type = Image.Type.Sliced;
        var brt = boxGO.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0f, -556f);
        brt.sizeDelta = new Vector2(880f, 520f);

        Color subColor = new Color(0.56f, 0.61f, 0.70f);
        string[] statLabels = { "HP", "공격", "치확", "치피" };
        float[] statX = { 28f, 224f, 420f, 560f };

        // ================= [정보] 탭 =================
        var infoGO = new GameObject("InfoRoot", typeof(RectTransform));
        infoGO.transform.SetParent(boxGO.transform, false);
        var irt2 = infoGO.GetComponent<RectTransform>();
        irt2.anchorMin = Vector2.zero;
        irt2.anchorMax = Vector2.one;
        irt2.offsetMin = irt2.offsetMax = Vector2.zero;
        panel.infoRoot = infoGO;

        var iv = panel.info;
        for (int s = 0; s < 4; s++)
        {
            Text label = MakeText(infoGO.transform, statLabels[s], 24);
            label.color = subColor;
            label.alignment = TextAnchor.UpperLeft;
            PlaceTopLeft(label.rectTransform, statX[s], 28f, 160f, 32f);

            Text value = MakeText(infoGO.transform, "-", 36);
            value.alignment = TextAnchor.UpperLeft;
            PlaceTopLeft(value.rectTransform, statX[s], 58f, 180f, 48f);
            iv.statValues[s] = value;

            Text sub = MakeText(infoGO.transform, "", 22);
            sub.color = subColor;
            sub.alignment = TextAnchor.UpperLeft;
            PlaceTopLeft(sub.rectTransform, statX[s], 106f, 180f, 30f);
            iv.statSubs[s] = sub;
        }

        MakeDetailLabel(infoGO.transform, subColor, "액티브", 168f);
        Text dActive = MakeText(infoGO.transform, "-", 27);
        dActive.alignment = TextAnchor.UpperLeft;
        PlaceTopLeft(dActive.rectTransform, 148f, 165f, 700f, 38f);
        iv.activeText = dActive;

        MakeDetailLabel(infoGO.transform, subColor, "특성", 214f);
        Text dTrait = MakeText(infoGO.transform, "-", 27);
        dTrait.alignment = TextAnchor.UpperLeft;
        PlaceTopLeft(dTrait.rectTransform, 148f, 211f, 700f, 82f);
        iv.traitText = dTrait;

        MakeDetailLabel(infoGO.transform, subColor, "장착", 300f);
        Text dSummary = MakeText(infoGO.transform, "-", 27);
        dSummary.alignment = TextAnchor.UpperLeft;
        PlaceTopLeft(dSummary.rectTransform, 148f, 297f, 700f, 38f);
        iv.equipSummaryText = dSummary;

        // ================= [장비] 탭 =================
        var equipGO = new GameObject("EquipRoot", typeof(RectTransform));
        equipGO.transform.SetParent(boxGO.transform, false);
        var ert2 = equipGO.GetComponent<RectTransform>();
        ert2.anchorMin = Vector2.zero;
        ert2.anchorMax = Vector2.one;
        ert2.offsetMin = ert2.offsetMax = Vector2.zero;
        equipGO.SetActive(false);
        panel.equipRoot = equipGO;

        // 슬롯 줄 배경 — 이 영역 아무 데나 드롭하면 스마트 장착 (드롭 정밀도 부담 제거)
        var stripGO = new GameObject("SlotStrip", typeof(Image));
        stripGO.transform.SetParent(equipGO.transform, false);
        var stripImg = stripGO.GetComponent<Image>();
        stripImg.color = new Color(0f, 0f, 0f, 0.22f);
        stripImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        stripImg.type = Image.Type.Sliced;
        PlaceTopLeft(stripGO.GetComponent<RectTransform>(), 20f, 12f, 840f, 130f);
        panel.slotStripArea = stripGO.GetComponent<RectTransform>();

        // 장착 슬롯 줄 (무기 1 + 자유 3 — 축약명 표시, 탭=상세, 드래그=해제)
        for (int i = 0; i < 4; i++)
        {
            bool isWeapon = i == 0;
            var slotGO = new GameObject(isWeapon ? "WeaponSlot" : $"GearSlot{i - 1}", typeof(Image), typeof(Outline));
            slotGO.transform.SetParent(equipGO.transform, false);
            var slotImg = slotGO.GetComponent<Image>();
            slotImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            slotImg.type = Image.Type.Sliced;
            var outline = slotGO.GetComponent<Outline>();
            outline.effectColor = isWeapon ? new Color(0.85f, 0.68f, 0.30f, 0.55f) : new Color(0.55f, 0.60f, 0.75f, 0.40f);
            outline.effectDistance = new Vector2(2f, -2f);
            PlaceTopLeft(slotGO.GetComponent<RectTransform>(), 30f + i * 210f, 22f, 190f, 110f);

            var slotUI = slotGO.AddComponent<LobbyEquipSlotUI>();
            slotUI.isWeaponSlot = isWeapon;
            slotUI.slotIndex = isWeapon ? 0 : i - 1;

            Text slotLabel = MakeText(slotGO.transform, "", 28);
            slotLabel.alignment = TextAnchor.MiddleCenter;
            var slrt = slotLabel.rectTransform;
            slrt.anchorMin = Vector2.zero;
            slrt.anchorMax = Vector2.one;
            slrt.offsetMin = slrt.offsetMax = Vector2.zero;
            slotUI.label = slotLabel;

            panel.equipSlots[i] = slotUI;
        }

        // 상세/안내 줄
        Text detailLine = MakeText(equipGO.transform, "", 24);
        detailLine.alignment = TextAnchor.MiddleLeft;
        PlaceTopLeft(detailLine.rectTransform, 30f, 144f, 820f, 40f);
        panel.detailLine = detailLine;

        // 보관소 제목
        Text storageTitle = MakeText(equipGO.transform, "보관소  (0)", 26);
        storageTitle.alignment = TextAnchor.MiddleLeft;
        storageTitle.color = subColor;
        PlaceTopLeft(storageTitle.rectTransform, 30f, 192f, 400f, 34f);
        panel.storageTitle = storageTitle;

        // 보관소 목록 (스크롤 + 스크롤바 — 행 드래그와 분리)
        var scrollGO = new GameObject("StorageScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollGO.transform.SetParent(equipGO.transform, false);
        PlaceTopLeft(scrollGO.GetComponent<RectTransform>(), 20f, 230f, 840f, 272f);
        scrollGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

        var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var vrt = viewportGO.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero;
        vrt.anchorMax = Vector2.one;
        vrt.offsetMin = Vector2.zero;
        vrt.offsetMax = new Vector2(-24f, 0f);
        viewportGO.GetComponent<Image>().color = Color.white;
        viewportGO.GetComponent<Mask>().showMaskGraphic = false;

        var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGO.transform.SetParent(viewportGO.transform, false);
        var cort = contentGO.GetComponent<RectTransform>();
        cort.anchorMin = new Vector2(0.5f, 1f);
        cort.anchorMax = new Vector2(0.5f, 1f);
        cort.pivot = new Vector2(0.5f, 1f);
        cort.sizeDelta = new Vector2(810f, 0f);
        var slayout = contentGO.GetComponent<VerticalLayoutGroup>();
        slayout.spacing = 8f;
        slayout.padding = new RectOffset(0, 0, 8, 8);
        slayout.childAlignment = TextAnchor.UpperCenter;
        slayout.childControlWidth = false;
        slayout.childControlHeight = false;
        slayout.childForceExpandWidth = false;
        slayout.childForceExpandHeight = false;
        contentGO.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sbGO = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        sbGO.transform.SetParent(scrollGO.transform, false);
        var sbrt = sbGO.GetComponent<RectTransform>();
        sbrt.anchorMin = new Vector2(1f, 0f);
        sbrt.anchorMax = new Vector2(1f, 1f);
        sbrt.pivot = new Vector2(1f, 0.5f);
        sbrt.anchoredPosition = Vector2.zero;
        sbrt.sizeDelta = new Vector2(20f, 0f);
        sbGO.GetComponent<Image>().color = new Color(0.10f, 0.11f, 0.16f, 0.9f);
        var sb = sbGO.GetComponent<Scrollbar>();
        sb.direction = Scrollbar.Direction.BottomToTop;
        var handleGO = new GameObject("Handle", typeof(Image));
        handleGO.transform.SetParent(sbGO.transform, false);
        var handleImg = handleGO.GetComponent<Image>();
        handleImg.color = new Color(0.38f, 0.42f, 0.55f, 0.95f);
        handleImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        handleImg.type = Image.Type.Sliced;
        var hdrt = handleGO.GetComponent<RectTransform>();
        hdrt.anchorMin = Vector2.zero;
        hdrt.anchorMax = Vector2.one;
        hdrt.offsetMin = new Vector2(3f, 3f);
        hdrt.offsetMax = new Vector2(-3f, -3f);
        sb.handleRect = hdrt;
        sb.targetGraphic = handleImg;

        var scroll = scrollGO.GetComponent<ScrollRect>();
        scroll.viewport = vrt;
        scroll.content = cort;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.verticalScrollbar = sb;
        scroll.scrollSensitivity = 30f;

        // 보관소 행 템플릿 ([타입 뱃지] 이름 — ★특별 금색 틴트, 드래그=장착)
        var rowGO = new GameObject("RowTemplate", typeof(Image));
        rowGO.transform.SetParent(contentGO.transform, false);
        var rowImg = rowGO.GetComponent<Image>();
        rowImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        rowImg.type = Image.Type.Sliced;
        rowGO.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 70f);
        var rowUI = rowGO.AddComponent<LobbyStorageRowUI>();

        Text badge = MakeText(rowGO.transform, "무기", 20);
        badge.color = new Color(0.75f, 0.78f, 0.88f);
        badge.alignment = TextAnchor.MiddleCenter;
        var bdrt = badge.rectTransform;
        bdrt.anchorMin = bdrt.anchorMax = new Vector2(0f, 0.5f);
        bdrt.pivot = new Vector2(0f, 0.5f);
        bdrt.anchoredPosition = new Vector2(12f, 0f);
        bdrt.sizeDelta = new Vector2(64f, 40f);
        rowUI.badge = badge;

        Text rowLabel = MakeText(rowGO.transform, "", 25);
        rowLabel.alignment = TextAnchor.MiddleLeft;
        var rlrt = rowLabel.rectTransform;
        rlrt.anchorMin = Vector2.zero;
        rlrt.anchorMax = Vector2.one;
        rlrt.offsetMin = new Vector2(88f, 0f);
        rlrt.offsetMax = new Vector2(-12f, 0f);
        rowUI.label = rowLabel;

        rowGO.SetActive(false);
        panel.storageListRoot = contentGO.transform;
        panel.storageRowTemplate = rowGO;

        // ---- 해고 버튼 ----
        Button dismiss = MakeButton(panelGO.transform, "DismissButton", "해고 (환급 없음)", new Color(0.55f, 0.25f, 0.28f), 26);
        var drt = dismiss.GetComponent<RectTransform>();
        drt.anchorMin = drt.anchorMax = new Vector2(1f, 0f);
        drt.pivot = new Vector2(1f, 0f);
        drt.anchoredPosition = new Vector2(-30f, 30f);
        drt.sizeDelta = new Vector2(320f, 90f);
        Wire(dismiss, panel, nameof(HeroManagePanel.OnClickDismiss));
        dismiss.interactable = false;
        panel.dismissButton = dismiss;

        panel.listRoot = listGO.transform;
        panel.entryTemplate = entry.gameObject;
        panelGO.SetActive(false);

        hud.heroManagePanel = panel;
        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(hud);
    }

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