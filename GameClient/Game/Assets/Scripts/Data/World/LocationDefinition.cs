using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장소(Location) 정의 — 월드의 실제 이동 단위.
/// 개편된 규칙: 출구는 상/하/좌/우 방향 슬롯 (일방통행 — 지나간 장소로는 돌아갈 수 없음).
/// 미리보기 텍스트는 탐험 화면에서 다음 장소의 이름과 함께 표기됨 (데이터 분리).
/// 위치·출구·이름은 세계에 고정된 요소.
/// </summary>
[CreateAssetMenu(menuName = "Game/World/Location", fileName = "Loc_")]
public class LocationDefinition : ScriptableObject
{
    [Header("신원")]
    public string id;             // 저장/조회용 고유 키 (예: "windplain_forest_1")
    [Tooltip("일반 장소는 보편 명칭(숲, 폐허...), 주요 장소만 고유 지명(로엔 마을...)")]
    public string displayName;
    public LocationType type = LocationType.Field;

    [Header("월드 배치 (세계 고정 요소 — 지형/이동 연출/지도 표기 기준)")]
    public Vector2 worldPosition;

    [Header("출구 (일방통행 — 방향별. 비우면 그 방향에 길 없음)")]
    public LocationDefinition north; // 상
    public LocationDefinition south; // 하
    public LocationDefinition west;  // 좌
    public LocationDefinition east;  // 우

    [Header("미리보기 — 탐험 화면에 이름과 함께 표기되는 상태 문자열")]
    [TextArea]
    public string previewText; // 예: "연기가 피어오른다"

    [Header("콘텐츠 구성 — 이번 런에서 '발생 가능'한 것들 (미구현 시스템은 데이터만 보유)")]
    [Tooltip("전투 발생 가능")]
    public bool hasBattle = true;
    [Tooltip("상인 등장 가능 (시스템 추후)")]
    public bool canMerchant;
    [Tooltip("일반 사건 발생 가능 (시스템 추후)")]
    public bool canEvent;
    [Tooltip("이 장소 전용 사건 보유 (시스템 추후)")]
    public bool hasDedicatedEvent;
    [Tooltip("발견 발생 가능 (시스템 추후)")]
    public bool canDiscovery;

    [Header("고정 기능 — 항상 제공 (동작 판정 기준)")]
    public LocationFunction fixedFunction = LocationFunction.None;

    public LocationDefinition GetExit(Direction dir)
    {
        switch (dir)
        {
            case Direction.North: return north;
            case Direction.South: return south;
            case Direction.West: return west;
            case Direction.East: return east;
            default: return null;
        }
    }

    /// <summary>존재하는 출구들 (방향, 목적지)</summary>
    public IEnumerable<(Direction dir, LocationDefinition loc)> Exits
    {
        get
        {
            if (north != null) yield return (Direction.North, north);
            if (south != null) yield return (Direction.South, south);
            if (west != null) yield return (Direction.West, west);
            if (east != null) yield return (Direction.East, east);
        }
    }
}