using UnityEngine;

/// <summary>
/// 개발용 월드 데이터 — '바람 평원' 지역, 14개 장소 (일방통행, 모든 경로가 14로 수렴).
/// 방향: 도식의 ↑=상, ↖=좌, ↗=우. 방향 충돌 없음 검증 완료.
///
///                         [14 숲 입구]            ← 랜드마크 (런 클리어)
///                        ↗(12)    ↖(13)
///                [12 낮은 언덕]    [13 오래된 길]
///                  ↑(10) ↖(11)      ↑(11)
///             [10 풍차 언덕]      [11 작은 돌다리]
///               ↗(8)   ↖(9)        ↗(9)
///          [8 밀밭]       [9 개울가]
///            ↑(6) ↖(7)      ↑(7)
///      [6 작은 촌락] ──→── [7 목초지]
///            ↑(4)            ↑(5)
///         [4 농로]        [5 들길]
///             ↖(3)          ↗(3)
///                [3 작은 예배당]                   ← 휴식 장소 (생존자 회복)
///                      ↑(2)
///                [2 남쪽 초원]
///                      ↑(1)
///                [1 시작 마을]                     ← 시작 (전투 없음)
/// </summary>
public static class DevWorldData
{
    public static WorldDefinition Create()
    {
        // ---- 장소 (번호 = 기획 도식 번호) ----
        // Make(..., 전투, 상인, 사건, 전용사건, 발견, 고정기능) — 콘텐츠 표 그대로
        var l01 = Make("wp01_village", "시작 마을", LocationType.Settlement, new Vector2(0f, 0f), "여정이 시작되는 마을 — 문이 열려 있다",
            battle: false, merchant: false, evt: false, dedicated: false, discovery: false, func: LocationFunction.RunStart);
        var l02 = Make("wp02_meadow", "남쪽 초원", LocationType.Field, new Vector2(0f, 14f), "잔풀이 흔들린다 — 무언가 숨어 있는 듯하다",
            battle: true, merchant: true, evt: true, dedicated: false, discovery: true);
        var l03 = Make("wp03_chapel", "작은 예배당", LocationType.Camp, new Vector2(0f, 28f), "고요한 예배당 — 쉬어 가기 좋아 보인다",
            battle: false, merchant: false, evt: false, dedicated: true, discovery: false, func: LocationFunction.Rest);
        var l04 = Make("wp04_farmroad", "농로", LocationType.Field, new Vector2(-16f, 42f), "수레바퀴 자국이 이어진 길",
            battle: true, merchant: true, evt: true, dedicated: false, discovery: true);
        var l05 = Make("wp05_path", "들길", LocationType.Field, new Vector2(16f, 42f), "풀숲 사이로 좁은 길이 나 있다",
            battle: true, merchant: false, evt: true, dedicated: false, discovery: true);
        var l06 = Make("wp06_hamlet", "작은 촌락", LocationType.Settlement, new Vector2(-16f, 56f), "인적은 드물지만 안전해 보인다",
            battle: false, merchant: false, evt: false, dedicated: true, discovery: false, func: LocationFunction.Shop);
        var l07 = Make("wp07_pasture", "목초지", LocationType.Field, new Vector2(16f, 56f), "풀 뜯던 흔적만 남아 있다",
            battle: true, merchant: true, evt: true, dedicated: false, discovery: true);
        var l08 = Make("wp08_wheat", "밀밭", LocationType.Field, new Vector2(-16f, 70f), "밀이 어지럽게 쓰러져 있다",
            battle: true, merchant: false, evt: true, dedicated: false, discovery: true);
        var l09 = Make("wp09_creek", "개울가", LocationType.Field, new Vector2(16f, 70f), "물소리 너머로 낯선 기척",
            battle: true, merchant: true, evt: true, dedicated: false, discovery: true);
        var l10 = Make("wp10_windmill", "풍차 언덕", LocationType.Field, new Vector2(0f, 84f), "풍차 날개가 삐걱거린다",
            battle: true, merchant: false, evt: true, dedicated: true, discovery: true); // 전용 + 일반 사건
        var l11 = Make("wp11_bridge", "작은 돌다리", LocationType.Field, new Vector2(32f, 84f), "다리 밑이 수상하다",
            battle: true, merchant: false, evt: true, dedicated: false, discovery: true);
        var l12 = Make("wp12_hill", "낮은 언덕", LocationType.Field, new Vector2(0f, 98f), "언덕 너머가 보이지 않는다",
            battle: true, merchant: false, evt: true, dedicated: false, discovery: true);
        var l13 = Make("wp13_oldroad", "오래된 길", LocationType.Field, new Vector2(32f, 98f), "이끼 낀 표지판이 서 있다",
            battle: true, merchant: true, evt: true, dedicated: false, discovery: true);
        var l14 = Make("wp14_gate", "숲 입구", LocationType.Landmark, new Vector2(16f, 112f), "어두운 숲 — 거대한 기운이 새어 나온다",
            battle: true, merchant: false, evt: true, dedicated: false, discovery: true); // 랜드마크 = 런 클리어 유지

        // ---- 출구 (일방통행, 기획 연결 18개 그대로) ----
        l01.north = l02;                    // 1 → 2
        l02.north = l03;                    // 2 → 3
        l03.west = l04; l03.east = l05;   // 3 → 4, 3 → 5
        l04.north = l06;                    // 4 → 6
        l05.north = l07;                    // 5 → 7
        l06.east = l07; l06.north = l08;   // 6 → 7, 6 → 8
        l07.west = l08; l07.north = l09;   // 7 → 8, 7 → 9
        l08.east = l10;                    // 8 → 10
        l09.west = l10; l09.east = l11;   // 9 → 10, 9 → 11
        l10.north = l12;                    // 10 → 12
        l11.west = l12; l11.north = l13;   // 11 → 12, 11 → 13
        l12.east = l14;                    // 12 → 14
        l13.west = l14;                    // 13 → 14

        var region = ScriptableObject.CreateInstance<RegionDefinition>();
        region.id = "windplain";
        region.regionName = "바람 평원";
        region.locations.AddRange(new[] { l01, l02, l03, l04, l05, l06, l07, l08, l09, l10, l11, l12, l13, l14 });

        var world = ScriptableObject.CreateInstance<WorldDefinition>();
        world.regions.Add(region);
        world.defaultStartLocation = l01;
        return world;
    }

    static LocationDefinition Make(string id, string name, LocationType type, Vector2 pos, string preview,
        bool battle, bool merchant, bool evt, bool dedicated, bool discovery,
        LocationFunction func = LocationFunction.None)
    {
        var loc = ScriptableObject.CreateInstance<LocationDefinition>();
        loc.id = id;
        loc.displayName = name;
        loc.type = type;
        loc.worldPosition = pos;
        loc.previewText = preview;
        loc.hasBattle = battle;
        loc.canMerchant = merchant;
        loc.canEvent = evt;
        loc.hasDedicatedEvent = dedicated;
        loc.canDiscovery = discovery;
        loc.fixedFunction = func;
        return loc;
    }
}