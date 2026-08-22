using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지역(Region) 정의 — 여러 장소를 묶는 단위 (GDD 2).
/// 지역은 기억할 수 있는 고유 지명을 가진다 (GDD 3: "바람 평원", "잿빛 산맥"...).
/// </summary>
[CreateAssetMenu(menuName = "Game/World/Region", fileName = "Region_")]
public class RegionDefinition : ScriptableObject
{
    public string id;
    public string regionName; // 고유 지명
    public List<LocationDefinition> locations = new List<LocationDefinition>();
}
