using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnPointId;
    [SerializeField] private string zoneId;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField, Min(1)] private int resolvedEnemyLevel = 1;

    private bool registered;

    public string SpawnPointId => MapProgressKey.NormalizeSegment(spawnPointId);
    public string ZoneId => MapProgressKey.NormalizeSegment(zoneId);
    public Vector2Int GridPosition => gridPosition;
    public int ResolvedEnemyLevel => Mathf.Max(1, resolvedEnemyLevel);

    private void OnValidate()
    {
        RefreshResolvedEnemyLevel();
    }
    private void OnEnable()
    {
        RefreshResolvedEnemyLevel();
        TryRegister();
    }

    private void OnDisable()
    {
        if (!registered || GateThreatController.Instance == null)
            return;

        GateThreatController.Instance.UnregisterSpawnPoint(this);
        registered = false;
    }

    public void Initialize(string nextZoneId, Vector2Int nextGridPosition)
    {
        zoneId = MapProgressKey.NormalizeSegment(nextZoneId);
        gridPosition = nextGridPosition;
        spawnPointId = $"{zoneId}_{gridPosition.x}_{gridPosition.y}";
        RefreshResolvedEnemyLevel();
        TryRegister();
    }

    public void RefreshResolvedEnemyLevel()
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        resolvedEnemyLevel = repository != null && repository.TryGetZoneEnemyLevel(ZoneId, out int level)
            ? Mathf.Max(1, level)
            : 1;
    }

    private void TryRegister()
    {
        if (registered || string.IsNullOrWhiteSpace(ZoneId))
            return;

        GateThreatController controller = GateThreatController.EnsureSceneInstance();
        if (controller == null)
            return;

        controller.RegisterSpawnPoint(this);
        registered = true;
    }
}
