using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 던전 층 절차 생성기 (방 생성 명세 v1.0).
///
/// 핵심 원칙 (명세 25):
///   계단 확보 전 — "안전망을 위해 어디로 탐험할 것인가"
///   계단 확보 후 — "끝낼 것인가 / 내려갈 것인가 / 더 털 것인가"
///
/// 구조 (명세 1): 트리 중심 + 막다른 가지 적극 허용 + 조건부 재합류(비용이 다른 두 경로만).
/// 층은 하강 시점에 그 층만 생성 (명세 22) — GenerateWorld는 1층만 만들고,
/// 다음 층은 RunManager가 하강할 때 GenerateFloor로 이어 붙임.
///
/// 파이프라인 (명세 15): 방 수 결정 → 격자 위 랜덤 트리 성장 → 계단 배치(전투 거리 범위)
/// → 엘리트(계단 필수 경로 제외, 선택 가지) → 보물(경로 밖 막다른 가지, 전부는 금지)
/// → 조건부 재합류 → 품질 검증(명세 16) → 미달 시 폐기·재생성.
///
/// 격자 기반인 이유: 출구가 상/하/좌/우 4슬롯(LocationDefinition)이라 트리를 격자에
/// 심으면 방향/좌표/겹침이 공짜로 해결됨. 같은 시드 = 같은 층.
/// </summary>
public static class MapGenerator
{
    /// <summary>생성 파라미터 (명세 24 — 하드코딩 대신 데이터화)</summary>
    [System.Serializable]
    public class Config
    {
        [Header("던전 구조 (던전 명세: 1F~최심부, 보스/진입 포인트)")]
        [Tooltip("최심부 층 — 이 층에는 계단 대신 보스방 (프로토: 강화 엘리트 자리)")]
        public int maxFloor = 40;
        [Tooltip("외부 진입 포인트가 존재하는 마일스톤 층 (도달 즉시 영구 개방)")]
        public int[] entryPointFloors = { 11, 21, 31 };

        [Header("방 수 (명세 14: 초기 10~14)")]
        public int minRoomCount = 10;
        public int maxRoomCount = 14;

        [Header("계단 전투 거리 (명세 6.3: 평균 3~4, 허용 2~6)")]
        public int minStairCombatDistance = 2;
        public int targetStairCombatDistance = 3;
        public int maxStairCombatDistance = 6;

        [Header("분기 (명세 8: 시작 후 1~2 전투 내 첫 분기)")]
        public int maxLinearRoomsBeforeBranch = 2;
        public int minBranchCount = 1;

        [Header("재합류 (명세 10: 제한적 — 트리 거리가 충분히 다른 경로만)")]
        [Range(0f, 1f)] public float rejoinPatternChance = 0.35f;
        [Tooltip("재합류로 이을 두 방의 최소 트리 거리 (이보다 가까우면 의미 없는 루프)")]
        public int rejoinMinTreeDistance = 4;

        [Header("배치 (명세 11~12)")]
        public int eliteMinCount = 1;
        public int eliteMaxCount = 2;
        public int treasureMinCount = 1;
        public int treasureMaxCount = 2;

        [Header("생성 시도")]
        [Tooltip("품질 검증 실패 시 재생성 최대 횟수 (명세 15.13)")]
        public int maxGenerationAttempts = 30;

        [Header("격자/배치 간격")]
        public int gridWidth = 6;
        public int gridHeight = 6;
        public float cellSpacingX = 16f;
        public float cellSpacingY = 14f;
        public float floorOffsetX = 240f;
    }

