using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnPointId;
    [SerializeField] private string zoneId;
    [SerializeField] private Vector2Int gridPosition;

    private bool registered;

    public string SpawnPointId => MapProgressKey.NormalizeSegment(spawnPointId);
    public string ZoneId => MapProgressKey.NormalizeSegment(zoneId);
    public Vector2Int GridPosition => gridPosition;

    private void OnEnable()
    {
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
        TryRegister();
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
