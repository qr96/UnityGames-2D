using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 중 하단 소모품 바 (4칸). 보유 소모품이 '오른쪽부터' 채워짐.
/// 현재 소모품은 포션 1종 — 전투당 지급 개수만큼 칸에 표시되고,
/// 칸을 전장으로 드래그하면 착탄 지점 주변 용사를 범위 회복 (GDD 4).
/// 소모품 종류가 늘어나면 종류별 아이템 큐로 확장 예정.
/// </summary>
public class ConsumableBar : MonoBehaviour
{
    [Header("포션 효과 (GDD 4 — 수치 미정, 튜닝값)")]
    public float healAmount = 40f;
    public float healRadius = 1.8f;

    readonly List<ConsumableSlot> slots = new List<ConsumableSlot>();
    int potionCount;
    Camera cam;

    void Awake()
    {
        // 에디터에서 만든 자식 슬롯을 자동 수집 — 하이어라키 순서 = 왼쪽→오른쪽
        slots.Clear();
        var found = GetComponentsInChildren<ConsumableSlot>(true);
        for (int i = 0; i < found.Length; i++)
        {
            found[i].bar = this;
            found[i].index = i;
            slots.Add(found[i]);
        }
    }

    /// <summary>전투 시작 시 지급 개수 설정 (BattleController가 호출)</summary>
    public void SetPotions(int count)
    {
        potionCount = count;
        Render();
    }

    /// <summary>슬롯의 포션을 화면 좌표 위치에 투척. 성공 시 true.</summary>
    public bool TryUseAt(int slotIndex, Vector2 screenPos)
    {
        if (potionCount <= 0 || !IsFilled(slotIndex)) return false;

        if (cam == null) cam = Camera.main;
        Vector3 world = cam.ScreenToWorldPoint((Vector3)screenPos);
        world.z = 0f;

        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
        {
            if (Vector2.Distance(u.transform.position, world) <= healRadius)
                u.Heal(healAmount);
        }

        potionCount--;
        Render();
        return true;
    }

    bool IsFilled(int slotIndex)
    {
        int shown = Mathf.Min(potionCount, slots.Count);
        return slotIndex >= slots.Count - shown;
    }

    /// <summary>오른쪽부터 채워서 표시</summary>
    void Render()
    {
        int shown = Mathf.Min(potionCount, slots.Count);
        for (int i = 0; i < slots.Count; i++)
            slots[i].SetFilled(i >= slots.Count - shown, "포션");
    }
}