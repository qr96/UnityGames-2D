using UnityEngine;

/// <summary>
/// 잡기 슬로우 모션 (실험 기능): 전투 중 영웅을 잡고 있는 동안 시간이 천천히 흐른다.
/// "잡는 순간 = 판단의 시간" — 재배치 판단에 여유를 주는 연출/플레이 장치.
///
/// 동작:
///   · Hero.Grab()/Release()가 Notify로 잡힘 수를 알림 (사망/비활성 누수는 Hero가 방어)
///   · 잡힌 영웅이 1명 이상 + 교전 중일 때만 목표 timeScale = slowScale
///     (준비/탐험 중에는 느려질 것이 없고 조작감만 상함 — 교전 한정)
///   · 전환은 unscaled 시간으로 부드럽게 (급정지/급출발 방지)
///   · fixedDeltaTime도 비례 조정 (물리 사용 시 일관성 — 원복 보장)
///   · 잡힌 영웅 자신의 포인터 추적/버둥거림은 Hero가 unscaled로 처리 → 세상만 느려짐
///
/// 수치 임시: slowScale 0.35, 전환 0.12초. 파괴/씬 전환 시 timeScale 원복.
/// </summary>
public class GrabSlowMotion : MonoBehaviour
{
    [Tooltip("잡는 동안의 시간 배율 (1 = 정상)")]
    [Range(0.05f, 1f)] public float slowScale = 0.35f;

    [Tooltip("배율 전환에 걸리는 시간 (초, unscaled)")]
    public float transitionTime = 0.12f;

    static GrabSlowMotion instance;
    static int grabbedCount;
    static float baseFixedDeltaTime = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        grabbedCount = 0;
        baseFixedDeltaTime = -1f;
    }

    /// <summary>영웅이 잡히는 순간 (Hero.Grab)</summary>
    public static void NotifyGrab()
    {
        grabbedCount++;
        EnsureInstance();
    }

    /// <summary>영웅을 놓는 순간 (Hero.Release / 잡힌 채 비활성화)</summary>
    public static void NotifyRelease()
    {
        grabbedCount = Mathf.Max(0, grabbedCount - 1);
    }

    static void EnsureInstance()
    {
        if (instance != null) return;
        var go = new GameObject("GrabSlowMotion");
        instance = go.AddComponent<GrabSlowMotion>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (baseFixedDeltaTime < 0f) baseFixedDeltaTime = Time.fixedDeltaTime;
    }

    void Update()
    {
        // 교전 중 + 잡는 중일 때만 감속 — 준비/탐험/전리품에서는 항상 정상 속도
        float target = (grabbedCount > 0 && BattleController.CombatActive) ? slowScale : 1f;

        float speed = transitionTime > 0.001f ? 1f / transitionTime : 1000f;
        float next = Mathf.MoveTowards(Time.timeScale, target, speed * Time.unscaledDeltaTime);
        ApplyScale(next);
    }

    void ApplyScale(float scale)
    {
        Time.timeScale = scale;
        if (baseFixedDeltaTime > 0f)
            Time.fixedDeltaTime = baseFixedDeltaTime * scale; // 물리 일관성 (사용 시)
    }

    void OnDestroy()
    {
        // 씬 전환/종료 시 시간 원복 (에디터 정지 포함 안전망)
        Time.timeScale = 1f;
        if (baseFixedDeltaTime > 0f)
            Time.fixedDeltaTime = baseFixedDeltaTime;
        if (instance == this) instance = null;
    }
}
