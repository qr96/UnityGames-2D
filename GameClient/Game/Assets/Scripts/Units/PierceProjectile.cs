using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 직선 관통 투사체 (관통사격 — Skill Tag: PROJECTILE + AOE).
/// 지정 방향으로 날아가며 경로의 폭 안에 들어온 적을 각각 1회씩 타격.
/// </summary>
public class PierceProjectile : MonoBehaviour
{
    Vector3 dir;
    float speed;
    float maxDistance;
    float halfWidth;
    float damage;
    float traveled;

    readonly HashSet<Unit> hit = new HashSet<Unit>();

    public void Init(Vector3 direction, float damage, float maxDistance, float width, float speed = 16f)
    {
        dir = direction.normalized;
        this.damage = damage;
        this.maxDistance = maxDistance;
        halfWidth = width * 0.5f;
        this.speed = speed;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    void Update()
    {
        if (!BattleController.CombatActive)
        {
            Destroy(gameObject);
            return;
        }

        float step = speed * Time.deltaTime;
        transform.position += dir * step;
        traveled += step;

        foreach (Unit u in UnitRegistry.GetAll(Team.Enemy))
        {
            if (hit.Contains(u)) continue;
            if (Vector2.Distance(u.transform.position, transform.position) <= halfWidth + u.radius)
            {
                hit.Add(u);
                u.TakeDamage(damage); // 관통: 맞아도 소멸하지 않음
            }
        }

        if (traveled >= maxDistance)
            Destroy(gameObject);
    }
}
