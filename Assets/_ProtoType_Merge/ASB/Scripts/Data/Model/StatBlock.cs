using System;
using UnityEngine;



[Serializable]
public struct StatBlock
{
    // scalar stats
    public float HP;
    public float Atk;
    public float DEF;
    public float Luck;

    // rate/speed stats
    public float Speed;
    public float CriticalRate;
    public float CritMultiplier;
    public float CounterRate;
    public float AvoidRate;

    [SerializeField] private float influence;
    public float Influence
    {
        get => influence;
        set => influence = Mathf.Clamp(value, 0f, 200f);
    }

    // 전투 계산 코드에서 새 이름으로 접근할 수 있도록 호환 프로퍼티 제공
    public float CritRate
    {
        get => CriticalRate;
        set => CriticalRate = value;
    }

    public float EvadeRate
    {
        get => AvoidRate;
        set => AvoidRate = value;
    }

    public StatBlock(
        float hp,
        float atk,
        float def,
        float luck,
        float speed,
        float criticalRate = 0.0f,
        float critMultiplier = 0.0f,
        float counterRate = 0.0f,
        float avoidRate = 0.0f,
        float influence = 0f)
    {
        HP = hp;
        Atk = atk;
        DEF = def;
        Luck = luck;
        Speed = speed;
        CriticalRate = criticalRate;
        CritMultiplier = critMultiplier;
        CounterRate = counterRate;
        AvoidRate = avoidRate;
        this.influence = Mathf.Clamp(influence, 0f, 200f);

    }

    public static StatBlock operator +(StatBlock a, StatBlock b)
    {
        return new StatBlock(
            a.HP + b.HP,
            a.Atk + b.Atk,
            a.DEF + b.DEF,
            a.Luck + b.Luck,
            a.Speed + b.Speed,
            a.CriticalRate + b.CriticalRate,
            a.CritMultiplier + b.CritMultiplier,
            a.CounterRate + b.CounterRate,
            a.AvoidRate + b.AvoidRate,
            a.Influence + b.Influence
        );
    }


    public static StatBlock operator *(StatBlock a, StatBlock b)
    {
        return new StatBlock(
            a.HP * b.HP,
            a.Atk * b.Atk,
            a.DEF * b.DEF,
            a.Luck ,
            a.Speed,
            a.CriticalRate,
            a.CritMultiplier,
            a.CounterRate,
            a.AvoidRate,
            a.Influence
        );
    }

    public static StatBlock operator *(StatBlock a, int b)
    {
        return new StatBlock(
            a.HP * b,
            a.Atk * b,
            a.DEF * b,
            a.Luck,
            a.Speed,
            a.CriticalRate,
            a.CritMultiplier,
            a.CounterRate,
            a.AvoidRate,
            a.Influence
        );
    }


    public void ClampToMinimumOne()
    {
       
        Atk = Math.Max(1f, Atk);
        DEF = Math.Max(0f, DEF);
        HP = Math.Max(1f, HP);

        
        CriticalRate = Math.Max(0f, CriticalRate);
        CritMultiplier = Math.Max(1f, CritMultiplier);
        CounterRate = Math.Max(0f, CounterRate);
        AvoidRate = Math.Max(0f, AvoidRate);
        Influence = Mathf.Clamp(Influence, 0f, 200f);

        Speed = Math.Max(0f, Speed);
        Luck = Math.Max(0f, Luck);
    }
}


// HP / Atk / DEF 
[Serializable]
public struct StatWeights
{
    public float HP;

    public float Atk;

    public float DEF;

    public StatWeights(float hp, float atk, float def)
    {
        HP = hp;
        Atk = atk;
        DEF = def;
    }

    public static StatWeights One => new StatWeights(1f, 1f, 1f);

    public static StatBlock operator *(StatBlock a, StatWeights b)
    {
        return new StatBlock(
            a.HP * b.HP,
            a.Atk * b.Atk,
            a.DEF * b.DEF,
            a.Luck,
            a.Speed,
            a.CriticalRate,
            a.CritMultiplier,
            a.CounterRate,
            a.AvoidRate,
            a.Influence
        );
    }

    public static StatWeights operator *(StatWeights a, int b)
    {
        return new StatWeights(
            a.HP * b,
            a.Atk * b,
            a.DEF * b
        );
    }

    public static StatWeights operator+(StatWeights a, StatWeights b)
    {
        return new StatWeights(
            a.HP + b.HP,
            a.Atk + b.Atk,
            a.DEF + b.DEF
        );
    }

    public static StatBlock operator +(StatBlock a, StatWeights b)
    {
        return new StatBlock(
            a.HP + b.HP,
            a.Atk + b.Atk,
            a.DEF + b.DEF,
            a.Luck,
            a.Speed,
            a.CriticalRate,
            a.CritMultiplier,
            a.CounterRate,
            a.AvoidRate,
            a.Influence
        );
    }

}