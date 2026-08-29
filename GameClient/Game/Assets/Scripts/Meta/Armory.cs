using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장비 보관소 (장비 영속 v1) — 저장 시스템 도입 전 인메모리.
/// 장비/무기는 런 종료로 소멸하지 않는다:
///   · 미장착 장비는 전부 여기 보관 — RunState.inventory가 이 리스트를 '그대로' 공유하므로
///     런 중 획득/장착/해제가 즉시 영구 반영됨 (귀환/클리어 시 옮길 것 없음)
///   · 장착 장비는 OwnedHero에 유지 (영웅과 운명 공유 — 사망 시 함께 소멸)
///   · 전멸 시에는 '이번 원정에서 획득한 전리품'만 제거 (탐험 규칙 — RunManager가 처리)
/// 저장 도입 시 items가 직렬화 대상.
/// </summary>
public static class Armory
{
    static readonly List<EquipmentDefinition> items = new List<EquipmentDefinition>();

    /// <summary>보관 목록 — RunState.inventory가 직접 이 인스턴스를 사용 (복사 아님)</summary>
    public static List<EquipmentDefinition> Items => items;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => items.Clear();

    public static void Add(EquipmentDefinition item)
    {
        if (item != null) items.Add(item);
    }

    /// <summary>한 개만 제거 (같은 정의가 여러 개 있을 수 있음). 없으면 무시 — 장착 중 소멸 케이스.</summary>
    public static void RemoveOnce(EquipmentDefinition item)
    {
        if (item != null) items.Remove(item);
    }
}
