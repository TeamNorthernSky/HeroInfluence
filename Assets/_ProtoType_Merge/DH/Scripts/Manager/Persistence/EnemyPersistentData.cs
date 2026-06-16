using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

[Serializable]
public class EnemyPersistentData
{
    public string EnemyId => enemyId;
    public IReadOnlyList<int> UnitIndices => unitIndices;
    public IReadOnlyList<int> UnitSlots => unitSlots;

    [UnityEngine.SerializeField] private string enemyId;
    [FormerlySerializedAs("combatUnitIndices")]
    [UnityEngine.SerializeField] private List<int> unitIndices = new List<int>();
    [UnityEngine.SerializeField] private List<int> unitSlots = new List<int>();

    public EnemyPersistentData(string enemyId, IReadOnlyList<int> unitIndices)
        : this(enemyId, unitIndices, null)
    {
    }

    public EnemyPersistentData(string enemyId, IReadOnlyList<int> unitIndices, IReadOnlyList<int> unitSlots)
    {
        this.enemyId = string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId;
        SetUnitIndices(unitIndices);
        SetUnitSlots(unitSlots);
    }

    public void SetUnitIndices(IReadOnlyList<int> source)
    {
        // [JC 260511] Unity serialization can restore this field as null.
        if (unitIndices == null) unitIndices = new List<int>();
        unitIndices.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
            unitIndices.Add(source[i]);
    }

    public void SetUnitSlots(IReadOnlyList<int> source)
    {
        if (unitSlots == null) unitSlots = new List<int>();
        unitSlots.Clear();

        if (unitIndices == null)
            unitIndices = new List<int>();

        if (source != null)
        {
            for (int i = 0; i < source.Count && i < unitIndices.Count; i++)
                unitSlots.Add(Math.Max(1, source[i]));
        }

        for (int i = unitSlots.Count; i < unitIndices.Count; i++)
            unitSlots.Add(i + 1);
    }
}
