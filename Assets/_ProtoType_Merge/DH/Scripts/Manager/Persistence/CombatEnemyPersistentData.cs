using System;
using System.Collections.Generic;

[Serializable]
public class CombatEnemyPersistentData
{
    public string EnemyId => enemyId;
    public string PlacementKey => placementKey;
    public IReadOnlyList<int> UnitIndices => unitIndices ?? (unitIndices = new List<int>());

    [UnityEngine.SerializeField] private string enemyId;
    [UnityEngine.SerializeField] private string placementKey;
    [UnityEngine.SerializeField] private List<int> unitIndices = new List<int>();

    public CombatEnemyPersistentData(string enemyId, IReadOnlyList<int> unitIndices)
        : this(enemyId, string.Empty, unitIndices)
    {
    }

    public CombatEnemyPersistentData(string enemyId, string placementKey, IReadOnlyList<int> unitIndices)
    {
        this.enemyId = string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId;
        this.placementKey = string.IsNullOrWhiteSpace(placementKey) ? string.Empty : placementKey;
        SetUnitIndices(unitIndices);
    }

    public void SetEnemyId(string nextEnemyId)
    {
        enemyId = string.IsNullOrWhiteSpace(nextEnemyId) ? string.Empty : nextEnemyId;
    }

    public void SetPlacementKey(string nextPlacementKey)
    {
        placementKey = string.IsNullOrWhiteSpace(nextPlacementKey) ? string.Empty : nextPlacementKey;
    }

    public void SetUnitIndices(IReadOnlyList<int> source)
    {
        EnsureInitialized();
        unitIndices.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] > 0)
                unitIndices.Add(source[i]);
        }
    }

    private void EnsureInitialized()
    {
        if (unitIndices == null)
            unitIndices = new List<int>();
    }
}
