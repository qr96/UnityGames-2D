using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 월드 정의 — 고정된 하나의 세계 (GDD 2).
/// World → Region → Location 구조.
/// 업데이트 시 regions에 새 지역을 추가하고 가장자리 장소끼리 연결하면 세계가 확장됨.
/// </summary>
[CreateAssetMenu(menuName = "Game/World/World", fileName = "World")]
public class WorldDefinition : ScriptableObject
{
    public List<RegionDefinition> regions = new List<RegionDefinition>();

    [Tooltip("임시 — 런 시작 위치 결정 방식은 미확정 (GDD 미확정 사항)")]
    public LocationDefinition defaultStartLocation;

    public IEnumerable<LocationDefinition> AllLocations
    {
        get
        {
            foreach (var region in regions)
            {
                if (region == null) continue;
                foreach (var loc in region.locations)
                    if (loc != null) yield return loc;
            }
        }
    }

    public LocationDefinition GetLocationById(string id)
    {
        foreach (var loc in AllLocations)
            if (loc.id == id) return loc;
        return null;
    }

    public RegionDefinition GetRegionOf(LocationDefinition location)
    {
        foreach (var region in regions)
            if (region != null && region.locations.Contains(location)) return region;
        return null;
    }
}
