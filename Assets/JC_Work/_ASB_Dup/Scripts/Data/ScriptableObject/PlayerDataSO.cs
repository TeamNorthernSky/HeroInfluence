using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "ASB/Data/Player Data")]
public class PlayerDataSO : ScriptableObject, IUnitData
{
    [Serializable]
    public class EquipmentSlot
    {
        public EquipmentData Weapon;
        public EquipmentData Armor;
    }

    [Serializable]
    public struct LevelEntry
    {
        public int Level;
        public float HpBonus;
        public float AttackBonus;
        public float DefenseBonus;
        public float SpeedBonus;
    }

    [Header("Identity")]
    [SerializeField] private string _unitId;
    [SerializeField] private string _unitName;

    [Header("Base Stats")]
    [SerializeField] private float _baseMaxHp = 1f;
    [SerializeField] private float _baseAttack = 1f;
    [SerializeField] private float _baseDefense;
    [SerializeField] private float _baseSpeed;

    [Header("Progression")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private List<LevelEntry> levelTable = new List<LevelEntry>();

    [Header("Equipment")]
    [SerializeField] private EquipmentSlot equipmentSlot = new EquipmentSlot();

    public string unitId => _unitId;
    public string unitName => _unitName;
    public TeamType teamType => TeamType.Player;

    public float maxHp => Mathf.Max(1f, _baseMaxHp + ResolveLevelStat(e => e.HpBonus) + GetEquipmentBonus().HP);

    public float attack => Mathf.Max(1f, _baseAttack + ResolveLevelStat(e => e.AttackBonus) + GetEquipmentBonus().Atk);

    public float defense => Mathf.Max(0f, _baseDefense + ResolveLevelStat(e => e.DefenseBonus) + GetEquipmentBonus().DEF);

    public float speed => Mathf.Max(0f, _baseSpeed + ResolveLevelStat(e => e.SpeedBonus) + GetEquipmentBonus().Speed);

    public EquipmentSlot EquipmentSlots => equipmentSlot;

    private float ResolveLevelStat(Func<LevelEntry, float> selector)
    {
        if (levelTable == null || levelTable.Count == 0)
        {
            return 0f;
        }

        float value = 0f;
        int safeLevel = Mathf.Max(1, currentLevel);
        for (int i = 0; i < levelTable.Count; i++)
        {
            var entry = levelTable[i];
            if (entry.Level <= safeLevel)
            {
                value += selector(entry);
            }
        }

        return value;
    }

    private StatBlock GetEquipmentBonus()
    {
        StatBlock total = new StatBlock(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
        if (equipmentSlot == null)
        {
            return total;
        }

        if (equipmentSlot.Weapon != null)
        {
            total += equipmentSlot.Weapon.StatBonus;
        }

        if (equipmentSlot.Armor != null)
        {
            total += equipmentSlot.Armor.StatBonus;
        }

        return total;
    }
}
