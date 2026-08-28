/// <summary>
/// 장소의 콘텐츠 성격 — 내부 분류용.
/// GDD 4: 플레이어에게는 '전투 노드/회복 노드'처럼 추상적으로 표현하지 않고
/// 항상 실제 장소(숲, 마을, 야영지...)로 보여준다. 이 enum은 코드/데이터에서만 사용.
/// </summary>
public enum LocationType
{
    Field,        // 일반 장소: 숲, 평원, 산길 등 (전투 등 발생)
    Settlement,   // 정착지: 마을/도시 (병원, 상점 등 시설 — GDD 8)
    Camp,         // 야영지: 체력 관리 (회복량/제한 미정)
    Exploration,  // 탐험 장소: 폐허, 광산, 동굴 (전투/보상/사건)
    Landmark,     // 주요 랜드마크 / 보스 지역
}

/// <summary>
/// 장소의 고정 기능 — 콘텐츠 표의 '고정 기능' 열. 랜덤 콘텐츠와 달리 항상 제공됨.
/// 동작 판정은 이 값 기준 (LocationType은 지형/분류 표현용).
/// </summary>
public enum LocationFunction
{
    None,
    RunStart, // 런 시작 지점 (정보용 — 실제 시작은 WorldDefinition.defaultStartLocation)
    Rest,     // 휴식: 생존자 완전 회복 (예배당/야영지)
    Shop,     // 상점 (시스템 추후)
    Stairs,   // 내려가는 계단 — 도착 시 [귀환 / 내려가기] 선택 (내려가기 = descendTo로 이동)
}

/// <summary>
/// 랜덤 맵 노드의 콘텐츠 종류 (노드 규칙).
/// 정보 노출: 전투 클리어 시 인접 노드의 이 값이 노출됨 (런타임 판정은 MapRunState).
/// </summary>
public enum NodeContent
{
    None,         // 시작 지점 등 콘텐츠 없음
    NormalBattle, // 일반 전투
    EliteBattle,  // 엘리트 전투 (적 티어 판정은 이 값 기준)
    Treasure,     // 보물
    Stairs,       // 내려가는 계단
    Special,      // 특수방 (시스템 추후 — 생성기에서 배치만 지원)
}