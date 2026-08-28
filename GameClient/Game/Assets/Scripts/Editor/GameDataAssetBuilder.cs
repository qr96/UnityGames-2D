using UnityEditor;
using UnityEngine;

/// <summary>
/// 에디터 메뉴 [Tools > GrabProto > 게임 데이터 에셋 생성]
/// 개발용 데이터(영웅/장비/런 설정/월드)를 Assets/GameData 아래 에셋 파일로 생성.
/// 로비 씬과 게임 씬이 같은 에셋을 참조하기 위한 승격 작업.
/// 씬에 DevBootstrap이 있으면 필드까지 자동 연결.
/// </summary>
public static class GameDataAssetBuilder
{
    const string Root = "Assets/GameData";

    [MenuItem("Tools/GrabProto/게임 데이터 에셋 생성")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<HeroDatabase>($"{Root}/Heroes/HeroDatabase.asset") != null)
        {
            EditorUtility.DisplayDialog("게임 데이터 에셋",
                "이미 Assets/GameData에 에셋이 있습니다.\n다시 만들려면 폴더를 삭제한 뒤 실행하세요.", "확인");
            return;
        }

        EnsureFolder(Root);
        EnsureFolder($"{Root}/Heroes");
        EnsureFolder($"{Root}/Equipment");
        EnsureFolder($"{Root}/World");

        // ---- 영웅 + 스킬 (액티브 스펙 v2: 스킬은 영웅이 아닌 풀에 소속 — 생성 시 랜덤 배정) ----
        EnsureFolder($"{Root}/Skills");
        HeroDatabase heroDb = DevGameData.CreateHeroDatabase();
        foreach (var sk in heroDb.skillPool)
            if (sk != null)
                AssetDatabase.CreateAsset(sk, $"{Root}/Skills/Skill_{sk.id}.asset");
        foreach (var hero in heroDb.heroes)
            AssetDatabase.CreateAsset(hero, $"{Root}/Heroes/Hero_{hero.id}.asset");
        AssetDatabase.CreateAsset(heroDb, $"{Root}/Heroes/HeroDatabase.asset");

        // ---- 장비 ----
        EquipmentDatabase equipDb = DevGameData.CreateEquipmentDatabase();
        foreach (var item in equipDb.items)
            AssetDatabase.CreateAsset(item, $"{Root}/Equipment/Equip_{item.id}.asset");
        AssetDatabase.CreateAsset(equipDb, $"{Root}/Equipment/EquipmentDatabase.asset");

        // ---- 런 설정 ----
        RunConfig config = DevGameData.CreateRunConfig();
        AssetDatabase.CreateAsset(config, $"{Root}/RunConfig.asset");

        // ---- 월드 ----
        // 장소들은 서로를 참조(방향 출구)하므로, ①모든 장소를 먼저 에셋으로 만들고
        // ②전부 에셋이 된 뒤 다시 Dirty 처리해 참조를 재직렬화해야 연결이 끊기지 않음.
        WorldDefinition world = DevWorldData.Create();
        foreach (var region in world.regions)
            foreach (var loc in region.locations)
                AssetDatabase.CreateAsset(loc, $"{Root}/World/Loc_{loc.id}.asset");

        foreach (var region in world.regions)
        {
            foreach (var loc in region.locations)
                EditorUtility.SetDirty(loc); // 이제 모든 연결 대상이 에셋 — 참조 재직렬화
            AssetDatabase.CreateAsset(region, $"{Root}/World/Region_{region.id}.asset");
        }
        AssetDatabase.CreateAsset(world, $"{Root}/World/World.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ---- 현재 씬의 부트스트랩에 자동 연결 ----
        var bootstrap = Object.FindFirstObjectByType<DevBootstrap>();
        if (bootstrap != null)
        {
            bootstrap.heroDatabase = heroDb;
            bootstrap.equipmentDatabase = equipDb;
            bootstrap.runConfig = config;
            // ※ worldDefinition은 연결하지 않음 — 연결하면 랜덤 맵(useRandomMap)이 무시됨.
            //    고정 맵으로 테스트하려면 인스펙터에서 World.asset을 직접 연결할 것.
            EditorUtility.SetDirty(bootstrap);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
            Debug.Log("[GameDataAssetBuilder] 부트스트랩 필드 자동 연결 완료 — worldDefinition은 랜덤 맵 사용을 위해 비워둠 (씬 저장 필요)");
        }

        Debug.Log("[GameDataAssetBuilder] Assets/GameData 생성 완료");
        EditorUtility.DisplayDialog("게임 데이터 에셋", "Assets/GameData 생성 완료.\n이후 밸런싱은 이 에셋들에서 진행하세요.", "확인");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}