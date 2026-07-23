using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnPointId;
    [SerializeField] private string zoneId;
    [SerializeField] private string enemyGroupKey;
    [SerializeField, Min(0)] private int spawnChatZoneId;
    [SerializeField, Min(0)] private int spawnChatId;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField, Min(1)] private int resolvedEnemyLevel = 1;

    private bool registered;

    public string SpawnPointId => MapProgressKey.NormalizeSegment(spawnPointId);
    public string ZoneId => MapProgressKey.NormalizeSegment(zoneId);
    public string EnemyGroupKey => string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
    public int SpawnChatZoneId => Mathf.Max(0, spawnChatZoneId);
    public int SpawnChatId => Mathf.Max(0, spawnChatId);
    public Vector2Int GridPosition => gridPosition;
    public int ResolvedEnemyLevel => Mathf.Max(1, resolvedEnemyLevel);

    private void OnValidate()
    {
        spawnChatZoneId = Mathf.Max(0, spawnChatZoneId);
        spawnChatId = Mathf.Max(0, spawnChatId);
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
        Initialize(nextZoneId, nextGridPosition, string.Empty);
    }

    public void Initialize(string nextZoneId, Vector2Int nextGridPosition, string nextEnemyGroupKey)
    {
        Initialize(nextZoneId, nextGridPosition, nextEnemyGroupKey, 0, 0);
    }

    public void Initialize(
        string nextZoneId,
        Vector2Int nextGridPosition,
        string nextEnemyGroupKey,
        int nextSpawnChatZoneId,
        int nextSpawnChatId)
    {
        zoneId = MapProgressKey.NormalizeSegment(nextZoneId);
        enemyGroupKey = string.IsNullOrWhiteSpace(nextEnemyGroupKey) ? string.Empty : nextEnemyGroupKey.Trim();
        spawnChatZoneId = Mathf.Max(0, nextSpawnChatZoneId);
        spawnChatId = Mathf.Max(0, nextSpawnChatId);
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
