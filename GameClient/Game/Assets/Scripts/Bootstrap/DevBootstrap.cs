using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 개발용 부트스트랩: 빈 씬에 이 컴포넌트 하나로 런 전체 루프를 데모.
///   전투 → 장비 드랍 → 영입(후보 2 중 1 선택) → 정비(장비 장착) → 다음 전투 → 클리어/해금
///
/// 아직 없는 것 (다음 단계에서 실제 UI로 교체):
///   - 로비 영웅 선택 UI  → 지금은 해금 영웅 중 앞의 3명 자동 선발
///   - 수동 장비 장착 UI  → 지금은 '장비 자동 분배(임시)' 버튼
///
/// 데이터(영웅/장비/설정)는 코드에서 생성하지만 전부 ScriptableObject라서,
/// 에디터에서 에셋(Create > Game > ...)으로 만들어 인스펙터로 연결하면 이 파일은 얇아짐.
/// </summary>
public class DevBootstrap : MonoBehaviour
{
    RunManager runManager;
    BattleController battleController;

    Font font;
    Text phaseLabel;
    GameObject recruitPanel;
    readonly Button[] recruitButtons = new Button[2];
    readonly Text[] recruitLabels = new Text[2];
    GameObject prepPanel;
    Text prepSummary;
    GameObject resultPanel;
    Text resultText;
    GameObject startButton;

    void Awake()
    {
        SetupCamera();
        new GameObject("GrabController").AddComponent<GrabController>();

        battleController = new GameObject("BattleController").AddComponent<BattleController>();

        runManager = new GameObject("RunManager").AddComponent<RunManager>();
        runManager.config = ScriptableObject.CreateInstance<RunConfig>(); // 기본값 사용

        // ★ 영입 시스템 보류 중 (방식 미확정) — 확정되면 이 두 줄을 지우고 재활성화
        runManager.config.recruitChances = 0;
        runManager.config.recruitAfterBattle = new int[0];
        runManager.heroDatabase = CreateDevHeroDatabase();
        runManager.equipmentPool = CreateDevEquipmentPool();
        runManager.battleController = battleController;

        CreateUI();
        runManager.OnPhaseChanged += OnPhaseChanged;
    }

    void Start()
    {
        runManager.Profile.EnsureDefaults(runManager.heroDatabase);
        StartNewRun();
    }

    void StartNewRun()
    {
        // 임시 로비: 해금된 영웅 중 앞의 3명 자동 선발 (GDD 6의 '3명 선택' UI는 다음 단계)
        var unlocked = runManager.Profile.GetUnlockedHeroes(runManager.heroDatabase);
        var starters = unlocked.GetRange(0, Mathf.Min(RunState.StartPartySize, unlocked.Count));
        runManager.StartRun(starters);
    }

    // ================= 페이즈 → UI =================

    void OnPhaseChanged(RunPhase phase)
    {
        recruitPanel.SetActive(phase == RunPhase.Recruit);
        prepPanel.SetActive(phase == RunPhase.Prep);
        resultPanel.SetActive(phase == RunPhase.RunClear || phase == RunPhase.RunFailed);
        startButton.SetActive(phase == RunPhase.Placement);

        RunState run = runManager.Run;
        switch (phase)
        {
            case RunPhase.Placement:
                phaseLabel.text = $"배치 {run.battleNumber}/{runManager.config.battlesPerRun} — 영웅을 원하는 위치로 옮기세요";
                break;

            case RunPhase.Battle:
                phaseLabel.text = $"전투 {run.battleNumber}/{runManager.config.battlesPerRun}   파티 {run.party.Count}명";
                break;

            case RunPhase.Recruit:
                phaseLabel.text = "동료 영입 — 한 명을 선택하세요";
                RefreshRecruit();
                break;

            case RunPhase.Prep:
                phaseLabel.text = "정비 — 장비 장착 후 다음 전투";
                RefreshPrep();
                break;

            case RunPhase.RunClear:
                phaseLabel.text = "런 클리어!";
                resultText.text = BuildClearText();
                break;

            case RunPhase.RunFailed:
                phaseLabel.text = "런 실패";
                resultText.text = "파티가 전멸했습니다.\n이번 런의 파티와 장비는 소멸합니다.";
                break;
        }
    }

    void RefreshRecruit()
    {
        List<HeroDefinition> cands = runManager.CurrentCandidates;
        for (int i = 0; i < recruitButtons.Length; i++)
        {
            bool has = i < cands.Count;
            recruitButtons[i].gameObject.SetActive(has);
            if (!has) continue;

            HeroDefinition def = cands[i];
            bool unlocked = runManager.Profile.IsUnlocked(def);
            string role = def.isHealer ? "힐러" : (def.attackRange >= 2.5f ? "원거리" : "근접");
            recruitLabels[i].text = unlocked
                ? $"{def.displayName}  ({role})"
                : $"{def.displayName}  ({role})\n★ 미해금 — 죽이지 않고 클리어 시 영구 해금";
        }
    }

