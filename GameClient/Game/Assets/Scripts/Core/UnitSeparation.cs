using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛 겹침 방지 (물리 엔진 없이 소프트 분리).
/// 매 프레임 같은 팀 유닛끼리 반경이 겹치면 겹친 만큼 서로 밀어냄.
/// - 잡혀 있는(공중에 들린) 영웅은 겹침 판정에서 완전히 제외 —
///   내려놓는 순간부터 겹쳐 있으면 그때 자연스럽게 분리됨
/// - 기본: 영웅끼리만 적용 (separateEnemies로 적끼리도 켤 수 있음)
/// - 배치/전투/승리 화면 등 유닛이 전장에 있는 동안 항상 동작
/// </summary>
public class UnitSeparation : MonoBehaviour
{
    [Tooltip("프레임당 겹침 해소 비율 (1 = 즉시, 낮을수록 부드럽게)")]
    [Range(0.05f, 1f)] public float softness = 0.5f;

    public bool separateHeroes = true;
    public bool separateEnemies = false;

    void LateUpdate()
    {
        if (separateHeroes) Separate(UnitRegistry.GetAll(Team.Hero));
        if (separateEnemies) Separate(UnitRegistry.GetAll(Team.Enemy));
    }

    void Separate(List<Unit> units)
    {
        for (int i = 0; i < units.Count; i++)
        {
            for (int j = i + 1; j < units.Count; j++)
            {
                Unit a = units[i];
                Unit b = units[j];

                // 공중(잡힘) 상태는 지상 유닛과 부딪히지 않음
                if (IsAirborne(a) || IsAirborne(b)) continue;

                float minDist = a.radius + b.radius;
                Vector2 delta = b.transform.position - a.transform.position;
                float dist = delta.magnitude;
                if (dist >= minDist) continue;

                // 완전히 같은 위치면 임의 방향으로 분리
                Vector2 dir = dist > 0.0001f
                    ? delta / dist
                    : (Vector2)(Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)) * Vector2.right);

                float push = (minDist - dist) * softness * 0.5f;
                a.transform.position -= (Vector3)(dir * push);
                b.transform.position += (Vector3)(dir * push);
            }
        }
    }

    static bool IsAirborne(Unit u) => u is Hero h && h.IsGrabbed;
}