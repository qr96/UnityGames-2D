using System;
using UnityEngine;

/// <summary>
/// 일정 시간 유지되는 영역 효과 (성역/화염지대/독구름 등 — Skill Tag: ZONE).
/// 주기(tick)마다 범위 안 대상 팀 유닛에게 onTick 콜백 실행. 교전 종료 시 소멸.
/// 비주얼은 자리표시자 원 — 아트 교체 지점.
/// </summary>
public class ZoneEffect : MonoBehaviour
{
    float radius;
    float tickInterval;
    float lifeLeft;
    float tickTimer; // 0에서 시작 → 첫 틱 즉시
    Team targetTeam;
    Action<Unit> onTick;

    public static ZoneEffect Spawn(Vector3 pos, float radius, float duration, float tickInterval,
        Team targetTeam, Action<Unit> onTick, Color color)
    {
        var go = new GameObject("ZoneEffect");
        go.transform.position = pos;
        UnitFactory.MakeVisual(go.transform, UnitFactory.Circle, color, radius * 2f, sortingOrder: 2);

        var zone = go.AddComponent<ZoneEffect>();
        zone.radius = radius;
        zone.tickInterval = Mathf.Max(0.05f, tickInterval);
        zone.lifeLeft = duration;
        zone.targetTeam = targetTeam;
        zone.onTick = onTick;
        return zone;
    }

    void Update()
    {
        if (!BattleController.CombatActive)
        {
            Destroy(gameObject);
            return;
        }

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer += tickInterval;
            foreach (Unit u in UnitRegistry.GetAll(targetTeam).ToArray()) // 스냅샷: 처치로 목록이 변해도 안전
                if (u != null && !u.IsDead &&
                    Vector2.Distance(u.transform.position, transform.position) <= radius)
                    onTick?.Invoke(u);
        }

        lifeLeft -= Time.deltaTime;
        if (lifeLeft <= 0f)
            Destroy(gameObject);
    }
}