using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상단 영웅 상태 바 (전투 HUD 개편) — 출전 영웅별 카드: 색점/이름/HP/스킬 쿨.
/// 배치·전투 페이즈에만 표시. 카드 구성은 GameUIBuilder, 참조 연결은 빌더가.
/// ※미포함(시스템 부재): 상태 태그(중독 등)/그랩 게이지 — 해당 시스템 도입 시 확장.
/// </summary>
public class HeroStatusBar : MonoBehaviour
{
    public static HeroStatusBar Instance { get; private set; }

    [Header("UI 연결 (빌더가 자동 연결)")]
    public HeroStatusCard[] cards = new HeroStatusCard[4]; // 최대 파티 4

    readonly List<Hero> bound = new List<Hero>();

    void Awake() => Instance = this;

    /// <summary>포션 범위 등 외부 강조 — 전장 링과 HUD 카드 동시 강조용 (ConsumableBar가 호출)</summary>
    public void SetHighlights(HashSet<Hero> highlighted)
    {
        foreach (var c in cards)
            if (c != null && c.gameObject.activeSelf)
                c.SetHighlighted(highlighted != null && c.BoundHero != null && highlighted.Contains(c.BoundHero));
    }

    public void ClearHighlights() => SetHighlights(null);

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
        if (Instance == this) Instance = null;
        if (RunManager.Instance != null)
            RunManager.Instance.OnPhaseChanged -= OnPhase;
    }

    // 페이즈별 세로 위치 — 배치: 상단 라벨("전투 준비 — 지명") 아래 / 전투: 라벨이 비므로 최상단으로
    // (라벨 텍스트만 비우고 자리를 안 옮기면 전투 중 상단에 빈 띠가 남음 — UI 피드백 ⑥ 완결)
    const float TopYBattle = -64f;
    const float TopYPlacement = -186f;

    void OnPhase(RunPhase phase)
    {
        bool show = phase == RunPhase.Placement || phase == RunPhase.Battle;

        var rt = (RectTransform)transform;
        rt.anchoredPosition = new Vector2(0f, phase == RunPhase.Battle ? TopYBattle : TopYPlacement);

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