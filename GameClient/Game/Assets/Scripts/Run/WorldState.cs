using System.Collections.Generic;

/// <summary>
/// 런 1회 동안의 월드 진행 상태 (방 생성 명세 v1.0).
///  - 존재 은닉 (명세 2): 미공개(Hidden) 방은 존재/연결/종류가 모두 숨겨지며 이동도 불가.
///    방 클리어(전투방) 또는 방문(비전투방) 시 인접 Hidden 방이 Revealed로 전환.
///    RoomState 대응 — Hidden: !IsRevealed / Revealed: IsRevealed / Cleared: IsBattleCleared·방문
///  - 계단 확보 (명세 5·18): 계단방 도달 시 Secured — 이후 위치와 무관하게 귀환/하강 가능
///    (백트래킹 없음 — 클리어된 안전 경로로 돌아간 것으로 추상화)
///  - 재전투 없음 (명세 3): 클리어한 방은 유지. 특수 기믹만 ReArmBattle()로 예외 가능
///  - 보물: 방당 1회 회수 (CanLoot / MarkLooted)
///  - 양방향 이동: 공개된 방 사이는 자유 재통행
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

    /// <summary>현재 층의 계단 확보 여부 (명세 18: Secured) — 확보 후 어디서든 귀환/하강 가능</summary>
    public bool StairsSecured { get; private set; }

    /// <summary>확보한 계단방 (하강 목적지 결정용)</summary>
    public LocationDefinition SecuredStairs { get; private set; }

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

        // 계단 확보 (명세 5.2): 도달 = Secured — 이후 층 어디서든 귀환/하강 가능
        if (loc.fixedFunction == LocationFunction.Stairs)
        {
            StairsSecured = true;
            SecuredStairs = loc;
        }

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

    /// <summary>해당 방향의 출구 — 공개(Revealed)된 방만 (명세 2: Hidden 방은 존재 자체가 숨겨져 이동 불가).</summary>
    public LocationDefinition GetAvailableExit(Direction dir)
    {
        LocationDefinition exit = Current != null ? Current.GetExit(dir) : null;
        return (exit != null && IsRevealed(exit)) ? exit : null;
    }

    /// <summary>현재 위치에서 이동 가능한 (방향, 장소) 목록 — 공개된 출구만.</summary>
    public List<(Direction dir, LocationDefinition loc)> GetAvailableExits()
    {
        var list = new List<(Direction, LocationDefinition)>();
        if (Current == null) return list;
        foreach (var (dir, loc) in Current.Exits)
            if (IsRevealed(loc)) list.Add((dir, loc));
        return list;
    }

    /// <summary>이동 — 현재 장소의 출구이거나, 계단의 descendTo(다음 층)여야 함.</summary>
    public bool MoveTo(LocationDefinition destination)
    {
        if (destination == null || Current == null) return false;

        bool isLinked = Current.descendTo == destination; // 계단 → 다음 층
        if (!isLinked)
        {
            if (!IsRevealed(destination)) return false; // Hidden 방으로는 이동 불가 (명세 2)
            foreach (var (_, loc) in Current.Exits)
                if (loc == destination) { isLinked = true; break; }
        }
        if (!isLinked) return false;

        // 층 이동(하강)이면 새 층의 계단 상태로 리셋 (명세 22)
        if (Current.descendTo == destination)
        {
            StairsSecured = false;
            SecuredStairs = null;
        }

        Current = destination;
        MarkVisited(destination);
        return true;
    }
}