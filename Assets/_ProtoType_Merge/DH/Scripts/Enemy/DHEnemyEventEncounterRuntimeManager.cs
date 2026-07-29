using UnityEngine;

[DisallowMultipleComponent]
public sealed class DHEnemyEventEncounterRuntimeManager : MonoBehaviour
{
    private const string RootName = "[DH_EnemyEventEncounterRuntime]";

    public static DHEnemyEventEncounterRuntimeManager Instance { get; private set; }

    private string pendingPlacementKey;
    private string pendingBattleKey;
    private bool pendingEncounterConsumed;
    private string completedVictoryPlacementKey;
    private ChatManager subscribedChatManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static DHEnemyEventEncounterRuntimeManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject root = new GameObject(RootName);
        DontDestroyOnLoad(root);
        return root.AddComponent<DHEnemyEventEncounterRuntimeManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDisable()
    {
        UnsubscribeChatEnded();
    }

    public void BeginPendingEncounter(EnemyEventEncounterBinding binding)
    {
        if (binding == null)
            return;

        pendingPlacementKey = binding.SourcePlacementKey;
        pendingBattleKey = binding.EventBattleKey;
        pendingEncounterConsumed = false;
    }

    public bool TryConsumePendingBattleSource(string battleKey, out string placementKey)
    {
        placementKey = string.Empty;
        if (string.IsNullOrWhiteSpace(pendingPlacementKey))
            return false;

        string normalizedBattleKey = NormalizeKey(battleKey);
        if (!string.IsNullOrEmpty(pendingBattleKey) &&
            !string.Equals(pendingBattleKey, normalizedBattleKey, System.StringComparison.Ordinal))
        {
            return false;
        }

        placementKey = pendingPlacementKey;
        pendingEncounterConsumed = true;
        pendingPlacementKey = string.Empty;
        pendingBattleKey = string.Empty;
        return true;
    }

    public void ClearPendingEncounterIfUnused()
    {
        if (pendingEncounterConsumed)
            return;

        pendingPlacementKey = string.Empty;
        pendingBattleKey = string.Empty;
    }

    public void RegisterCompletedEventBattle(CombatContext context)
    {
        if (context == null || context.EventBattle == null)
            return;

        if (context.Result != CombatResult.Victory)
            return;

        string placementKey = MapProgressKey.NormalizeSegment(context.EventBattle.SourceEnemyPlacementKey);
        if (string.IsNullOrWhiteSpace(placementKey))
            return;

        completedVictoryPlacementKey = placementKey;
        SubscribeChatEnded();
    }

    private void SubscribeChatEnded()
    {
        ChatManager manager = ChatManager.Instance;
        if (manager == null)
        {
            CompleteVictoryEnemy();
            return;
        }

        if (subscribedChatManager == manager)
            return;

        UnsubscribeChatEnded();
        subscribedChatManager = manager;
        subscribedChatManager.OnChatEnded += HandleChatEnded;
    }

    private void UnsubscribeChatEnded()
    {
        if (subscribedChatManager == null)
            return;

        subscribedChatManager.OnChatEnded -= HandleChatEnded;
        subscribedChatManager = null;
    }

    private void HandleChatEnded()
    {
        CompleteVictoryEnemy();
    }

    private void CompleteVictoryEnemy()
    {
        string placementKey = completedVictoryPlacementKey;
        completedVictoryPlacementKey = string.Empty;
        UnsubscribeChatEnded();

        if (string.IsNullOrWhiteSpace(placementKey))
            return;

        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        progressRepository?.MarkEnemyDefeated(placementKey);
        GateThreatController.NotifyEnemyDefeated(placementKey);
        DestroyMatchingSceneEnemy(placementKey);
    }

    private static void DestroyMatchingSceneEnemy(string placementKey)
    {
        string normalizedPlacementKey = MapProgressKey.NormalizeSegment(placementKey);
        EnemyGridMover[] enemies = FindObjectsByType<EnemyGridMover>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyGridMover enemy = enemies[i];
            if (enemy == null)
                continue;

            EnemyIdentity identity = enemy.GetComponent<EnemyIdentity>();
            if (identity == null ||
                !string.Equals(
                    MapProgressKey.NormalizeSegment(identity.PlacementKey),
                    normalizedPlacementKey,
                    System.StringComparison.Ordinal))
            {
                continue;
            }

            Destroy(enemy.gameObject);
            return;
        }
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