    static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(-1, 0), new Vector2Int(1, 0),
    };

    // 표현은 항상 실제 장소로 (GDD 4)
    static readonly string[] NormalNames = { "숲길", "초원", "산길", "습지", "골짜기", "덤불 지대", "바위 언덕", "버려진 농가", "안개 낀 들판", "무너진 담장" };
    static readonly string[] NormalPreviews = { "수풀 사이에서 기척이 느껴진다", "발자국이 어지럽게 남아 있다", "낮게 으르렁거리는 소리", "공기가 무겁다", "무언가 지나간 흔적" };
    static readonly string[] EliteNames = { "짐승의 둥지", "무너진 제단", "검은 웅덩이", "뒤틀린 고목", "핏자국 동굴" };
    static readonly string[] ElitePreviews = { "강한 기운이 새어 나온다", "뼈 무더기가 쌓여 있다", "섬뜩한 정적이 흐른다" };
    static readonly string[] TreasureNames = { "낡은 석실", "묻힌 궤짝", "잊힌 창고", "허물어진 사당" };
    static readonly string[] TreasurePreviews = { "무언가 반짝인다", "손대지 않은 흔적", "먼지 쌓인 궤가 보인다" };

    // =================================================================
    //  공개 API
    // =================================================================

    /// <summary>시작 층 하나만 생성한 월드 반환 — 다음 층은 하강 시 GenerateFloor로 이어 붙임 (명세 22).
    /// startFloorNumber: 1 = 지상 입구, 그 외 = 개방된 외부 진입 포인트 층 (던전 명세).</summary>
    public static WorldDefinition GenerateWorld(int seed, Config cfg = null, int startFloorNumber = 1)
    {
        cfg ??= new Config();
        var world = ScriptableObject.CreateInstance<WorldDefinition>();
        var region = GenerateFloor(startFloorNumber - 1, seed, cfg, out var start, out _);
        world.regions.Add(region);
        world.defaultStartLocation = start;
        return world;
    }

    /// <summary>층 하나 생성 (품질 검증 포함 — 미달 시 재생성). start = 층 진입 방, stairs = 계단방.</summary>
    public static RegionDefinition GenerateFloor(int floorIndex, int seed, Config cfg,
        out LocationDefinition start, out LocationDefinition stairs)
    {
        cfg ??= new Config();

        int floorNumber = floorIndex + 1;
        bool isBossFloor = floorNumber >= cfg.maxFloor; // 최심부: 계단 없음, 보스방 (던전 명세)
        bool isEntryFloor = System.Array.IndexOf(cfg.entryPointFloors, floorNumber) >= 0;

        for (int attempt = 0; attempt < Mathf.Max(1, cfg.maxGenerationAttempts); attempt++)
        {
            var rng = new System.Random(unchecked(seed * 486187739 + floorIndex * 16777619 + attempt * 92821));
            var map = TryGenerate(rng, cfg, isBossFloor, isEntryFloor);
            if (map != null && Validate(map, cfg, isEntryFloor))
                return BuildRegion(map, floorIndex, cfg, rng, isBossFloor, out start, out stairs);
        }

        // 모든 시도 실패 — 마지막 시도라도 사용 (연결성은 트리라 항상 보장됨)
        Debug.LogWarning($"[MapGenerator] {cfg.maxGenerationAttempts}회 내 품질 기준 미달 — 마지막 생성물 사용 (floor {floorIndex})");
        var fallbackRng = new System.Random(unchecked(seed * 486187739 + floorIndex * 16777619 + 999983));
        MapDraft fallback = null;
        while (fallback == null) fallback = TryGenerate(fallbackRng, cfg, isBossFloor, isEntryFloor);
        return BuildRegion(fallback, floorIndex, cfg, fallbackRng, isBossFloor, out start, out stairs);
    }

    // =================================================================
    //  생성 초안
    // =================================================================

    class MapDraft
    {
        public HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        public HashSet<(Vector2Int, Vector2Int)> edges = new HashSet<(Vector2Int, Vector2Int)>();
        public Dictionary<Vector2Int, Vector2Int> parent = new Dictionary<Vector2Int, Vector2Int>(); // 트리 부모
        public Vector2Int start;
        public Vector2Int stairsCell;
        public HashSet<Vector2Int> stairPath = new HashSet<Vector2Int>(); // 시작→계단 트리 경로 (양 끝 포함)
        public Dictionary<Vector2Int, NodeContent> content = new Dictionary<Vector2Int, NodeContent>();
    }

    static MapDraft TryGenerate(System.Random rng, Config cfg, bool isBossFloor, bool isEntryFloor)
    {
        var m = new MapDraft();
        int targetRooms = rng.Next(cfg.minRoomCount, cfg.maxRoomCount + 1);

        // ---- 격자 위 랜덤 트리 성장 (프림 방식: 기존 방 중 하나에서 빈 이웃으로 확장) ----
        m.start = new Vector2Int(rng.Next(cfg.gridWidth), 0);
        m.cells.Add(m.start);
        var growable = new List<Vector2Int> { m.start };

        int guard = 0;
        while (m.cells.Count < targetRooms && guard++ < 500)
        {
            if (growable.Count == 0) return null; // 성장 불가 — 재시도
            var from = growable[rng.Next(growable.Count)];

            var free = new List<Vector2Int>();
            foreach (var d in Dirs)
            {
                var n = from + d;
                if (n.x >= 0 && n.x < cfg.gridWidth && n.y >= 0 && n.y < cfg.gridHeight && !m.cells.Contains(n))
                    free.Add(n);
            }
            if (free.Count == 0) { growable.Remove(from); continue; }

            var next = free[rng.Next(free.Count)];
            m.cells.Add(next);
            m.edges.Add(EdgeKey(from, next));
            m.parent[next] = from;
            growable.Add(next);
        }
        if (m.cells.Count < cfg.minRoomCount) return null;

        // ---- 전원 일반 전투로 초기화 (시작 제외) ----
        foreach (var c in m.cells)
            m.content[c] = c == m.start ? NodeContent.None : NodeContent.NormalBattle;

        // ---- 계단 배치: 전투 거리(경로상 전투 수 = 트리 깊이-1)가 허용 범위인 후보 (명세 6) ----
        var depth = TreeDepths(m);
        var stairCandidates = m.cells
            .Where(c => c != m.start)
            .Where(c => InRange(depth[c] - 1, cfg.minStairCombatDistance, cfg.maxStairCombatDistance))
            .ToList();
        if (stairCandidates.Count == 0) return null;

        // 목표 구간(target~target+1, 즉 3~4)은 동순위 선호 → 분포가 한 값에 몰리지 않음.
        // 구간 밖(2 또는 5~6)은 낮은 확률로만 선택됨 (명세 6.3: 빠르면 2, 늦으면 5~6)
        var children = ChildCounts(m);
        int Score(Vector2Int c)
        {
            int d = depth[c] - 1;
            int band = (d >= cfg.targetStairCombatDistance && d <= cfg.targetStairCombatDistance + 1) ? 0
                : Mathf.Min(Mathf.Abs(d - cfg.targetStairCombatDistance),
                            Mathf.Abs(d - (cfg.targetStairCombatDistance + 1)));
            return band * 2 + (children[c] == 0 ? 0 : 1);
        }
        int best = stairCandidates.Min(Score);
        var bestCells = stairCandidates.Where(c => Score(c) == best).ToList();
        m.stairsCell = bestCells[rng.Next(bestCells.Count)];
        // 최심부: 계단 대신 보스방 — 전투 있음, 거리 규칙은 계단과 동일하게 적용됨
        m.content[m.stairsCell] = isBossFloor ? NodeContent.EliteBattle : NodeContent.Stairs;

        // 시작→계단 트리 경로 기록 (엘리트/보물 배치 제외 구역 — 명세 11)
        for (var c = m.stairsCell; ; c = m.parent[c])
        {
            m.stairPath.Add(c);
            if (c == m.start) break;
        }

        // ---- 외부 진입 포인트 (마일스톤 층): 경로 밖 배치 — 탐색해서 직접 발견 (던전 명세) ----
        var offPath = m.cells.Where(c => !m.stairPath.Contains(c)).ToList();
        Shuffle(offPath, rng);
        if (isEntryFloor)
        {
            var entryLeaves = offPath.Where(c => ChildCounts(m)[c] == 0 && m.content[c] == NodeContent.NormalBattle).ToList();
            var entryPick = entryLeaves.Count > 0 ? entryLeaves[rng.Next(entryLeaves.Count)]
                : offPath.FirstOrDefault(c => m.content[c] == NodeContent.NormalBattle);
            if (entryPick == default && offPath.Count == 0) return null; // 배치 불가 — 재시도
            m.content[entryPick] = NodeContent.EntryPoint;
        }

        // ---- 엘리트: 계단 필수 경로 밖 선택 가지에만 (명세 11) ----
        var eliteCandidates = offPath.Where(c => m.content[c] == NodeContent.NormalBattle).ToList();
        int eliteCount = Mathf.Min(rng.Next(cfg.eliteMinCount, cfg.eliteMaxCount + 1), eliteCandidates.Count);
        for (int i = 0; i < eliteCount; i++)
            m.content[eliteCandidates[i]] = NodeContent.EliteBattle;

        // ---- 보물: 경로 밖 막다른 가지 우선 — 단, 그런 잎 전부를 보물로 만들지 않음 (명세 12) ----
        var leavesOffPath = offPath
            .Where(c => children[c] == 0 && m.content[c] == NodeContent.NormalBattle)
            .ToList();
        Shuffle(leavesOffPath, rng);
        int treasureCap = leavesOffPath.Count > 1 ? leavesOffPath.Count - 1 : leavesOffPath.Count; // "막다른 길=보물" 공식 방지
        int treasureCount = Mathf.Min(rng.Next(cfg.treasureMinCount, cfg.treasureMaxCount + 1), treasureCap);
        for (int i = 0; i < treasureCount; i++)
            m.content[leavesOffPath[i]] = NodeContent.Treasure;

        // ---- 조건부 재합류: 트리 거리가 충분히 다른 인접 방만 잇기 (명세 10) ----
        if (rng.NextDouble() < cfg.rejoinPatternChance)
        {
            var rejoinCandidates = new List<(Vector2Int, Vector2Int)>();
            foreach (var c in m.cells)
                foreach (var d in new[] { Dirs[0], Dirs[3] }) // 중복 없이 절반 방향만
                {
                    var n = c + d;
                    if (!m.cells.Contains(n) || m.edges.Contains(EdgeKey(c, n))) continue;
                    if (TreeDistance(m, c, n) >= cfg.rejoinMinTreeDistance)
                        rejoinCandidates.Add(EdgeKey(c, n));
                }
            if (rejoinCandidates.Count > 0)
                m.edges.Add(rejoinCandidates[rng.Next(rejoinCandidates.Count)]);
        }

        return m;
    }

    // =================================================================
    //  품질 검증 (명세 16)
    // =================================================================

    static bool Validate(MapDraft m, Config cfg, bool isEntryFloor)
    {
        var children = ChildCounts(m);
        var depth = TreeDepths(m);

        // 계단 전투 거리: 최종 그래프(재합류 포함) 기준 최소 전투 경로로 재검사
        int battles = MinBattlesToStairs(m);
        if (!InRange(battles, cfg.minStairCombatDistance, cfg.maxStairCombatDistance)) return false;

        // 계단까지 엘리트 강제 통과 금지 — 트리 경로에는 배치상 없음. 재합류로 생긴
        // 대체 경로는 추가 선택지일 뿐이므로 트리 경로 무결성만 보장하면 됨.
        // (보스 층은 목적지 자체가 강화 엘리트라 목적지 칸은 검사 제외)
        foreach (var c in m.stairPath)
            if (c != m.stairsCell && m.content[c] == NodeContent.EliteBattle) return false;

        // 마일스톤 층: 진입 포인트가 반드시 존재해야 함
        if (isEntryFloor && !m.cells.Any(c => m.content[c] == NodeContent.EntryPoint)) return false;

        // 첫 의미 있는 분기: 전투 maxLinear회 내 (명세 8.1) — 시작 포함 얕은 분기 노드 존재
        bool earlyBranch = m.cells.Any(c =>
            children[c] >= 2 && (depth[c] == 0 ? 0 : depth[c] - 0) <= cfg.maxLinearRoomsBeforeBranch + 0
            && depth[c] <= cfg.maxLinearRoomsBeforeBranch);
        if (!earlyBranch) return false;

        // 사실상 선형 금지: 분기 노드 수 (명세 16)
        int branchCount = m.cells.Count(c => children[c] >= 2);
        if (branchCount < cfg.minBranchCount) return false;

        // 계단 확보 후 남은 콘텐츠: 경로 밖 엘리트/보물 최소 1개 (명세 7)
        bool remaining = m.cells.Any(c =>
            !m.stairPath.Contains(c) &&
            (m.content[c] == NodeContent.EliteBattle || m.content[c] == NodeContent.Treasure));
        if (!remaining) return false;

        return true;
    }

    /// <summary>시작→계단 최소 전투 횟수 (0/1 가중 BFS — 재합류 경로 포함 최종 그래프 기준)</summary>
    static int MinBattlesToStairs(MapDraft m)
    {
        var adj = BuildAdjacency(m);
        var cost = new Dictionary<Vector2Int, int> { [m.start] = 0 };
        var dq = new LinkedList<Vector2Int>();
        dq.AddFirst(m.start);
        while (dq.Count > 0)
        {
            var c = dq.First.Value; dq.RemoveFirst();
            foreach (var n in adj[c])
            {
                bool isBattle = n != m.stairsCell && // 목적지(계단/보스방) 자체는 '도달 비용'에서 제외
                    (m.content[n] == NodeContent.NormalBattle || m.content[n] == NodeContent.EliteBattle);
                int w = isBattle ? 1 : 0;
                int nc = cost[c] + w;
                if (cost.TryGetValue(n, out int old) && old <= nc) continue;
                cost[n] = nc;
                if (w == 0) dq.AddFirst(n); else dq.AddLast(n);
            }
        }
        return cost.TryGetValue(m.stairsCell, out int r) ? r : int.MaxValue;
    }

    // =================================================================
    //  Region 조립
    // =================================================================

    static RegionDefinition BuildRegion(MapDraft m, int floorIndex, Config cfg, System.Random rng,
        bool isBossFloor, out LocationDefinition start, out LocationDefinition stairs)
    {
        var locs = new Dictionary<Vector2Int, LocationDefinition>();
        foreach (var c in m.cells)
            locs[c] = MakeLocation(floorIndex, c, m.content[c], c == m.start && floorIndex == 0, cfg, rng);

        // 최심부 보스방 마감 (프로토: 강화 엘리트 자리 — Landmark 승리 = 런 클리어 규칙에 걸림)
        if (isBossFloor)
        {
            var boss = locs[m.stairsCell];
            boss.displayName = "최심부";
            boss.type = LocationType.Landmark; // BattleController: Landmark 배수 + 엘리트 배수 중첩 = 강화 엘리트
            boss.previewText = "깊은 어둠 속에서 무언가 도사리고 있다";
        }

        foreach (var (a, b) in m.edges)
            Connect(locs[a], locs[b], a, b); // 양방향 — 확보한 경로 재통행 (명세 3.1)

        var region = ScriptableObject.CreateInstance<RegionDefinition>();
        region.id = $"floor_{floorIndex}";
        region.regionName = $"지하 {floorIndex + 1}층";
        region.locations.AddRange(m.cells.OrderBy(c => c.y).ThenBy(c => c.x).Select(c => locs[c]));

        start = locs[m.start];
        stairs = locs[m.stairsCell];
        return region;
    }

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
            case NodeContent.None:
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
                loc.canEvent = true;      // 특수 기믹 재전투는 이벤트 시스템에서 (명세 3.2)
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

            case NodeContent.EntryPoint:
                loc.displayName = "봉인된 외부 통로";
                loc.type = LocationType.Landmark;
                loc.previewText = "바깥으로 이어지는 낡은 통로 — 열 수 있을 것 같다";
                loc.hasBattle = false;
                loc.fixedFunction = LocationFunction.EntryPoint;
                break;

            case NodeContent.Stairs:
                loc.displayName = "내려가는 계단";
                loc.type = LocationType.Landmark;
                loc.previewText = "어둠 속으로 계단이 이어진다";
                loc.hasBattle = false;
                loc.fixedFunction = LocationFunction.Stairs;
                break;
        }
        return loc;
    }

    // =================================================================
    //  그래프 도구
    // =================================================================

    static void Connect(LocationDefinition la, LocationDefinition lb, Vector2Int a, Vector2Int b)
    {
        var d = b - a;
        if (d == Dirs[0]) { la.north = lb; lb.south = la; }
        else if (d == Dirs[1]) { la.south = lb; lb.north = la; }
        else if (d == Dirs[2]) { la.west = lb; lb.east = la; }
        else if (d == Dirs[3]) { la.east = lb; lb.west = la; }
    }

    static Dictionary<Vector2Int, List<Vector2Int>> BuildAdjacency(MapDraft m)
    {
        var adj = m.cells.ToDictionary(c => c, _ => new List<Vector2Int>());
        foreach (var (a, b) in m.edges) { adj[a].Add(b); adj[b].Add(a); }
        return adj;
    }

    /// <summary>트리 깊이 (시작 = 0) — 부모 체인 기준</summary>
    static Dictionary<Vector2Int, int> TreeDepths(MapDraft m)
    {
        var depth = new Dictionary<Vector2Int, int> { [m.start] = 0 };
        foreach (var c in m.cells)
        {
            if (depth.ContainsKey(c)) continue;
            var chain = new List<Vector2Int>();
            var cur = c;
            while (!depth.ContainsKey(cur)) { chain.Add(cur); cur = m.parent[cur]; }
            int d = depth[cur];
            for (int i = chain.Count - 1; i >= 0; i--) depth[chain[i]] = ++d;
        }
        return depth;
    }

    /// <summary>트리 기준 자식 수 (잎 판정용)</summary>
    static Dictionary<Vector2Int, int> ChildCounts(MapDraft m)
    {
        var counts = m.cells.ToDictionary(c => c, _ => 0);
        foreach (var kv in m.parent) counts[kv.Value]++;
        return counts;
    }

    /// <summary>두 방의 트리 경로 거리 (재합류 후보 판정용)</summary>
    static int TreeDistance(MapDraft m, Vector2Int a, Vector2Int b)
    {
        var pathA = new List<Vector2Int>();
        for (var c = a; ; c = m.parent[c]) { pathA.Add(c); if (c == m.start) break; }
        var indexA = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < pathA.Count; i++) indexA[pathA[i]] = i;

        int up = 0;
        for (var c = b; ; c = m.parent[c], up++)
        {
            if (indexA.TryGetValue(c, out int i)) return i + up;
            if (c == m.start) break;
        }
        return int.MaxValue;
    }

    static (Vector2Int, Vector2Int) EdgeKey(Vector2Int a, Vector2Int b)
    {
        bool swap = a.y > b.y || (a.y == b.y && a.x > b.x);
        return swap ? (b, a) : (a, b);
    }

    static bool InRange(int v, int min, int max) => v >= min && v <= max;
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