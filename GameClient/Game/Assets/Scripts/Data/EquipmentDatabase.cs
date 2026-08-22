using System.Collections.Generic;
using UnityEngine;

/// <summary>전체 장비 목록 (런 드랍 풀의 원천). 로비/게임 씬이 같은 에셋을 참조.</summary>
[CreateAssetMenu(menuName = "Game/Equipment Database", fileName = "EquipmentDatabase")]
public class EquipmentDatabase : ScriptableObject
{
    public List<EquipmentDefinition> items = new List<EquipmentDefinition>();
}
