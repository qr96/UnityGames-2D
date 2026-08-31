using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임(런) 씬 부트스트랩 — 게임 로직 초기화만 담당 (UI는 씬에서 제작, GameHUD가 연결).
///   생성: 카메라, GrabController, UnitSeparation, BattleController, TravelController,
///         RunManager, 월드 표현(지형/라벨/카메라)
///   시작: 해금 영웅 중 앞의 3명으로 런 시작 (로비 출정 연동은 ④단계에서 교체 예정)
///
/// 데이터: 인스펙터에 에셋(Assets/GameData)을 연결하면 에셋 사용,
///         비워두면 DevGameData/DevWorldData로 런타임 생성 (폴백).
///         에셋 생성은 [Tools > GrabProto > 게임 데이터 에셋 생성].
/// </summary>
public class DevBootstrap : MonoBehaviour
{
    [Header("씬 연결")]
    public GameHUD hud;

    [Header("데이터 에셋 (권장) — 비워두면 개발용 데이터를 런타임 생성")]
    public HeroDatabase heroDatabase;
    public EquipmentDatabase equipmentDatabase;
    public RunConfig runConfig;
    public WorldDefinition worldDefinition;

    [Header("랜덤 맵 — worldDefinition이 비어 있을 때 사용")]
    [Tooltip("끄면 기존 고정 맵(DevWorldData)으로 폴백")]
    public bool useRandomMap = true;
    [Tooltip("0 = 매 실행 랜덤. 그 외 = 시드 고정 (같은 시드 = 같은 맵, 버그 재현용)")]
    public int mapSeed = 0;
    [Tooltip("생성 파라미터 (방 생성 명세 v1.0 §24) — 층은 하강 시 그때 생성됨")]
    public MapGenerator.Config mapConfig = new MapGenerator.Config();

    RunManager runManager;
    BattleController battleController;

    void Awake()
    {
        SetupCamera();
        new GameObject("GrabController").AddComponent<GrabController>();
        new GameObject("UnitSeparation").AddComponent<UnitSeparation>(); // 유닛 겹침 방지

        battleController = new GameObject("BattleController").AddComponent<BattleController>();

        runManager = new GameObject("RunManager").AddComponent<RunManager>();

        // 설정: 에셋이 있으면 런타임 사본 사용 (플레이 중 변경이 에셋 원본을 더럽히지 않도록)
        RunConfig config = runConfig != null ? Instantiate(runConfig) : DevGameData.CreateRunConfig();
        // ★ 영입 시스템 보류 중 (방식 미확정) — 확정되면 이 두 줄을 지우고 재활성화
        config.recruitChances = 0;
        config.recruitAfterBattle = new int[0];
        runManager.config = config;

        runManager.heroDatabase = heroDatabase != null ? heroDatabase : DevGameData.CreateHeroDatabase();

        // 액티브 배정 풀 (액티브 스펙 v2) — HeroRoster가 영웅을 생성하기 전에 설정해야 함
        var skillPool = runManager.heroDatabase.skillPool;
        HeroRoster.SetSkillPool(skillPool != null && skillPool.Count > 0 ? skillPool : DevGameData.CreateSkillPool());

        EquipmentDatabase equips = equipmentDatabase != null ? equipmentDatabase : DevGameData.CreateEquipmentDatabase();
        runManager.equipmentPool = new List<EquipmentDefinition>(equips.items);
        // 월드: 에셋 > 랜덤 생성 > 고정 개발 맵 순
        if (worldDefinition != null)
        {
            runManager.world = worldDefinition;
        }
        else if (useRandomMap)
        {
            int seed = mapSeed != 0 ? mapSeed : Random.Range(1, int.MaxValue);
            // 출발 층: 로비 출정이 설정한 값 (1층 또는 개방된 진입 포인트 — 던전 명세)
            int startFloor = Mathf.Clamp(SortieData.startFloor, 1, mapConfig.maxFloor);
            Debug.Log($"[DevBootstrap] 랜덤 맵 생성 — seed={seed}, 출발 지하 {startFloor}층 (이후 층은 하강 시 생성)");
            runManager.world = MapGenerator.GenerateWorld(seed, mapConfig, startFloor);
            runManager.mapSeed = seed;
            runManager.mapConfig = mapConfig; // 하강 시 다음 층 생성용 (명세 22)
            runManager.startFloorNumber = startFloor;
        }
        else
        {
            runManager.world = DevWorldData.Create();
        }
        runManager.battleController = battleController;
        runManager.travelController = new GameObject("TravelController").AddComponent<TravelController>();

        // 탐험 표현 (지형 레이어 + 카메라 연출 — 방향 선택/지도는 씬 UI가 담당)
        new GameObject("WorldEnvironment").AddComponent<WorldEnvironment>();
        new GameObject("CameraController").AddComponent<CameraController>();

        // 전투 중 소모품 바 연결 (씬 UI)
        if (hud != null && hud.consumableBar != null)
            battleController.consumableBar = hud.consumableBar;
        else
            Debug.LogWarning("[DevBootstrap] hud(또는 hud.consumableBar)가 연결되지 않았습니다 — 포션 바가 동작하지 않습니다.");
    }

    void Start()
    {
        runManager.Profile.EnsureDefaults(runManager.heroDatabase);

        // 영입 스펙 v1: 메타 상태 준비 (로스터/골드/영입 후보 — 저장 시스템 전 인메모리)
        HeroRoster.EnsureStarters(runManager.heroDatabase);
        GoldWallet.EnsureDevGold();
        RecruitShop.EnsureCandidates(runManager.heroDatabase);

        // 로비 출정으로 진입했으면 선택된 파티로, 아니면(게임 씬 직접 실행) 로스터 앞 5명으로
        if (SortieData.HasSelection)
        {
            var starters = SortieData.ResolveOwned(); // heroId 우선, 정의 id 폴백
            SortieData.Clear();
            if (starters.Count > 0)
            {
                runManager.StartRun(starters);
                return;
            }
        }
        runManager.StartDefaultRun();
    }

    // ================= 카메라 =================

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
}