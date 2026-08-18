using System;
using System.Collections;
using UnityEngine;

public enum Team { Hero, Enemy }

/// <summary>
/// 용사/적 공통 베이스. HP, 피격, 회복, 사망 처리를 담당.
/// GDD: HP 0 → 해당 전투에서 사망(오브젝트 비활성화). 영구 사망 아님.
/// </summary>
public abstract class Unit : MonoBehaviour
{
    [HideInInspector] public Team team;

    public float MaxHP { get; protected set; }
    public float CurrentHP { get; protected set; }
    public bool IsDead => CurrentHP <= 0f;
    public float HPRatio => MaxHP <= 0f ? 0f : CurrentHP / MaxHP;

    public event Action<Unit> OnDeath;

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

    public virtual void TakeDamage(float amount)
    {
        if (IsDead) return;
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
