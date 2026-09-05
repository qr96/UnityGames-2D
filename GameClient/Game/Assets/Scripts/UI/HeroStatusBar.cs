using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상단 영웅 상태 바 (전투 HUD 개편) — 출전 영웅별 카드: 색점/이름/HP/스킬 쿨.
/// 배치·전투 페이즈에만 표시. 카드 구성은 GameUIBuilder, 참조 연결은 빌더가.
/// ※미포함(시스템 부재): 상태 태그(중독 등)/그랩 게이지 — 해당 시스템 도입 시 확장.
/// </summary>
public class HeroStatusBar : MonoBehaviour
{
    [Header("UI 연결 (빌더가 자동 연결)")]
    public HeroStatusCard[] cards = new HeroStatusCard[5]; // 최대 파티 5

    readonly List<Hero> bound = new List<Hero>();

    void Start()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnPhaseChanged += OnPhase;
            OnPhase(RunManager.Instance.Phase);
        }
    }

    void OnDestroy()
    {
        if (RunManager.Instance != null)
            RunManager.Instance.OnPhaseChanged -= OnPhase;
    }

    void OnPhase(RunPhase phase)
    {
        bool show = phase == RunPhase.Placement || phase == RunPhase.Battle;
        // 자기 자신을 끄면 OnPhase를 못 받으므로 카드만 토글
        if (show) Rebind();
        else foreach (var c in cards) if (c != null) c.gameObject.SetActive(false);
    }

    void Rebind()
    {
        bound.Clear();
        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
            if (u is Hero h) bound.Add(h);

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            cards[i].Bind(i < bound.Count ? bound[i] : null);
        }
    }

    void Update()
    {
        foreach (var c in cards)
            if (c != null && c.gameObject.activeSelf) c.Refresh();
    }
}
