using UnityEngine;

/// <summary>
/// 골드 지갑 (영입 스펙 v1) — 저장 시스템 도입 전 인메모리 대체물.
/// 획득 경로는 미정 — 개발용 시작 골드만 지급. 저장 도입 시 PlayerProfile로 통합 예정.
/// </summary>
public static class GoldWallet
{
    const int DevStartingGold = 300; // ※ 임시 — 골드 획득 경로 확정 전 개발용

    public static int Gold { get; private set; }
    static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Gold = 0; initialized = false; }

    /// <summary>부트스트랩/로비 진입 시 호출 — 최초 1회만 개발용 골드 지급.</summary>
    public static void EnsureDevGold()
    {
        if (initialized) return;
        initialized = true;
        Gold = DevStartingGold;
    }

    public static bool CanAfford(int cost) => cost <= Gold;

    public static bool Spend(int cost)
    {
        if (cost < 0 || !CanAfford(cost)) return false;
        Gold -= cost;
        return true;
    }

    public static void Add(int amount)
    {
        if (amount > 0) Gold += amount;
    }
}
