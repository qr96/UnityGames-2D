using System;
using System.Collections;
using UnityEngine;

public enum Team { Hero, Enemy }

/// <summary>
/// 용사/적 공통 베이스. HP, 피격, 회복, 사망 처리를 담당.
/// 확정 규칙: HP는 전투 간 이월. HP 0 → 사망(비활성화), 사망은 교회에서 부활할 때까지 유지.
/// </summary>
public abstract class Unit : MonoBehaviour
{
    [HideInInspector] public Team team;
    [HideInInspector] public float radius = 0.4f; // 겹침 방지(UnitSeparation) 판정 반경

    public float MaxHP { get; protected set; }
    public float CurrentHP { get; protected set; }
    public bool IsDead => CurrentHP <= 0f;
    public float HPRatio => MaxHP <= 0f ? 0f : CurrentHP / MaxHP;

    public event Action<Unit> OnDeath;

    /// <summary>시간제 버프/디버프 (없으면 null — GetStatus로 지연 생성)</summary>
    public StatusEffects Status { get; private set; }

    protected SpriteRenderer sr;
    Coroutine flashRoutine;
    Color baseColor;
    bool baseColorCached;

    protected virtual void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    protected virtual void OnEnable() => UnitRegistry.Register(this);
    protected virtual void OnDisable() => UnitRegistry.Unregister(this);

    public void SetMaxHP(float value)
    {
        MaxHP = value;
        CurrentHP = value;
    }

    /// <summary>최대/현재 HP를 따로 설정 (전투 간 HP 이월용)</summary>
    public void SetVitals(float max, float current)
    {
        MaxHP = max;
        CurrentHP = Mathf.Clamp(current, 0f, max);
    }

    public StatusEffects GetStatus()
    {
        if (Status == null) Status = gameObject.AddComponent<StatusEffects>();
        return Status;
    }

    public virtual void TakeDamage(float amount)
    {
        if (IsDead) return;
        if (Status != null)
            amount *= Status.Multiplier(StatusEffects.Kind.DamageTaken); // 철벽 등 피해감소
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        Flash(new Color(1f, 0.35f, 0.3f));
        if (IsDead) Die();
    }

    public virtual void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
        Flash(new Color(0.4f, 1f, 0.5f));
    }

    protected virtual void Die()
    {
        OnDeath?.Invoke(this);
        // GDD: 해당 전투에서만 이탈. 파괴하지 않고 비활성화.
        gameObject.SetActive(false);
    }

    protected void Flash(Color c)
    {
        if (sr == null || !gameObject.activeInHierarchy) return;
        if (!baseColorCached) { baseColor = sr.color; baseColorCached = true; }
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(c));
    }

    IEnumerator FlashRoutine(Color c)
    {
        sr.color = c;
        yield return new WaitForSeconds(0.12f);
        sr.color = baseColor;
        flashRoutine = null;
    }
}