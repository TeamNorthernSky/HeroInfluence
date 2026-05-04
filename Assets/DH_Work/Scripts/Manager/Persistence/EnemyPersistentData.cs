using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

[Serializable]
public class EnemyPersistentData
{
    public int EnemyId => enemyId;
    public IReadOnlyList<int> UnitIndices => unitIndices;

    [UnityEngine.SerializeField] private int enemyId;
    [FormerlySerializedAs("combatUnitIndices")]
    [UnityEngine.SerializeField] private List<int> unitIndices = new List<int>();

    public EnemyPersistentData(int enemyId, IReadOnlyList<int> unitIndices)
    {
        this.enemyId = Math.Max(1, enemyId);
        SetUnitIndices(unitIndices);
    }

    public void SetUnitIndices(IReadOnlyList<int> source)
    {
        unitIndices.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
            unitIndices.Add(source[i]);
    }
}
