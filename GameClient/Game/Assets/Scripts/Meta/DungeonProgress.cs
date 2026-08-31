using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 던전 영구 진행 상태 (던전 명세: 외부 진입 포인트) — 저장 시스템 도입 전 인메모리.
///   · 마일스톤 층(11/21/31F 등)의 진입 포인트에 '도달'하면 즉시 영구 개방 (프로토 확정: 퍼즐 생략)
///   · 개방된 층은 이후 원정의 시작 지점으로 선택 가능 (SortiePanel)
/// 저장 도입 시 openedEntryFloors가 직렬화 대상.
/// </summary>
public static class DungeonProgress
{
    static readonly HashSet<int> openedEntryFloors = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => openedEntryFloors.Clear();

    public static bool IsOpened(int floorNumber) => openedEntryFloors.Contains(floorNumber);

    /// <summary>진입 포인트 개방 — 새로 열렸으면 true</summary>
    public static bool Open(int floorNumber) => openedEntryFloors.Add(floorNumber);

    /// <summary>선택 가능한 출발 층 목록 — 1층 + 개방된 진입 포인트 (오름차순)</summary>
    public static List<int> GetStartFloorOptions()
    {
        var list = new List<int> { 1 };
        list.AddRange(openedEntryFloors.OrderBy(f => f));
        return list;
    }
}
