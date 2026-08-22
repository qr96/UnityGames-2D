using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장소(Location/Node) 정의 — 월드의 실제 이동 단위 (GDD 2).
/// 위치·연결·이름은 세계에 고정된 요소 (GDD 7).
/// 이번 런에서 무엇이 벌어지는지(적, 사건, 상품 등)는 런 상태가 별도로 결정.
/// </summary>
[CreateAssetMenu(menuName = "Game/World/Location", fileName = "Loc_")]
public class LocationDefinition : ScriptableObject
{
    [Header("신원")]
    public string id;             // 저장/조회용 고유 키 (예: "windplain_forest_1")
    [Tooltip("GDD 3: 일반 장소는 보편 명칭(숲, 폐허...), 주요 장소만 고유 지명(로엔 마을...)")]
    public string displayName;
    public LocationType type = LocationType.Field;

    [Header("월드 배치 (세계 고정 요소)")]
    public Vector2 worldPosition; // 탐험 카메라/지형 배치의 기준 좌표

    [Tooltip("길로 연결된 인접 장소. 반드시 양쪽 모두 서로를 등록 (양방향 그래프)")]
    public List<LocationDefinition> connections = new List<LocationDefinition>();

    [Header("콘텐츠 (임시 — 콘텐츠 시스템 확장 전)")]
    [Tooltip("도착 시 전투가 발생하는 장소인지. 이후 '장소에서 발생하는 콘텐츠' 목록으로 확장 예정 (GDD 4)")]
    public bool hasBattle = true;

    public bool IsConnectedTo(LocationDefinition other) =>
        other != null && connections.Contains(other);
}
