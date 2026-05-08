using System;
using System.Collections.Generic;

[Serializable]
public class CombatEnemyPersistentData
{
    public string EnemyId => enemyId;
    public IReadOnlyList<int> UnitIndices => unitIndices;

    [UnityEngine.SerializeField] private string enemyId;
    [UnityEngine.SerializeField] private List<int> unitIndices = new List<int>();

    public CombatEnemyPersistentData(string enemyId, IReadOnlyList<int> unitIndices)
    {
        this.enemyId = string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId;
        SetUnitIndices(unitIndices);
    }

    public void SetEnemyId(string nextEnemyId)
    {
        enemyId = string.IsNullOrWhiteSpace(nextEnemyId) ? string.Empty : nextEnemyId;
    }

    public void SetUnitIndices(IReadOnlyList<int> source)
    {
        unitIndices.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] > 0)
                unitIndices.Add(source[i]);
        }
    }
}
