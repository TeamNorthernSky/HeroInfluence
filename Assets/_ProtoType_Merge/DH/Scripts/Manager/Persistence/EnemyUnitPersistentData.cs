using System;
using UnityEngine;

[Serializable]
public class EnemyUnitPersistentData
{
    [SerializeField] private int unitIndex;
    [SerializeField] private string unitTemplateKey;
    [SerializeField] private int level = 1;
    [SerializeField] private StatBlock baseStats;
    [SerializeField] private StatBlock ingameStats;
    [SerializeField] private float currentHp;
    [SerializeField] private float currentInfluence;
    [SerializeField] private bool isIncapacitated;

    public int UnitIndex => unitIndex;
    public string UnitTemplateKey => unitTemplateKey;
    public int Level => Mathf.Max(1, level);
    public StatBlock BaseStats => baseStats;
    public StatBlock IngameStats => ingameStats;
    public float CurrentHp => Mathf.Max(0f, currentHp);
    public float CurrentInfluence => currentInfluence;
    public bool IsIncapacitated => isIncapacitated;

    public EnemyUnitPersistentData(
        int unitIndex,
        string unitTemplateKey,
        int level,
        StatBlock baseStats,
        StatBlock ingameStats,
        float currentHp,
        float currentInfluence = -1f,
        bool isIncapacitated = false)
    {
        this.unitIndex = Mathf.Max(0, unitIndex);
        this.unitTemplateKey = string.IsNullOrWhiteSpace(unitTemplateKey) ? string.Empty : unitTemplateKey.Trim();
        this.level = Mathf.Max(1, level);
        this.baseStats = baseStats;
        this.ingameStats = ingameStats;
        this.currentHp = Mathf.Max(0f, currentHp);
        this.currentInfluence = currentInfluence;
        this.isIncapacitated = isIncapacitated;
    }
}
