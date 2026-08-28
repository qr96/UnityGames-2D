using System.Collections.Generic;

/// <summary>
/// 런 1회 동안의 월드 진행 상태 (구 MapRunState 병합 — 런 월드 상태의 단일 창구).
/// 개편된 규칙 (랜덤 맵):
///  - 양방향 이동: 지나간 장소로 되돌아갈 수 있음 (출구는 생성기가 양방향으로 연결)
///  - 재전투 없음: 전투 클리어한 장소는 재전투 없음 — 단, 특정 이벤트가 ReArmBattle()로 다시 걸 수 있음
///  - 정보 노출: 전투 클리어 시 인접 장소 정보 노출 (전투 없는 장소는 방문만으로 노출)
///    → UI는 IsRevealed()가 false인 장소를 "???"로 표기
///  - 보물: 장소당 1회 회수 (CanLoot / MarkLooted)
///  - 지도 기록: 첫 방문 순서(경로)를 기록 — 지도 팝업이 표시에 사용
/// 세계 구조 자체는 WorldDefinition에 고정.
/// </summary>
public class WorldState
{
    public readonly WorldDefinition world;
    public LocationDefinition Current { get; private set; }

    readonly HashSet<string> visited = new HashSet<string>();
    readonly List<LocationDefinition> visitedOrder = new List<LocationDefinition>();
    readonly HashSet<string> clearedBattles = new HashSet<string>();
    readonly HashSet<string> revealed = new HashSet<string>();       // 정보 노출된 장소
    readonly HashSet<string> rearmedBattles = new HashSet<string>(); // 이벤트로 재전투가 걸린 장소
    readonly HashSet<string> looted = new HashSet<string>();         // 보물 회수한 장소

    /// <summary>방문 경로 (첫 방문 순서대로) — 지도 표기용. 재방문은 중복 기록하지 않음.</summary>
    public IReadOnlyList<LocationDefinition> VisitedPath => visitedOrder;

    public WorldState(WorldDefinition world, LocationDefinition start)
    {
        this.world = world;
        Current = start;
        MarkVisited(start); // 시작 장소는 전투 없음 → 인접 정보까지 노출됨
    }

    // ---------------- 방문 ----------------

    public bool IsVisited(LocationDefinition loc) =>
        loc != null && visited.Contains(loc.id);

    void MarkVisited(LocationDefinition loc)
    {
        if (loc == null) return;
        if (visited.Add(loc.id))
            visitedOrder.Add(loc);

        Reveal(loc); // 밟은 장소는 당연히 노출

        // 전투 없는 장소(또는 이미 클리어한 장소)는 클리어 절차가 없으므로 방문만으로 인접 노출
        if (!loc.hasBattle || IsBattleCleared(loc))
            RevealNeighbors(loc);
    }

    // ---------------- 전투 ----------------

    public bool IsBattleCleared(LocationDefinition loc) =>
        loc != null && clearedBattles.Contains(loc.id);

    /// <summary>이 장소 도착 시 전투를 시작해야 하는가 — 클리어한 장소는 재전투 없음 (재장전 시 예외).</summary>
    public bool ShouldBattle(LocationDefinition loc)
    {
        if (loc == null || !loc.hasBattle) return false;
        if (rearmedBattles.Contains(loc.id)) return true;
        return !clearedBattles.Contains(loc.id);
    }

    /// <summary>전투 승리 시 호출 — 클리어 기록 + 인접 장소 정보 노출 (노드 규칙).</summary>
    public void MarkBattleCleared(LocationDefinition loc)
    {
        if (loc == null) return;
        clearedBattles.Add(loc.id);
        rearmedBattles.Remove(loc.id); // 재전투였다면 소화됨
        RevealNeighbors(loc);
    }

    /// <summary>특정 이벤트가 클리어된 장소에 다시 전투를 거는 경우 (이벤트 시스템에서 호출).</summary>
    public void ReArmBattle(LocationDefinition loc)
    {
        if (loc == null || !loc.hasBattle) return;
        rearmedBattles.Add(loc.id);
    }

    // ---------------- 정보 노출 ----------------

    /// <summary>UI 표기 판정 — false면 이름/미리보기 대신 "???" 등으로 가림.</summary>
    public bool IsRevealed(LocationDefinition loc) =>
        loc != null && revealed.Contains(loc.id);

    public void Reveal(LocationDefinition loc)
    {
        if (loc != null) revealed.Add(loc.id);
    }

    void RevealNeighbors(LocationDefinition loc)
    {
        foreach (var (_, next) in loc.Exits)
            Reveal(next);
    }

    // ---------------- 보물 ----------------

    public bool CanLoot(LocationDefinition loc) =>
        loc != null && loc.nodeType == NodeContent.Treasure && !looted.Contains(loc.id);

    public void MarkLooted(LocationDefinition loc)
    {
        if (loc != null) looted.Add(loc.id);
    }

    // ---------------- 이동 (양방향) ----------------

    /// <summary>해당 방향의 출구 (없으면 null). 방문 여부와 무관하게 이동 가능 — 재통과 허용.</summary>
    public LocationDefinition GetAvailableExit(Direction dir)
    {
        return Current != null ? Current.GetExit(dir) : null;
    }

    /// <summary>현재 위치에서 이동 가능한 (방향, 장소) 목록 — 모든 출구.</summary>
    public List<(Direction dir, LocationDefinition loc)> GetAvailableExits()
    {
        var list = new List<(Direction, LocationDefinition)>();
        if (Current == null) return list;
        foreach (var (dir, loc) in Current.Exits)
            list.Add((dir, loc));
        return list;
    }

    /// <summary>이동 — 현재 장소의 출구이거나, 계단의 descendTo(다음 층)여야 함.</summary>
    public bool MoveTo(LocationDefinition destination)
    {
        if (destination == null || Current == null) return false;

        bool isLinked = Current.descendTo == destination; // 계단 → 다음 층
        if (!isLinked)
        {
            foreach (var (_, loc) in Current.Exits)
                if (loc == destination) { isLinked = true; break; }
        }
        if (!isLinked) return false;

        Current = destination;
        MarkVisited(destination);
        return true;
    }
}