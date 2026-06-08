// Auto Generated. Do not modify.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerWeaponData
{
    public int WeaponIndex;
    public string Class;
    public string WeaponName;
    public string WaeponDescription;
    public int BonusMaxHPLv1;
    public int BonusMaxHPLv2;
    public int BonusMaxHPLv3;
    public int BonusMaxHPLv4;
    public int BonusMaxHPLv5;
    public int BonusATKLv1;
    public int BonusATKLv2;
    public int BonusATKLv3;
    public int BonusATKLv4;
    public int BonusATKLv5;
    public int BonusDEFLv1;
    public int BonusDEFLv2;
    public int BonusDEFLv3;
    public int BonusDEFLv4;
    public int BonusDEFLv5;
    public float BonusCriticalRate;
    public float BonusCounterRate;
    public float BonusReduceRate;
    public int BonusSpeed;
    public int WeaponSkillIndex;
    public string WeaponSkillName;
    public string WeaponSkillDescription;
    public int IPCost;
    public int WeaponSkillEffect;
    public int WeaponSkillRange;
    public int WeaponSkillRangeLine;
    public int WeaponSkillTarget;
    public List<int> WeaponSkillMultiTarget;
    public int WeaponSkill_MultiTargetType;
    public int WeaponSkillMultiTargetCount;
    public float WeaponSkillValueLv1;
    public float WeaponSkillSubValueLv1;
    public float WeaponSkillValueLv2;
    public float WeaponSkillSubValueLv2;
    public float WeaponSkillValueLv3;
    public float WeaponSkillSubValueLv3;
    public float WeaponSkillValueLv4;
    public float WeaponSkillSubValueLv4;
    public float WeaponSkillValueLv5;
    public float WeaponSkillSubValueLv5;
}

[CreateAssetMenu(fileName="PlayerWeaponDataTable", menuName="DataTable/PlayerWeapon")]
public class PlayerWeaponDataTable : ScriptableObject
{
    public List<PlayerWeaponData> DataList = new List<PlayerWeaponData>();
}
