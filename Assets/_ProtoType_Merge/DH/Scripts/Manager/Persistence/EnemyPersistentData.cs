using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

[Serializable]
public class EnemyPersistentData
{
    public string EnemyId => enemyId;
    public IReadOnlyList<int> UnitIndices => unitIndices;

    [UnityEngine.SerializeField] private string enemyId;
    [FormerlySerializedAs("combatUnitIndices")]
    [UnityEngine.SerializeField] private List<int> unitIndices = new List<int>();

    public EnemyPersistentData(string enemyId, IReadOnlyList<int> unitIndices)
    {
        this.enemyId = string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId;
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
