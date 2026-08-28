using System.Collections.Generic;
using UnityEngine;

/// <summary>전체 영웅 목록. 영입 후보 풀, 로비 목록의 원천.</summary>
[CreateAssetMenu(menuName = "Game/Hero Database", fileName = "HeroDatabase")]
public class HeroDatabase : ScriptableObject
{
    [Tooltip("랜덤 영웅(영입 후보) 외형 템플릿 풀")]
    public List<HeroDefinition> heroes = new List<HeroDefinition>();

    [Tooltip("시작 영웅 전용 정의 3종 (브란/리나/오웬) — 랜덤 후보 풀과 분리")]
    public List<HeroDefinition> starters = new List<HeroDefinition>();

    [Tooltip("영웅 생성 시 랜덤 배정되는 액티브 풀 (액티브 스펙 v2 — 10종)")]
    public List<SkillDefinition> skillPool = new List<SkillDefinition>();

    public HeroDefinition GetById(string id)
    {
        var found = heroes.Find(h => h != null && h.id == id);
        return found != null ? found : starters.Find(h => h != null && h.id == id);
    }
}