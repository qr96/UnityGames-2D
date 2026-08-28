using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 랜덤 맵 생성기 — 층(Region) 단위로 노드 그래프를 절차 생성.
///
/// 규칙 반영:
///   · 노드 종류: 일반 전투 / 엘리트 전투 / 보물 / 계단 (특수방은 배치 슬롯만 지원, 시스템 추후)
///   · 계단: 층마다 시작에서 가장 먼 노드에 1개 배치, descendTo = 다음 층 시작 노드
///     (도착 시 [귀환/내려가기] 선택 처리는 TravelController/RunManager 담당)
///   · 이동: 모든 출구를 양방향으로 연결 — 지나간 노드 재통과 가능
///   · 정보 노출/클리어/재전투는 데이터가 아닌 런타임 상태 (MapRunState 참고)
///
/// 알고리즘:
///   1) 격자 위 취보(drunkard walk)로 연결된 셀 집합 선택 → 자연스럽게 스패닝 트리 간선 확보
///   2) 인접하지만 연결 안 된 셀 쌍 중 일부에 순환 간선 추가 (우회로/재방문 동선)
///   3) BFS 거리 기반 배치: 계단 = 최원거리, 엘리트 = 먼 구간, 보물 = 막다른 길 우선
///   4) 나머지는 전부 일반 전투. 1층 시작 노드만 전투 없음 (RunStart)
///
/// 방향 슬롯이 상/하/좌/우 4개라 격자 기반이 기존 LocationDefinition과 그대로 호환됨.
/// 같은 시드 = 같은 맵 (시드 저장 시 맵 재현 가능).
/// </summary>
public static class MapGenerator
{
    [System.Serializable]
    public class Config
    {
        [Header("격자")]
        public int gridWidth = 5;
        public int gridHeight = 6;
        [Range(0.3f, 1f), Tooltip("격자 중 실제 노드로 쓸 비율")]
        public float fillRatio = 0.6f;

        [Header("연결")]
        [Tooltip("스패닝 트리 외에 추가할 순환 간선 수 (우회로)")]
        public int extraLoopEdges = 2;

        [Header("노드 배치 (층당)")]
        public int eliteCount = 2;
        public int treasureCount = 2;
        [Tooltip("특수방 수 — 시스템 미구현이라 기본 0 (배치만 지원)")]
        public int specialCount = 0;

        [Header("배치 간격 (worldPosition)")]
        public float cellSpacingX = 16f;
        public float cellSpacingY = 14f;
        [Tooltip("층별 worldPosition 오프셋 (겹침 방지)")]
        public float floorOffsetX = 240f;
    }

