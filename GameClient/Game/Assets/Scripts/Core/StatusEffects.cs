using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛의 시간제 상태 효과 (버프/디버프). Unit.GetStatus()로 지연 생성.
/// 곱연산 계수(피해감소/공속/공격력/이속)와 합연산(흡혈), 기절을 관리.
/// 유닛은 전투마다 재생성되므로 효과는 전투를 넘겨 남지 않음.
/// </summary>
public class StatusEffects : MonoBehaviour
{
    public enum Kind
    {
        DamageTaken, // 받는 피해 계수 (0.4 = -60%)
        AttackSpeed, // 공격 속도 계수 (1.6 = +60%)
        Damage,      // 주는 피해 계수
        MoveSpeed,   // 이동 속도 계수
        Lifesteal,   // 흡혈 비율 (합연산, 0.15 = 15%)
        Stun,        // 기절 (value 무시)
    }

    class Mod
    {
        public Kind kind;
        public float value;
        public float timeLeft;
    }

    class Dot
    {
        public string id;
        public float damagePerTick;
        public float tickInterval;
        public float timeLeft;
        public float tickTimer;
    }

    class Shield
    {
        public float remaining;
        public float timeLeft;
    }

    readonly List<Mod> mods = new List<Mod>();
    readonly List<Dot> dots = new List<Dot>();
    readonly List<Shield> shields = new List<Shield>();
    Unit unit;

    void Awake()
    {
        unit = GetComponent<Unit>();
    }

    void Update()
    {
        for (int i = mods.Count - 1; i >= 0; i--)
        {
            mods[i].timeLeft -= Time.deltaTime;
            if (mods[i].timeLeft <= 0f)
                mods.RemoveAt(i);
        }

        // 지속 피해 (독 등)
        for (int i = dots.Count - 1; i >= 0; i--)
        {
            Dot dot = dots[i];
            dot.timeLeft -= Time.deltaTime;
            dot.tickTimer -= Time.deltaTime;

            if (dot.tickTimer <= 0f && unit != null && !unit.IsDead)
            {
                dot.tickTimer += dot.tickInterval;
                unit.TakeDamage(dot.damagePerTick);
            }
            if (dot.timeLeft <= 0f)
                dots.RemoveAt(i);
        }

        // 보호막 만료
        for (int i = shields.Count - 1; i >= 0; i--)
        {
            shields[i].timeLeft -= Time.deltaTime;
            if (shields[i].timeLeft <= 0f || shields[i].remaining <= 0f)
                shields.RemoveAt(i);
        }
    }

    // ---------- 보호막 (액티브 스펙 v2: 보호막 스킬) ----------

    /// <summary>보호막 부여 — 여러 장 중첩 가능, 각각 지속시간 만료 시 소멸.</summary>
    public void AddShield(float amount, float duration)
    {
        if (amount <= 0f) return;
        shields.Add(new Shield { remaining = amount, timeLeft = duration });
    }

    /// <summary>피해를 보호막이 먼저 흡수 (오래된 것부터 소모) — 남은 피해를 반환.</summary>
    public float AbsorbDamage(float amount)
    {
        for (int i = 0; i < shields.Count && amount > 0f; i++)
        {
            float used = Mathf.Min(shields[i].remaining, amount);
            shields[i].remaining -= used;
            amount -= used;
        }
        shields.RemoveAll(s => s.remaining <= 0f);
        return amount;
    }

    /// <summary>남은 보호막 총량 (HP바 표시 등)</summary>
    public float TotalShield
    {
        get
        {
            float total = 0f;
            foreach (var s in shields) total += s.remaining;
            return total;
        }
    }

    /// <summary>
    /// 지속 피해 부여 — 같은 id는 중첩되지 않고 지속시간/피해가 갱신됨 (확정 규칙).
    /// totalDamage를 duration에 걸쳐 tickInterval마다 균등 분배. 첫 틱은 tickInterval 후.
    /// </summary>
    public void AddOrRefreshDot(string id, float totalDamage, float duration, float tickInterval = 1f)
    {
        int ticks = Mathf.Max(1, Mathf.RoundToInt(duration / tickInterval));
        float perTick = totalDamage / ticks;

        foreach (var dot in dots)
        {
            if (dot.id != id) continue;
            dot.damagePerTick = perTick;
            dot.tickInterval = tickInterval;
            dot.timeLeft = duration; // 갱신 (틱 타이머는 유지)
            return;
        }

        dots.Add(new Dot
        {
            id = id,
            damagePerTick = perTick,
            tickInterval = tickInterval,
            timeLeft = duration,
            tickTimer = tickInterval,
        });
    }

    public void Add(Kind kind, float value, float duration)
    {
        mods.Add(new Mod { kind = kind, value = value, timeLeft = duration });
    }

    /// <summary>곱연산 계수 (해당 종류 없으면 1)</summary>
    public float Multiplier(Kind kind)
    {
        float m = 1f;
        foreach (var mod in mods)
            if (mod.kind == kind) m *= mod.value;
        return m;
    }

    /// <summary>합연산 수치 (흡혈 등)</summary>
    public float Sum(Kind kind)
    {
        float s = 0f;
        foreach (var mod in mods)
            if (mod.kind == kind) s += mod.value;
        return s;
    }

    public bool IsStunned
    {
        get
        {
            foreach (var mod in mods)
                if (mod.kind == Kind.Stun) return true;
            return false;
        }
    }
}