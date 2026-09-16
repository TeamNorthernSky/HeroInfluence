using System;

public enum CombatEnemySourceType
{
    None,
    Field,
    OutpostDefender,
    VillainUnionDefender,
    Event,
    Simulation,
    Tutorial
}

[Serializable]
public class CombatEnemyPersistentData
{
    public string EnemyId => enemyId;
    public string PlacementKey => placementKey;
    public string EnemyGroupKey => string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
    public CombatEnemySourceType SourceType => sourceType;

    [UnityEngine.SerializeField] private string enemyId;
    [UnityEngine.SerializeField] private string placementKey;
    [UnityEngine.SerializeField] private string enemyGroupKey;
    [UnityEngine.SerializeField] private CombatEnemySourceType sourceType;

    public CombatEnemyPersistentData(
        string enemyId,
        string placementKey,
        string enemyGroupKey,
        CombatEnemySourceType sourceType)
    {
        this.enemyId = string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId;
        this.placementKey = string.IsNullOrWhiteSpace(placementKey) ? string.Empty : placementKey;
        this.enemyGroupKey = string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
        this.sourceType = sourceType;
    }

    public void SetEnemyId(string nextEnemyId)
    {
        enemyId = string.IsNullOrWhiteSpace(nextEnemyId) ? string.Empty : nextEnemyId;
    }

    public void SetPlacementKey(string nextPlacementKey)
    {
        placementKey = string.IsNullOrWhiteSpace(nextPlacementKey) ? string.Empty : nextPlacementKey;
    }

    public void SetEnemyGroupKey(string nextEnemyGroupKey)
    {
        enemyGroupKey = string.IsNullOrWhiteSpace(nextEnemyGroupKey) ? string.Empty : nextEnemyGroupKey.Trim();
    }

    public void SetSourceType(CombatEnemySourceType nextSourceType)
    {
        sourceType = nextSourceType;
    }
}
