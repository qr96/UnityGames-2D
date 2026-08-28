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

    /// <summary>저장된 id들을 영웅 정의로 변환 (구 방식 — 정의 id 기반 로비 호환용)</summary>
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

    /// <summary>
    /// 저장된 id들을 보유 영웅으로 변환 (영입 스펙 v1 — 기본 경로).
    /// heroId 우선 매칭, 실패 시 정의 id로 폴백 (구 로비가 정의 id를 넣어도 동작).
    /// </summary>
    public static List<OwnedHero> ResolveOwned()
    {
        var list = new List<OwnedHero>();
        foreach (var id in selectedHeroIds)
        {
            var hero = HeroRoster.FindById(id);
            if (hero != null && !list.Contains(hero)) list.Add(hero);
        }
        return list;
    }
}
