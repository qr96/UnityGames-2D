using System.Collections.Generic;

/// <summary>
/// 런 1회 동안의 월드 탐험 상태.
/// GDD 6: 현재 위치에서 인접 장소만 공개, 방문한 장소는 기록, 재방문 가능.
/// (세계 구조 자체는 WorldDefinition에 고정 — 여기는 '이번 여행'의 상태만)
/// ※ 방문 기록의 영구 저장(세계를 기억) 여부는 미확정 — 확정되면 PlayerProfile 연동.
/// ※ 전투 클리어 기록: '클리어한 장소 재전투 없음'은 임시 결정 (RunConfig로 토글).
/// </summary>
public class WorldState
{
    public readonly WorldDefinition world;
    public LocationDefinition Current { get; private set; }

    readonly HashSet<string> visited = new HashSet<string>();
    readonly HashSet<string> clearedBattles = new HashSet<string>();

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

    /// <summary>현재 위치에서 이동 가능한(공개된) 인접 장소들 (깨진 참조는 제외)</summary>
    public List<LocationDefinition> GetReachable()
    {
        var list = new List<LocationDefinition>();
        if (Current == null) return list;
        foreach (var loc in Current.connections)
            if (loc != null) list.Add(loc);
        return list;
    }

    /// <summary>길은 인접 장소만 연결 (GDD 5) — 인접해야만 이동 가능</summary>
    public bool CanMoveTo(LocationDefinition destination) =>
        Current != null && Current.IsConnectedTo(destination);

    public bool MoveTo(LocationDefinition destination)
    {
        if (!CanMoveTo(destination)) return false;
        Current = destination;
        MarkVisited(destination);
        return true;
    }

    void MarkVisited(LocationDefinition loc)
    {
        if (loc != null) visited.Add(loc.id);
    }
}