    static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(0, 1),  // North
        new Vector2Int(0, -1), // South
        new Vector2Int(-1, 0), // West
        new Vector2Int(1, 0),  // East
    };

    // ---- 이름/미리보기 풀 (표현은 항상 실제 장소로 — GDD 4) ----
    static readonly string[] NormalNames = { "숲길", "초원", "산길", "습지", "골짜기", "덤불 지대", "바위 언덕", "버려진 농가", "안개 낀 들판", "무너진 담장" };
    static readonly string[] NormalPreviews = { "수풀 사이에서 기척이 느껴진다", "발자국이 어지럽게 남아 있다", "낮게 으르렁거리는 소리", "공기가 무겁다", "무언가 지나간 흔적" };
    static readonly string[] EliteNames = { "짐승의 둥지", "무너진 제단", "검은 웅덩이", "뒤틀린 고목", "핏자국 동굴" };
    static readonly string[] ElitePreviews = { "강한 기운이 새어 나온다", "뼈 무더기가 쌓여 있다", "섬뜩한 정적이 흐른다" };
    static readonly string[] TreasureNames = { "낡은 석실", "묻힌 궤짝", "잊힌 창고", "허물어진 사당" };
    static readonly string[] TreasurePreviews = { "무언가 반짝인다", "손대지 않은 흔적", "먼지 쌓인 궤가 보인다" };
    static readonly string[] SpecialNames = { "기묘한 방", "이상한 문", "빛나는 균열" };

    // =================================================================
    //  공개 API
    // =================================================================

    /// <summary>층 floorCount개를 한 번에 생성하고 계단(descendTo)으로 연결한 월드 반환.</summary>
    public static WorldDefinition GenerateWorld(int seed, int floorCount, Config cfg = null)
    {
        cfg ??= new Config();
        var world = ScriptableObject.CreateInstance<WorldDefinition>();

        LocationDefinition prevStairs = null;
        for (int f = 0; f < floorCount; f++)
        {
            var region = GenerateFloor(f, seed, cfg, out var start, out var stairs);
            world.regions.Add(region);

            if (f == 0) world.defaultStartLocation = start;
            if (prevStairs != null) prevStairs.descendTo = start; // 이전 층 계단 → 이번 층 시작
            prevStairs = stairs; // 마지막 층 계단은 descendTo = null
        }
        return world;
    }

    /// <summary>층 하나 생성. start = 층 진입 노드, stairs = 내려가는 계단 노드.</summary>
    public static RegionDefinition GenerateFloor(int floorIndex, int seed, Config cfg,
        out LocationDefinition start, out LocationDefinition stairs)
    {
        var rng = new System.Random(unchecked(seed * 486187739 + floorIndex * 16777619));

        // ---- 1) 취보로 셀 선택 + 트리 간선 ----
        int targetCells = Mathf.Clamp(
            Mathf.RoundToInt(cfg.gridWidth * cfg.gridHeight * cfg.fillRatio),
            6, cfg.gridWidth * cfg.gridHeight);

        var startCell = new Vector2Int(rng.Next(cfg.gridWidth), 0); // 아래쪽 행에서 시작
        var cells = new HashSet<Vector2Int> { startCell };
        var edges = new HashSet<(Vector2Int, Vector2Int)>();

        var cur = startCell;
        int guard = 0;
        while (cells.Count < targetCells && guard++ < 20000)
        {
            var next = cur + Dirs[rng.Next(4)];
            if (next.x < 0 || next.x >= cfg.gridWidth || next.y < 0 || next.y >= cfg.gridHeight)
                continue;
            if (cells.Add(next))
                edges.Add(EdgeKey(cur, next)); // 처음 방문한 셀 = 트리 간선 (연결 보장)
            cur = next;
        }

        // ---- 2) 순환 간선 추가 (양방향 이동이라 우회/재방문 동선이 됨) ----
        var loopCandidates = new List<(Vector2Int, Vector2Int)>();
        foreach (var c in cells)
        {
            var e = c + Dirs[3]; // East
            var n = c + Dirs[0]; // North (중복 없이 절반 방향만 검사)
            if (cells.Contains(e) && !edges.Contains(EdgeKey(c, e))) loopCandidates.Add(EdgeKey(c, e));
            if (cells.Contains(n) && !edges.Contains(EdgeKey(c, n))) loopCandidates.Add(EdgeKey(c, n));
        }
        Shuffle(loopCandidates, rng);
        for (int i = 0; i < Mathf.Min(cfg.extraLoopEdges, loopCandidates.Count); i++)
            edges.Add(loopCandidates[i]);

        // ---- 3) BFS 거리 계산 ----
        var adj = BuildAdjacency(cells, edges);
        var dist = Bfs(startCell, adj);
        var stairsCell = cells.OrderByDescending(c => dist[c]).First();

        // ---- 4) 노드 종류 배정 ----
        var content = new Dictionary<Vector2Int, NodeContent>();
        foreach (var c in cells) content[c] = NodeContent.NormalBattle;
        content[startCell] = NodeContent.None;
        content[stairsCell] = NodeContent.Stairs;

        var free = cells.Where(c => content[c] == NodeContent.NormalBattle).ToList();

        // 보물: 막다른 길(간선 1개) 우선 — 없으면 아무 곳
        var deadEnds = free.Where(c => adj[c].Count == 1).ToList();
        Shuffle(deadEnds, rng); Shuffle(free, rng);
        AssignFrom(content, NodeContent.Treasure, cfg.treasureCount, deadEnds, free);

        // 엘리트: 시작에서 먼 절반 우선
        free = cells.Where(c => content[c] == NodeContent.NormalBattle).ToList();
        int farLine = dist[stairsCell] / 2;
        var farCells = free.Where(c => dist[c] >= farLine).ToList();
        Shuffle(farCells, rng); Shuffle(free, rng);
        AssignFrom(content, NodeContent.EliteBattle, cfg.eliteCount, farCells, free);

        // 특수방 (시스템 추후 — cfg 기본 0)
        free = cells.Where(c => content[c] == NodeContent.NormalBattle).ToList();
        Shuffle(free, rng);
        AssignFrom(content, NodeContent.Special, cfg.specialCount, free, null);

        // ---- 5) LocationDefinition 생성 ----
        var locs = new Dictionary<Vector2Int, LocationDefinition>();
        foreach (var c in cells)
            locs[c] = MakeLocation(floorIndex, c, content[c], c == startCell && floorIndex == 0, cfg, rng);

        // ---- 6) 출구 연결 (항상 양방향 — 재통과 가능) ----
        foreach (var (a, b) in edges)
            Connect(locs[a], locs[b], a, b);

        var region = ScriptableObject.CreateInstance<RegionDefinition>();
        region.id = $"floor_{floorIndex}";
        region.regionName = $"지하 {floorIndex + 1}층";
        region.locations.AddRange(cells.OrderBy(c => c.y).ThenBy(c => c.x).Select(c => locs[c]));

        start = locs[startCell];
        stairs = locs[stairsCell];
        return region;
    }

    // =================================================================
    //  내부
    // =================================================================

    static LocationDefinition MakeLocation(int floor, Vector2Int cell, NodeContent node, bool isRunStart,
        Config cfg, System.Random rng)
    {
        var loc = ScriptableObject.CreateInstance<LocationDefinition>();
        loc.id = $"f{floor}_x{cell.x}y{cell.y}";
        loc.nodeType = node;
        loc.worldPosition = new Vector2(
            cell.x * cfg.cellSpacingX + floor * cfg.floorOffsetX,
            cell.y * cfg.cellSpacingY);

        switch (node)
        {
            case NodeContent.None: // 층 시작 노드
                loc.displayName = floor == 0 ? "던전 입구" : $"지하 {floor + 1}층 입구";
                loc.type = LocationType.Field;
                loc.previewText = "잠시 숨을 고를 수 있을 것 같다";
                loc.hasBattle = false;
                loc.fixedFunction = isRunStart ? LocationFunction.RunStart : LocationFunction.None;
                break;

            case NodeContent.NormalBattle:
                loc.displayName = Pick(NormalNames, rng);
                loc.type = LocationType.Field;
                loc.previewText = Pick(NormalPreviews, rng);
                loc.hasBattle = true;
                loc.canEvent = true;      // 특정 이벤트로 재전투 발생 가능 (판정은 MapRunState)
                loc.canDiscovery = true;
                break;

            case NodeContent.EliteBattle:
                loc.displayName = Pick(EliteNames, rng);
                loc.type = LocationType.Exploration;
                loc.previewText = Pick(ElitePreviews, rng);
                loc.hasBattle = true;
                loc.canEvent = true;
                break;

            case NodeContent.Treasure:
                loc.displayName = Pick(TreasureNames, rng);
                loc.type = LocationType.Exploration;
                loc.previewText = Pick(TreasurePreviews, rng);
                loc.hasBattle = false;
                loc.canDiscovery = true;
                break;

            case NodeContent.Stairs:
                loc.displayName = "내려가는 계단";
                loc.type = LocationType.Landmark;
                loc.previewText = "어둠 속으로 계단이 이어진다 — 귀환하거나 더 내려갈 수 있다";
                loc.hasBattle = false;
                loc.fixedFunction = LocationFunction.Stairs;
                break;

            case NodeContent.Special:
                loc.displayName = Pick(SpecialNames, rng);
                loc.type = LocationType.Exploration;
                loc.previewText = "설명하기 어려운 무언가가 있다";
                loc.hasBattle = false;
                loc.hasDedicatedEvent = true; // 시스템 추후
                break;
        }
        return loc;
    }

    /// <summary>a↔b 양방향 출구 설정 (격자 인접 전제).</summary>
    static void Connect(LocationDefinition la, LocationDefinition lb, Vector2Int a, Vector2Int b)
    {
        var d = b - a;
        if (d == Dirs[0]) { la.north = lb; lb.south = la; }
        else if (d == Dirs[1]) { la.south = lb; lb.north = la; }
        else if (d == Dirs[2]) { la.west = lb; lb.east = la; }
        else if (d == Dirs[3]) { la.east = lb; lb.west = la; }
    }

    static void AssignFrom(Dictionary<Vector2Int, NodeContent> content, NodeContent value, int count,
        List<Vector2Int> preferred, List<Vector2Int> fallback)
    {
        int assigned = 0;
        foreach (var c in preferred)
        {
            if (assigned >= count) return;
            if (content[c] == NodeContent.NormalBattle) { content[c] = value; assigned++; }
        }
        if (fallback == null) return;
        foreach (var c in fallback)
        {
            if (assigned >= count) return;
            if (content[c] == NodeContent.NormalBattle) { content[c] = value; assigned++; }
        }
    }

    static Dictionary<Vector2Int, List<Vector2Int>> BuildAdjacency(
        HashSet<Vector2Int> cells, HashSet<(Vector2Int, Vector2Int)> edges)
    {
        var adj = cells.ToDictionary(c => c, _ => new List<Vector2Int>());
        foreach (var (a, b) in edges) { adj[a].Add(b); adj[b].Add(a); }
        return adj;
    }

    static Dictionary<Vector2Int, int> Bfs(Vector2Int start, Dictionary<Vector2Int, List<Vector2Int>> adj)
    {
        var dist = new Dictionary<Vector2Int, int> { [start] = 0 };
        var q = new Queue<Vector2Int>();
        q.Enqueue(start);
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var n in adj[c])
                if (!dist.ContainsKey(n)) { dist[n] = dist[c] + 1; q.Enqueue(n); }
        }
        return dist;
    }

    static (Vector2Int, Vector2Int) EdgeKey(Vector2Int a, Vector2Int b)
    {
        // 정렬된 키로 저장해 (a,b)/(b,a) 중복 방지
        bool swap = a.y > b.y || (a.y == b.y && a.x > b.x);
        return swap ? (b, a) : (a, b);
    }

    static string Pick(string[] pool, System.Random rng) => pool[rng.Next(pool.Length)];

    static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}