using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 살아있는 유닛 조회용 정적 레지스트리.
/// 레이어/태그 설정 없이 타겟 탐색, 범위 판정, 승패 체크에 사용.
/// </summary>
public static class UnitRegistry
{
    static readonly List<Unit> units = new List<Unit>();

    // 에디터에서 Domain Reload를 꺼둔 경우를 대비해 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => units.Clear();

    public static void Register(Unit u)
    {
        if (!units.Contains(u)) units.Add(u);
    }

    public static void Unregister(Unit u) => units.Remove(u);

    /// <summary>해당 팀의 살아있는 유닛 목록 (매 호출 새 리스트)</summary>
    public static List<Unit> GetAll(Team team)
    {
        var list = new List<Unit>();
        foreach (var u in units)
            if (u != null && u.team == team && !u.IsDead) list.Add(u);
        return list;
    }

    /// <summary>from에서 가장 가까운 살아있는 유닛</summary>
    public static Unit GetNearest(Team team, Vector3 from, Func<Unit, bool> filter = null)
    {
        Unit best = null;
        float bestDist = float.MaxValue;
        foreach (var u in units)
        {
            if (u == null || u.team != team || u.IsDead) continue;
            if (filter != null && !filter(u)) continue;
            float d = (u.transform.position - from).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = u; }
        }
        return best;
    }

    public static bool AnyAlive(Team team)
    {
        foreach (var u in units)
            if (u != null && u.team == team && !u.IsDead) return true;
        return false;
    }
}
