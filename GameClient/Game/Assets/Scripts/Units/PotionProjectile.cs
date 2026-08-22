using System;
using UnityEngine;

/// <summary>
/// 포션 투척 연출. 시작점에서 착탄 지점까지 포물선으로 날아간 뒤 onImpact 호출.
/// 회전하며 날아가고, 도착 시 스스로 소멸. 교전이 끝나면 착탄 없이 정리.
/// 비주얼은 UnitFactory.SpawnPotionProjectile에서 구성 — 아트 교체 지점.
/// </summary>
public class PotionProjectile : MonoBehaviour
{
    Vector3 start;
    Vector3 target;
    float duration;
    float arcHeight;
    float spinSpeed;
    float t;
    Action onImpact;

    public void Init(Vector3 start, Vector3 target, float duration, float arcHeight, Action onImpact)
    {
        this.start = start;
        this.target = target;
        this.duration = Mathf.Max(0.05f, duration);
        this.arcHeight = arcHeight;
        this.onImpact = onImpact;
        spinSpeed = UnityEngine.Random.Range(420f, 620f);
        transform.position = start;
    }

    void Update()
    {
        // 교전이 끝났으면 착탄 없이 정리
        if (!BattleController.CombatActive)
        {
            Destroy(gameObject);
            return;
        }

        t += Time.deltaTime / duration;
        float k = Mathf.Clamp01(t);

        Vector3 pos = Vector3.Lerp(start, target, k);
        pos.y += Mathf.Sin(k * Mathf.PI) * arcHeight; // 포물선
        transform.position = pos;
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

        if (t >= 1f)
        {
            onImpact?.Invoke();
            Destroy(gameObject);
        }
    }
}
