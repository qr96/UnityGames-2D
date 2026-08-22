using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로비 → 게임 씬으로 출정 파티 선택을 전달하는 브릿지 (씬 전환 간 유지되는 정적 데이터).
/// 게임 씬 부트스트랩이 소비 후 Clear — 비어 있으면 개발용 기본 파티로 시작 (게임 씬 직접 실행 지원).
/// </summary>
public static class SortieData
{
    static readonly List<string> selectedHeroIds = new List<string>();

    public static bool HasSelection => selectedHeroIds.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => selectedHeroIds.Clear();

    public static void Set(IEnumerable<string> heroIds)
    {
        selectedHeroIds.Clear();
        foreach (var id in heroIds)
            if (!string.IsNullOrEmpty(id)) selectedHeroIds.Add(id);
    }

    public static void Clear() => selectedHeroIds.Clear();

    /// <summary>저장된 id들을 영웅 정의로 변환</summary>
    public static List<HeroDefinition> Resolve(HeroDatabase db)
    {
        var list = new List<HeroDefinition>();
        if (db == null) return list;
        foreach (var id in selectedHeroIds)
        {
            var def = db.GetById(id);
            if (def != null) list.Add(def);
        }
        return list;
    }
}
