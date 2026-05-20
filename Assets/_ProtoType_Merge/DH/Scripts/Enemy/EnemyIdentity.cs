using UnityEngine;

public enum EnemyPlacementSource
{
    Scene,
    Runtime
}

[DisallowMultipleComponent]
public class EnemyIdentity : MonoBehaviour
{
    [SerializeField] private string enemyId;
    [SerializeField] private string placementKey;
    [SerializeField] private EnemyPlacementSource placementSource = EnemyPlacementSource.Scene;

    public string EnemyId => enemyId;
    public string PlacementKey => placementKey;
    public EnemyPlacementSource PlacementSource => placementSource;

    public void SetEnemyId(string nextEnemyId)
    {
        if (string.IsNullOrWhiteSpace(nextEnemyId))
            return;

        enemyId = nextEnemyId;
    }

    public void SetPlacementKey(string nextPlacementKey)
    {
        if (string.IsNullOrWhiteSpace(nextPlacementKey))
            return;

        placementKey = nextPlacementKey;
    }

    public void SetPlacementSource(EnemyPlacementSource nextPlacementSource)
    {
        placementSource = nextPlacementSource;
    }
}
