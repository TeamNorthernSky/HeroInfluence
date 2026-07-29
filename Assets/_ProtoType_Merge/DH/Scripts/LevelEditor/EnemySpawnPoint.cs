using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnPointId;
    [SerializeField] private string zoneId;
    [SerializeField] private string enemyGroupKey;
    [SerializeField, Min(0)] private int spawnChatZoneId;
    [SerializeField, Min(0)] private int spawnChatId;
    [SerializeField, Min(0)] private int encounterChatZoneId;
    [SerializeField, Min(0)] private int encounterChatId;
    [SerializeField] private string eventBattleKey;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField, Min(1)] private int resolvedEnemyLevel = 1;

    private bool registered;

    public string SpawnPointId => MapProgressKey.NormalizeSegment(spawnPointId);
    public string ZoneId => NormalizeSpawnZoneId(zoneId);
    public string EnemyGroupKey => string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
    public int SpawnChatZoneId => ResolveZoneNumber(ZoneId);
    public int SpawnChatId => Mathf.Max(0, spawnChatId);
    public int EncounterChatZoneId => ResolveZoneNumber(ZoneId);
    public int EncounterChatId => Mathf.Max(0, encounterChatId);
    public string EventBattleKey => string.IsNullOrWhiteSpace(eventBattleKey) ? string.Empty : eventBattleKey.Trim();
    public Vector2Int GridPosition => gridPosition;
    public int ResolvedEnemyLevel => Mathf.Max(1, resolvedEnemyLevel);

    private void OnValidate()
    {
        spawnChatZoneId = Mathf.Max(0, spawnChatZoneId);
        spawnChatId = Mathf.Max(0, spawnChatId);
        encounterChatZoneId = Mathf.Max(0, encounterChatZoneId);
        encounterChatId = Mathf.Max(0, encounterChatId);
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
        Initialize(
            nextZoneId,
            nextGridPosition,
            nextEnemyGroupKey,
            nextSpawnChatZoneId,
            nextSpawnChatId,
            0,
            0,
            string.Empty);
    }

    public void Initialize(
        string nextZoneId,
        Vector2Int nextGridPosition,
        string nextEnemyGroupKey,
        int nextSpawnChatZoneId,
        int nextSpawnChatId,
        int nextEncounterChatZoneId,
        int nextEncounterChatId,
        string nextEventBattleKey)
    {
        zoneId = NormalizeSpawnZoneId(nextZoneId);
        enemyGroupKey = string.IsNullOrWhiteSpace(nextEnemyGroupKey) ? string.Empty : nextEnemyGroupKey.Trim();
        spawnChatZoneId = Mathf.Max(0, nextSpawnChatZoneId);
        spawnChatId = Mathf.Max(0, nextSpawnChatId);
        encounterChatZoneId = Mathf.Max(0, nextEncounterChatZoneId);
        encounterChatId = Mathf.Max(0, nextEncounterChatId);
        eventBattleKey = string.IsNullOrWhiteSpace(nextEventBattleKey) ? string.Empty : nextEventBattleKey.Trim();
        gridPosition = nextGridPosition;
        spawnPointId = $"{zoneId}_{gridPosition.x}_{gridPosition.y}";
        RefreshResolvedEnemyLevel();
        TryRegister();
    }

    public static string NormalizeSpawnZoneId(string value)
    {
        string normalized = MapProgressKey.NormalizeSegment(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (int.TryParse(normalized, out int zoneNumber) && zoneNumber > 0)
            return $"zone_{zoneNumber:000}";

        const string prefix = "zone_";
        if (normalized.StartsWith(prefix, System.StringComparison.Ordinal) &&
            int.TryParse(normalized.Substring(prefix.Length), out zoneNumber) &&
            zoneNumber > 0)
        {
            return $"zone_{zoneNumber:000}";
        }

        return normalized;
    }

    public static int ResolveZoneNumber(string value)
    {
        string normalized = NormalizeSpawnZoneId(value);
        const string prefix = "zone_";
        string numberText = normalized.StartsWith(prefix, System.StringComparison.Ordinal)
            ? normalized.Substring(prefix.Length)
            : normalized;

        return int.TryParse(numberText, out int zoneNumber) && zoneNumber > 0 ? zoneNumber : 0;
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
