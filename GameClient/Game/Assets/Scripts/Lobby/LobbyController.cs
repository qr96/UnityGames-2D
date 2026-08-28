using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로비 총괄 (초기 로비 기획 v0.1).
/// - 해금된 영웅들을 용병단 공간에 배치 (GDD 5·6: 많이 해금할수록 북적임, 밀도 상한 있음)
/// - 배경이 에디터에서 제작되지 않았으면 자리표시자 공간(훈련/메인/휴식) 생성 (GDD 7)
/// </summary>
public class LobbyController : MonoBehaviour
{
    [Header("데이터 (에셋 연결 권장 — 비워두면 개발용 데이터)")]
    public HeroDatabase heroDatabase;

    [Header("공간")]
    [Tooltip("로비 가로 반폭 — LobbyCameraDrag.halfWidth와 맞출 것")]
    public float spaceHalfWidth = 18f;
    [Tooltip("영웅이 서 있는 바닥 기준선 Y")]
    public float floorY = -2.5f;
    [Tooltip("바닥에서 위로 퍼지는 배치 폭")]
    public float floorBand = 2.2f;
    [Tooltip("에디터에서 만든 배경 루트 (비워두면 자리표시자 생성)")]
    public Transform authoredSpace;

    [Header("연출")]
    [Tooltip("동시에 로비에 등장하는 영웅 수 상한 (GDD 6: 밀도 제한)")]
    public int maxVisibleHeroes = 12;

    public PlayerProfile Profile { get; private set; }

    readonly List<LobbyHeroActor> actors = new List<LobbyHeroActor>();

    void Awake()
    {
        if (heroDatabase == null)
        {
            heroDatabase = DevGameData.CreateHeroDatabase();
            Debug.LogWarning("[Lobby] HeroDatabase 에셋이 연결되지 않아 개발용 데이터를 사용합니다. " +
                             "[Tools > GrabProto > 게임 데이터 에셋 생성] 후 연결하세요.");
        }

        Profile = PlayerProfile.Load(); // 구 해금 모델 — 로스터 전환 후 로직에서는 미사용 (저장 통합 시 정리)

        // 영입 스펙 v1: 메타 상태 준비 (로스터/골드/영입 후보 — 저장 시스템 전 인메모리)
        var skillPool = heroDatabase.skillPool;
        HeroRoster.SetSkillPool(skillPool != null && skillPool.Count > 0 ? skillPool : DevGameData.CreateSkillPool());
        HeroRoster.EnsureStarters(heroDatabase);
        GoldWallet.EnsureDevGold();
        RecruitShop.EnsureCandidates(heroDatabase);

        if (authoredSpace == null)
            BuildPlaceholderSpace();

        SpawnHeroes();
    }

    void SpawnHeroes()
    {
        // 영입 스펙 v1: 로비 배회 인원 = 실제 보유 로스터 (해금 목록 아님)
        var roster = HeroRoster.Heroes;
        int count = Mathf.Min(roster.Count, maxVisibleHeroes);

        for (int i = 0; i < count; i++)
        {
            if (roster[i].definition == null) continue;
            var go = new GameObject($"LobbyHero_{roster[i].heroId}");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(
                Random.Range(-spaceHalfWidth + 2.5f, spaceHalfWidth - 2.5f),
                floorY + Random.Range(0f, floorBand),
                0f);

            var actor = go.AddComponent<LobbyHeroActor>();
            actor.Init(roster[i].definition, this);
            actors.Add(actor);
        }
    }

    // ---------- 배우 지원 API (LobbyHeroActor가 사용) ----------

    /// <summary>배회 목적지 — 공간 안의 임의 지점</summary>
    public Vector3 RandomWanderPoint(float margin = 2.5f)
    {
        return new Vector3(
            Random.Range(-spaceHalfWidth + margin, spaceHalfWidth - margin),
            floorY + Random.Range(0f, floorBand),
            0f);
    }

    /// <summary>위치를 공간 안으로 보정</summary>
    public Vector3 ClampToSpace(Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, -spaceHalfWidth + 2f, spaceHalfWidth - 2f);
        pos.y = Mathf.Clamp(pos.y, floorY, floorY + floorBand);
        pos.z = 0f;
        return pos;
    }

    /// <summary>자신을 제외한 임의의 다른 영웅 (없으면 null)</summary>
    public LobbyHeroActor GetRandomOtherActor(LobbyHeroActor self)
    {
        if (actors.Count <= 1) return null;
        for (int guard = 0; guard < 8; guard++)
        {
            var pick = actors[Random.Range(0, actors.Count)];
            if (pick != null && pick != self) return pick;
        }
        return null;
    }

    /// <summary>자리표시자 공간 — 기능 없는 생활 연출용 (GDD 7). 아트/에디터 배경으로 교체 지점.</summary>
    void BuildPlaceholderSpace()
    {
        var space = new GameObject("PlaceholderSpace").transform;
        space.SetParent(transform, false);

        // 바닥 스트립
        var ground = UnitFactory.MakeVisual(space, UnitFactory.Square,
            new Color(0.18f, 0.15f, 0.12f), 1f, sortingOrder: -10);
        ground.transform.localScale = new Vector3(spaceHalfWidth * 2f + 6f, 8f, 1f);
        ground.transform.localPosition = new Vector3(0f, floorY + 1f, 0f);

        // 분위기용 세 공간 (훈련 / 메인 / 휴식) — 기능 없음
        CreateAreaPatch(space, "훈련 공간", -spaceHalfWidth * 0.65f, new Color(0.24f, 0.18f, 0.14f));
        CreateAreaPatch(space, "메인 공간", 0f, new Color(0.22f, 0.19f, 0.15f));
        CreateAreaPatch(space, "휴식 공간", spaceHalfWidth * 0.65f, new Color(0.19f, 0.20f, 0.16f));
    }

    void CreateAreaPatch(Transform parent, string label, float x, Color color)
    {
        var patch = UnitFactory.MakeVisual(parent, UnitFactory.Square, color, 1f, sortingOrder: -9);
        patch.transform.localScale = new Vector3(9f, 6f, 1f);
        patch.transform.localPosition = new Vector3(x, floorY + 1f, 0f);

        UnitFactory.MakeWorldLabel(parent, label, new Vector3(x, floorY + 4.6f, 0f), 0.08f, -8, 40);
    }
}