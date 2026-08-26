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

    readonly List<Mod> mods = new List<Mod>();

    void Update()
    {
        for (int i = mods.Count - 1; i >= 0; i--)
        {
            mods[i].timeLeft -= Time.deltaTime;
            if (mods[i].timeLeft <= 0f)
                mods.RemoveAt(i);
        }
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
