using System.Collections.Generic;

/// <summary>
/// 런 1회 동안의 월드 진행 상태.
/// 개편된 규칙:
///  - 일방통행: 이미 방문한 장소로는 이동 불가 (방향 선택지에서 제외)
///  - 지도 기록: 방문 순서(경로)를 기록 — 지도 팝업이 표시에 사용
/// 세계 구조 자체는 WorldDefinition에 고정.
/// </summary>
public class WorldState
{
    public readonly WorldDefinition world;
    public LocationDefinition Current { get; private set; }

    readonly HashSet<string> visited = new HashSet<string>();
    readonly List<LocationDefinition> visitedOrder = new List<LocationDefinition>();
    readonly HashSet<string> clearedBattles = new HashSet<string>();

    /// <summary>방문 경로 (순서대로) — 지도 표기용</summary>
    public IReadOnlyList<LocationDefinition> VisitedPath => visitedOrder;

    public WorldState(WorldDefinition world, LocationDefinition start)
    {
        this.world = world;
        Current = start;
        MarkVisited(start);
    }

    public bool IsVisited(LocationDefinition loc) =>
        loc != null && visited.Contains(loc.id);

    public bool IsBattleCleared(LocationDefinition loc) =>
        loc != null && clearedBattles.Contains(loc.id);

    public void MarkBattleCleared(LocationDefinition loc)
    {
        if (loc != null) clearedBattles.Add(loc.id);
    }

    /// <summary>해당 방향의 이동 가능한 출구 (없거나 이미 방문한 장소면 null — 일방통행)</summary>
    public LocationDefinition GetAvailableExit(Direction dir)
    {
        LocationDefinition exit = Current != null ? Current.GetExit(dir) : null;
        return (exit != null && !IsVisited(exit)) ? exit : null;
    }

    /// <summary>현재 위치에서 이동 가능한 (방향, 장소) 목록</summary>
    public List<(Direction dir, LocationDefinition loc)> GetAvailableExits()
    {
        var list = new List<(Direction, LocationDefinition)>();
        if (Current == null) return list;
        foreach (var (dir, loc) in Current.Exits)
            if (!IsVisited(loc)) list.Add((dir, loc));
        return list;
    }

    /// <summary>이동 — 현재 장소의 미방문 출구로만 가능</summary>
    public bool MoveTo(LocationDefinition destination)
    {
        if (destination == null || Current == null) return false;
        if (IsVisited(destination)) return false; // 일방통행

        bool isExit = false;
        foreach (var (_, loc) in Current.Exits)
            if (loc == destination) { isExit = true; break; }
        if (!isExit) return false;

        Current = destination;
        MarkVisited(destination);
        return true;
    }

    void MarkVisited(LocationDefinition loc)
    {
        if (loc == null) return;
        if (visited.Add(loc.id))
            visitedOrder.Add(loc);
    }
}