    void RefreshPrep()
    {
        RunState run = runManager.Run;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"보유 장비 {run.inventory.Count}개 · 남은 영입 기회 {run.recruitChancesLeft}회");
        sb.AppendLine();
        foreach (HeroRunInstance h in run.party)
        {
            sb.Append($"{h.definition.displayName}  [{h.equipment.Count}/{HeroRunInstance.MaxEquipSlots}]");
            foreach (var e in h.equipment) sb.Append($"  {e.displayName}");
            sb.AppendLine();
        }
        prepSummary.text = sb.ToString();
    }

    string BuildClearText()
    {
        var names = runManager.LastUnlockedHeroNames;
        string unlockLine = names.Count > 0
            ? "영구 해금: " + string.Join(", ", names)
            : "새로 해금된 영웅은 없습니다.";
        return unlockLine + "\n이번 런의 장비는 소멸합니다.";
    }

    // ================= 버튼 동작 =================

    void AutoEquipAll()
    {
        RunState run = runManager.Run;
        bool progress = true;
        while (run.inventory.Count > 0 && progress)
        {
            progress = false;
            foreach (HeroRunInstance h in run.party)
            {
                if (run.inventory.Count == 0) break;
                if (h.HasFreeSlot && run.Equip(h, run.inventory[0]))
                    progress = true;
            }
        }
        RefreshPrep();
    }

    // ================= 개발용 데이터 =================

    HeroDatabase CreateDevHeroDatabase()
    {
        var db = ScriptableObject.CreateInstance<HeroDatabase>();
        db.heroes.Add(MakeHero("knight",  "기사",   new Color(0.35f, 0.55f, 1f),   0.95f, true,
            hp: 140f, atk: 12f, range: 1.1f, interval: 0.9f, speed: 2.6f));
        db.heroes.Add(MakeHero("archer",  "궁수",   new Color(1f, 0.8f, 0.25f),    0.80f, true,
            hp: 90f,  atk: 16f, range: 3.5f, interval: 1.2f, speed: 2.8f));
        db.heroes.Add(MakeHero("healer",  "사제",   new Color(0.45f, 1f, 0.55f),   0.80f, true,
            hp: 80f,  atk: 6f,  range: 2.5f, interval: 1.1f, speed: 2.4f,
            healer: true, healPower: 14f, healRange: 3.2f));
        db.heroes.Add(MakeHero("rogue",   "도적",   new Color(0.6f, 0.6f, 0.7f),   0.75f, false,
            hp: 75f,  atk: 20f, range: 1.0f, interval: 0.6f, speed: 3.4f));
        db.heroes.Add(MakeHero("mage",    "마법사", new Color(0.75f, 0.45f, 1f),   0.80f, false,
            hp: 70f,  atk: 26f, range: 4.2f, interval: 1.8f, speed: 2.2f));
        db.heroes.Add(MakeHero("paladin", "성기사", new Color(1f, 0.95f, 0.7f),    1.00f, false,
            hp: 180f, atk: 10f, range: 1.2f, interval: 1.1f, speed: 2.2f));
        return db;
    }

    HeroDefinition MakeHero(string id, string name, Color color, float size, bool unlockedByDefault,
        float hp, float atk, float range, float interval, float speed,
        bool healer = false, float healPower = 0f, float healRange = 0f)
    {
        var d = ScriptableObject.CreateInstance<HeroDefinition>();
        d.id = id;
        d.displayName = name;
        d.color = color;
        d.size = size;
        d.unlockedByDefault = unlockedByDefault;
        d.maxHP = hp;
        d.attack = atk;
        d.attackRange = range;
        d.attackInterval = interval;
        d.moveSpeed = speed;
        d.isHealer = healer;
        d.healPower = healPower;
        d.healRange = healRange;
        return d;
    }

    List<EquipmentDefinition> CreateDevEquipmentPool()
    {
        return new List<EquipmentDefinition>
        {
            MakeEquip("sword",  "낡은 검",      Mod(StatType.Attack, flat: 5f)),
            MakeEquip("armor",  "사슬 갑옷",    Mod(StatType.MaxHP, flat: 40f)),
            MakeEquip("boots",  "바람의 신발",  Mod(StatType.MoveSpeed, pct: 20f)),
            MakeEquip("lens",   "저격 렌즈",    Mod(StatType.AttackRange, flat: 0.6f)),
            MakeEquip("ring",   "축복의 반지",  Mod(StatType.HealPower, flat: 8f)),
        };
    }

    EquipmentDefinition MakeEquip(string id, string name, params StatModifier[] mods)
    {
        var e = ScriptableObject.CreateInstance<EquipmentDefinition>();
        e.id = id;
        e.displayName = name;
        e.modifiers = mods;
        return e;
    }

    static StatModifier Mod(StatType stat, float flat = 0f, float pct = 0f) =>
        new StatModifier { stat = stat, flat = flat, percent = pct };

    // ================= 카메라 / UI 구성 =================

    void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            cam = go.GetComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 8f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.17f);
    }

    void CreateUI()
    {
        font = LoadDefaultFont();

        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        if (EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem", typeof(EventSystem));
            var module = esGO.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        Transform root = canvasGO.transform;

        // 상단 페이즈 라벨
        phaseLabel = MakeText(root, "", 44);
        var plrt = phaseLabel.GetComponent<RectTransform>();
        plrt.anchorMin = plrt.anchorMax = new Vector2(0.5f, 1f);
        plrt.anchoredPosition = new Vector2(0f, -80f);
        plrt.sizeDelta = new Vector2(1000f, 100f);

        // 포션 버튼 (우하단)
        var potionGO = new GameObject("PotionButton", typeof(Image));
        potionGO.transform.SetParent(root, false);
        var pImg = potionGO.GetComponent<Image>();
        pImg.sprite = UnitFactory.Circle;
        pImg.color = new Color(1f, 0.35f, 0.55f);
        var prt = potionGO.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(1f, 0f);
        prt.pivot = new Vector2(1f, 0f);
        prt.anchoredPosition = new Vector2(-50f, 50f);
        prt.sizeDelta = new Vector2(150f, 150f);
        var potion = potionGO.AddComponent<PotionButton>();
        var pCount = MakeText(potionGO.transform, "x3", 42);
        StretchToParent(pCount.GetComponent<RectTransform>());
        potion.countText = pCount;
        battleController.potionButton = potion;

        // 전투 시작 버튼 (배치 단계에만 표시, 하단 중앙)
        Button start = MakeButton(root, "전투 시작 ▶", Vector2.zero, new Vector2(520f, 120f),
            () => runManager.BeginCombat(), out _);
        var srt = start.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0f);
        srt.anchoredPosition = new Vector2(0f, 140f);
        startButton = start.gameObject;
        startButton.SetActive(false);

        // 영입 패널
        recruitPanel = MakePanel(root, "RecruitPanel", new Vector2(760f, 620f));
        var rTitle = MakeText(recruitPanel.transform, "새로운 동료가 합류를 원합니다", 40);
        PlaceCentered(rTitle.GetComponent<RectTransform>(), new Vector2(0f, 240f), new Vector2(700f, 90f));
        for (int i = 0; i < 2; i++)
        {
            int index = i; // 클로저 캡처
            recruitButtons[i] = MakeButton(recruitPanel.transform, "",
                new Vector2(0f, 70f - i * 200f), new Vector2(640f, 170f),
                () => runManager.ChooseRecruit(index), out recruitLabels[i]);
        }

        // 정비 패널
        prepPanel = MakePanel(root, "PrepPanel", new Vector2(760f, 760f));
        var pTitle = MakeText(prepPanel.transform, "정비 단계", 40);
        PlaceCentered(pTitle.GetComponent<RectTransform>(), new Vector2(0f, 320f), new Vector2(700f, 80f));
        prepSummary = MakeText(prepPanel.transform, "", 30);
        prepSummary.alignment = TextAnchor.UpperLeft;
        PlaceCentered(prepSummary.GetComponent<RectTransform>(), new Vector2(0f, 70f), new Vector2(660f, 380f));
        MakeButton(prepPanel.transform, "장비 자동 분배 (임시)",
            new Vector2(0f, -200f), new Vector2(600f, 100f), AutoEquipAll, out _);
        MakeButton(prepPanel.transform, "다음 전투 ▶",
            new Vector2(0f, -320f), new Vector2(600f, 110f),
            () => runManager.ContinueToNextBattle(), out _);

        // 결과 패널 (클리어/실패 공용)
        resultPanel = MakePanel(root, "ResultPanel", new Vector2(760f, 520f));
        resultText = MakeText(resultPanel.transform, "", 36);
        PlaceCentered(resultText.GetComponent<RectTransform>(), new Vector2(0f, 70f), new Vector2(660f, 280f));
        MakeButton(resultPanel.transform, "새 런 시작",
            new Vector2(0f, -160f), new Vector2(520f, 110f),
            () => { resultPanel.SetActive(false); StartNewRun(); }, out _);
    }

    // ---------- UI 헬퍼 ----------

    GameObject MakePanel(Transform parent, string name, Vector2 size)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.14f, 0.94f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        go.SetActive(false);
        return go;
    }

    Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size,
        UnityEngine.Events.UnityAction onClick, out Text labelText)
    {
        var go = new GameObject("Button_" + label, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.25f, 0.32f, 0.5f);
        PlaceCentered(go.GetComponent<RectTransform>(), pos, size);
        go.GetComponent<Button>().onClick.AddListener(onClick);

        labelText = MakeText(go.transform, label, 34);
        StretchToParent(labelText.GetComponent<RectTransform>());
        return go.GetComponent<Button>();
    }

    Text MakeText(Transform parent, string content, int fontSize)
    {
        var go = new GameObject("Text", typeof(Text));
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

    static void PlaceCentered(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    Font LoadDefaultFont()
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
