using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영입 상점 (영입 스펙 v1) — 로비/거점에서 노출. 저장 시스템 도입 전 인메모리.
///   · 후보 항상 3칸 — 랜덤 생성 (스탯 굴림 + 액티브 랜덤), 정보 전부 공개는 UI 담당
///   · 갱신: 원정 1회 종료 시(클리어/실패/귀환 모두) 후보 3명 전체 교체 — RunManager가 호출
///   · 수동 리롤 없음. 영입한 자리는 다음 갱신까지 빈 칸 유지
///   · 비용: 골드, 후보별 동일가 (정확한 가격 미정 — 임시 상수)
/// </summary>
public static class RecruitShop
{
    public const int CandidateCount = 3;
    public const int Price = 100; // ※ 임시 — 정확한 골드 가격 미정

    static readonly OwnedHero[] candidates = new OwnedHero[CandidateCount];
    static bool initialized;

    /// <summary>현재 후보 (영입/미생성 자리는 null — 다음 갱신까지 빈 칸).</summary>
    public static IReadOnlyList<OwnedHero> Candidates => candidates;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        for (int i = 0; i < candidates.Length; i++) candidates[i] = null;
        initialized = false;
    }

    /// <summary>최초 진입 시 후보 생성 (이미 있으면 유지 — 갱신은 원정 종료에만).</summary>
    public static void EnsureCandidates(HeroDatabase db)
    {
        if (initialized) return;
        initialized = true;
        Refresh(db);
    }

    /// <summary>후보 3명 전체 교체 — 원정 1회 종료 시 RunManager가 호출.</summary>
    public static void Refresh(HeroDatabase db)
    {
        var rng = new System.Random();
        for (int i = 0; i < candidates.Length; i++)
            candidates[i] = HeroRoster.CreateRandomHero(db, rng);
    }

    /// <summary>영입 가능 여부 (UI 버튼 활성 판정용).</summary>
    public static bool CanRecruit(int index) =>
        index >= 0 && index < candidates.Length && candidates[index] != null
        && HeroRoster.HasSpace && GoldWallet.CanAfford(Price);

    /// <summary>
    /// 후보 영입 — 골드 차감 + 로스터 편입 + 해당 자리 비움.
    /// 실패 사유: 빈 칸 / 골드 부족 / 로스터 가득(8명).
    /// </summary>
    public static bool TryRecruit(int index)
    {
        if (!CanRecruit(index)) return false;

        var hero = candidates[index];
        if (!GoldWallet.Spend(Price)) return false;
        if (!HeroRoster.Recruit(hero))
        {
            GoldWallet.Add(Price); // 편입 실패 시 환불 (동시성 방어)
            return false;
        }
        candidates[index] = null; // 다음 갱신까지 빈 칸
        return true;
    }
}
