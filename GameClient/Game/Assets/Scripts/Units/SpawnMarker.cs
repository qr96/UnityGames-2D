using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 적 스폰 예고 마커. warnTime 동안 예고 연출 후 onComplete 호출 (실제 적 생성은 호출자 책임).
///
/// 지금 비주얼은 자리표시자(붉은 원이 차오름)이며,
/// 아트/연출이 들어오면 UnitFactory.CreateSpawnMarker의 비주얼 구성만 교체하면 됨 — 로직 불변.
/// warnTime이 0 이하면 즉시 완료 (= 즉시 등장과 동일 동작).
/// </summary>
public class SpawnMarker : MonoBehaviour
{
    public SpriteRenderer ring;  // 예고 영역 표시
    public SpriteRenderer fill;  // 차오르는 게이지

    public void Play(float warnTime, Action onComplete)
    {
        if (warnTime <= 0f)
        {
            onComplete?.Invoke();
            Destroy(gameObject);
            return;
        }
        StartCoroutine(Run(warnTime, onComplete));
    }

    IEnumerator Run(float warnTime, Action onComplete)
    {
        if (fill != null) fill.transform.localScale = Vector3.zero;

        float t = 0f;
        while (t < warnTime)
        {
            t += Time.deltaTime;
            if (fill != null && ring != null)
                fill.transform.localScale = ring.transform.localScale * Mathf.Clamp01(t / warnTime);
            yield return null;
        }

        onComplete?.Invoke();
        Destroy(gameObject);
    }
}
