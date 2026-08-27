using UnityEngine;

/// <summary>
/// 원거리 공격 투사체. 발사 시점의 피해량을 들고 목표를 추적, 도달 시 피해 적용.
/// 목표가 도중에 죽으면 마지막 위치까지 날아간 뒤 소멸 (피해 없음).
/// 비주얼은 UnitFactory.SpawnProjectile에서 구성 — 아트 교체 지점.
/// </summary>
public class Projectile : MonoBehaviour
{
    Unit target;
    Vector3 lastTargetPos;
    float damage;
    float speed;
    System.Action<Unit> onHit; // 명중 시 추가 효과 (독 등)

    public void Init(Unit target, float damage, float speed, System.Action<Unit> onHit = null)
    {
        this.target = target;
        this.damage = damage;
        this.speed = speed;
        this.onHit = onHit;
        lastTargetPos = target != null ? target.transform.position : transform.position;
    }

    void Update()
    {
        // 교전이 끝났으면 정리
        if (!BattleController.CombatActive)
        {
            Destroy(gameObject);
            return;
        }

        bool targetAlive = target != null && target.gameObject.activeInHierarchy && !target.IsDead;
        if (targetAlive) lastTargetPos = target.transform.position;

        transform.position = Vector3.MoveTowards(transform.position, lastTargetPos, speed * Time.deltaTime);

        if (Vector2.Distance(transform.position, lastTargetPos) < 0.06f)
        {
            if (targetAlive)
            {
                target.TakeDamage(damage);
                if (!target.IsDead) onHit?.Invoke(target);
            }
            Destroy(gameObject);
        }
    }
}