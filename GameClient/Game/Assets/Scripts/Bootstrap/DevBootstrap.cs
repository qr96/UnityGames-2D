using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 개발용 부트스트랩 — 이제 게임 로직 초기화만 담당 (UI는 씬에서 제작, GameHUD가 연결).
///   생성: 카메라 설정, GrabController, BattleController, RunManager + 개발용 데이터
///   시작: 해금 영웅 중 앞의 3명으로 런 시작
///
/// 데이터(영웅/장비/설정)는 코드에서 생성하지만 전부 ScriptableObject라서
/// 에디터 에셋(Create > Game > ...)으로 옮기고 인스펙터 필드로 교체하면 이 파일은 사라져도 됨.
/// </summary>
public class DevBootstrap : MonoBehaviour
{
    [Header("씬 연결")]
    public GameHUD hud;

    RunManager runManager;
    BattleController battleController;

    void Awake()
    {
        SetupCamera();
        new GameObject("GrabController").AddComponent<GrabController>();
        new GameObject("UnitSeparation").AddComponent<UnitSeparation>(); // 유닛 겹침 방지

        battleController = new GameObject("BattleController").AddComponent<BattleController>();

        runManager = new GameObject("RunManager").AddComponent<RunManager>();
        runManager.config = ScriptableObject.CreateInstance<RunConfig>(); // 기본값 사용

        // ★ 영입 시스템 보류 중 (방식 미확정) — 확정되면 이 두 줄을 지우고 재활성화
        runManager.config.recruitChances = 0;
        runManager.config.recruitAfterBattle = new int[0];

        runManager.heroDatabase = CreateDevHeroDatabase();
        runManager.equipmentPool = CreateDevEquipmentPool();
        runManager.battleController = battleController;

        // 전투 중 소모품 바 연결 (씬 UI)
        if (hud != null && hud.consumableBar != null)
            battleController.consumableBar = hud.consumableBar;
        else
            Debug.LogWarning("[DevBootstrap] hud(또는 hud.consumableBar)가 연결되지 않았습니다 — 포션 바가 동작하지 않습니다.");
    }

    void Start()
    {
        runManager.Profile.EnsureDefaults(runManager.heroDatabase);
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

    // ================= 개발용 데이터 =================

    HeroDatabase CreateDevHeroDatabase()
    {
        var db = ScriptableObject.CreateInstance<HeroDatabase>();
        db.heroes.Add(MakeHero("knight", "기사", new Color(0.35f, 0.55f, 1f), 0.95f, true,
            hp: 140f, atk: 12f, range: 1.1f, interval: 0.9f, speed: 1.8f));
        db.heroes.Add(MakeHero("archer", "궁수", new Color(1f, 0.8f, 0.25f), 0.80f, true,
            hp: 90f, atk: 16f, range: 3.5f, interval: 1.2f, speed: 2.0f,
            projectile: true));
        db.heroes.Add(MakeHero("healer", "사제", new Color(0.45f, 1f, 0.55f), 0.80f, true,
            hp: 80f, atk: 6f, range: 2.5f, interval: 1.1f, speed: 1.7f,
            healer: true, healPower: 14f, healRange: 3.2f));
        db.heroes.Add(MakeHero("rogue", "도적", new Color(0.6f, 0.6f, 0.7f), 0.75f, false,
            hp: 75f, atk: 20f, range: 1.0f, interval: 0.6f, speed: 2.4f));
        db.heroes.Add(MakeHero("mage", "마법사", new Color(0.75f, 0.45f, 1f), 0.80f, false,
            hp: 70f, atk: 26f, range: 4.2f, interval: 1.8f, speed: 1.6f,
            projectile: true));
        db.heroes.Add(MakeHero("paladin", "성기사", new Color(1f, 0.95f, 0.7f), 1.00f, false,
            hp: 180f, atk: 10f, range: 1.2f, interval: 1.1f, speed: 1.5f));
        return db;
    }

    HeroDefinition MakeHero(string id, string name, Color color, float size, bool unlockedByDefault,
        float hp, float atk, float range, float interval, float speed,
        bool healer = false, float healPower = 0f, float healRange = 0f,
        bool projectile = false)
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
        d.usesProjectile = projectile;
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
}