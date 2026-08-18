using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 플레이어 포션.
/// GDD 4: 전투당 N개 지급(BattleController가 SetCount로 리셋), UI 드래그로 투척,
/// 착탄 지점 주변 용사 범위 회복. N/회복량/범위는 RunConfig 및 인스펙터에서 튜닝.
/// </summary>
public class PotionButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int potionCount = 3;
    public float healAmount = 40f;
    public float healRadius = 1.8f;
    public Text countText;

    RectTransform rt;
    Vector2 homePos;
    Camera cam;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        homePos = rt.anchoredPosition;
        cam = Camera.main;
        UpdateText();
    }

    /// <summary>전투 시작 시 지급 개수 리셋 (BattleController가 호출)</summary>
    public void SetCount(int count)
    {
        potionCount = count;
        UpdateText();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (potionCount <= 0)
            e.pointerDrag = null;
    }

    public void OnDrag(PointerEventData e)
    {
        rt.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (cam == null) cam = Camera.main;
        Vector3 world = cam.ScreenToWorldPoint(e.position);
        world.z = 0f;
        Throw(world);
        rt.anchoredPosition = homePos;
    }

    void Throw(Vector3 world)
    {
        if (potionCount <= 0) return;
        potionCount--;

        foreach (Unit u in UnitRegistry.GetAll(Team.Hero))
        {
            if (Vector2.Distance(u.transform.position, world) <= healRadius)
                u.Heal(healAmount);
        }
        UpdateText();
    }

    void UpdateText()
    {
        if (countText != null) countText.text = "x" + potionCount;
    }
}
