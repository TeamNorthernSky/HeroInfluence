using System;

[Serializable]
public struct EquipmentStatBlock
{
    public float HP;
    public float Atk;
    public float DEF;
    public float CriticalRate;
    public float CounterRate;
    public float AvoidRate;
    public float Speed;

    public EquipmentStatBlock(
        float hp,
        float atk,
        float def,
        float criticalRate,
        float counterRate,
        float avoidRate,
        float speed)
    {
        HP = hp;
        Atk = atk;
        DEF = def;
        CriticalRate = criticalRate;
        CounterRate = counterRate;
        AvoidRate = avoidRate;
        Speed = speed;
    }

    public static EquipmentStatBlock FromWeaponData(WeaponData weaponData)
    {
        if (weaponData == null)
            return default;

        return new EquipmentStatBlock(
            weaponData.BonusHP,
            weaponData.BonusATK,
            weaponData.BonusDEF,
            weaponData.BonusCriticalRate,
            weaponData.BonusCounterRate,
            weaponData.BonusReduceRate,
            weaponData.BonusSpeed);
    }

    public StatBlock ToStatBlock()
    {
        return new StatBlock(
            hp: HP,
            atk: Atk,
            def: DEF,
            luck: 0f,
            speed: Speed,
            criticalRate: CriticalRate,
            counterRate: CounterRate,
            avoidRate: AvoidRate);
    }
}
