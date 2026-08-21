using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛 겹침 방지 (물리 엔진 없이 소프트 분리).
/// 팀 구분 없이 전장의 모든 유닛끼리 반경이 겹치면 겹친 만큼 서로 밀어냄.
/// - 잡혀 있는(공중에 들린) 영웅은 판정에서 완전히 제외 —
///   드래그 중에는 어떤 유닛과도 간섭하지 않고, 내려놓은 뒤부터 분리됨
/// - 배치/전투/승리 화면 등 유닛이 전장에 있는 동안 항상 동작
/// </summary>
public class UnitSeparation : MonoBehaviour
{
    [Tooltip("프레임당 겹침 해소 비율 (1 = 즉시, 낮을수록 부드럽게)")]
    [Range(0.05f, 1f)] public float softness = 0.5f;

    readonly List<Unit> buffer = new List<Unit>();

    void LateUpdate()
    {
        // 전장의 살아있는 유닛 전체 (팀 무관)
        buffer.Clear();
        buffer.AddRange(UnitRegistry.GetAll(Team.Hero));
        buffer.AddRange(UnitRegistry.GetAll(Team.Enemy));
        Separate(buffer);
    }

    void Separate(List<Unit> units)
    {
        for (int i = 0; i < units.Count; i++)
        {
            for (int j = i + 1; j < units.Count; j++)
            {
                Unit a = units[i];
                Unit b = units[j];

                // 공중(잡힘) 상태는 어떤 유닛과도 간섭하지 않음
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