using UnityEngine;

/// <summary>
/// 개발용 월드 데이터 — '바람 평원' 지역 하나 (장소 7개, 분기 있는 2D 그래프).
/// 실제 개발이 진행되면 에디터 에셋(Create > Game > World > ...)으로 옮기고 이 파일은 삭제.
///
/// 장소 간격은 실제 지형(약 11x15)이 겹치지 않도록 벌려둠.
/// 그래프 (대략적 배치):
///                [거인의 무덤]           ← 랜드마크
///                     |
///                [로엔 마을]             ← 정착지
///                /         \
///           [폐허]          [산길]
///               \            /
///              [숲] ------ [평원]
///                  \      /
///                  [야영지]              ← 시작 (임시)
/// </summary>
public static class DevWorldData
{
    public static WorldDefinition Create()
    {
        // ---- 장소 ----
        var camp = MakeLocation("wp_camp", "야영지", LocationType.Camp, new Vector2(0f, 0f), hasBattle: false);
        var forest = MakeLocation("wp_forest", "숲", LocationType.Field, new Vector2(-8f, 14f));
        var plain = MakeLocation("wp_plain", "평원", LocationType.Field, new Vector2(8f, 14f));
        var ruin = MakeLocation("wp_ruin", "폐허", LocationType.Exploration, new Vector2(-11f, 30f));
        var mountain = MakeLocation("wp_mountain", "산길", LocationType.Field, new Vector2(11f, 30f));
        var village = MakeLocation("wp_village", "로엔 마을", LocationType.Settlement, new Vector2(0f, 46f), hasBattle: false);
        var tomb = MakeLocation("wp_tomb", "거인의 무덤", LocationType.Landmark, new Vector2(0f, 64f));

        // ---- 길 (양방향 연결 — GDD 5) ----
        Connect(camp, forest);
        Connect(camp, plain);
        Connect(forest, plain);
        Connect(forest, ruin);
        Connect(plain, mountain);
        Connect(ruin, village);
        Connect(mountain, village);
        Connect(village, tomb);

        // ---- 지역 / 월드 ----
        var region = ScriptableObject.CreateInstance<RegionDefinition>();
        region.id = "windplain";
        region.regionName = "바람 평원";
        region.locations.AddRange(new[] { camp, forest, plain, ruin, mountain, village, tomb });

        var world = ScriptableObject.CreateInstance<WorldDefinition>();
        world.regions.Add(region);
        world.defaultStartLocation = camp; // 임시 (런 시작 위치 결정 방식 미확정)
        return world;
    }

    static LocationDefinition MakeLocation(string id, string name, LocationType type, Vector2 pos, bool hasBattle = true)
    {
        var loc = ScriptableObject.CreateInstance<LocationDefinition>();
        loc.id = id;
        loc.displayName = name;
        loc.type = type;
        loc.worldPosition = pos;
        loc.hasBattle = hasBattle;
        return loc;
    }

    static void Connect(LocationDefinition a, LocationDefinition b)
    {
        if (!a.connections.Contains(b)) a.connections.Add(b);
        if (!b.connections.Contains(a)) b.connections.Add(a);
    }
}