using System.Collections.Generic;
using UnityEngine;

/// <summary>전체 영웅 목록. 영입 후보 풀, 로비 목록의 원천.</summary>
[CreateAssetMenu(menuName = "Game/Hero Database", fileName = "HeroDatabase")]
public class HeroDatabase : ScriptableObject
{
    public List<HeroDefinition> heroes = new List<HeroDefinition>();

    public HeroDefinition GetById(string id) =>
        heroes.Find(h => h != null && h.id == id);
}
