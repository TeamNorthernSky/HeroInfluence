using UnityEngine;

[DisallowMultipleComponent]
public class MobileEnemyRaidController : MonoBehaviour
{
    public static MobileEnemyRaidController Instance { get; private set; }

    [Header("Raid Settings")]
    [SerializeField, Min(0)] private int moneyPenalty = 500;
    [SerializeField] private bool destroyEnemyAfterRaid = true;

    [Header("References")]
    [SerializeField] private GridManager gridManager;

    public int MoneyPenalty => Mathf.Max(0, moneyPenalty);

    private void Awake()
    {
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        ResolveReferences();
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    public static MobileEnemyRaidController EnsureSceneInstance()
    {
        if (Instance != null)
            return Instance;

        MobileEnemyRaidController existing = FindFirstObjectByType<MobileEnemyRaidController>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        if (!Application.isPlaying)
            return null;

        GameObject created = new GameObject(nameof(MobileEnemyRaidController));
        Instance = created.AddComponent<MobileEnemyRaidController>();
        return Instance;
    }

    public bool TryHandleRaid(EnemyGridMover enemy)
    {
        if (enemy == null ||
            enemy.CurrentTargetType != EnemyTargetType.HeroUnion ||
            enemy.CurrentTarget is not HeroUnionUnit heroUnion ||
            !heroUnion.IsClaimedByHero)
        {
            return false;
        }

        ResolveReferences();
        if (gridManager == null || !gridManager.IsAdjacentToHeroUnion(enemy.GetCurrentGrid(), heroUnion))
            return false;

        ApplyMoneyPenalty();
        RemoveEnemy(enemy);
        return true;
    }

    private void ApplyMoneyPenalty()
    {
        if (moneyPenalty <= 0)
            return;

        EconomyManager economy = GameManager.Instance != null ? GameManager.Instance.Economy : null;
        if (economy == null)
            return;

        int currentMoney = economy.Get(ResourceType.Money);
        economy.Set(ResourceType.Money, Mathf.Max(0, currentMoney - moneyPenalty));
    }

    private void RemoveEnemy(EnemyGridMover enemy)
    {
        if (enemy == null)
            return;

        string placementKey = ResolveEnemyPlacementKey(enemy);
        if (!string.IsNullOrWhiteSpace(placementKey))
        {
            MapProgressRepository.Instance?.MarkEnemyDefeated(placementKey);
            GateThreatController.NotifyEnemyDefeated(placementKey);
        }

        enemy.ClearTarget();

        if (destroyEnemyAfterRaid)
            Destroy(enemy.gameObject);
        else
            enemy.gameObject.SetActive(false);
    }

    private static string ResolveEnemyPlacementKey(EnemyGridMover enemy)
    {
        EnemyIdentity identity = enemy != null ? enemy.GetComponent<EnemyIdentity>() : null;
        if (identity != null && !string.IsNullOrWhiteSpace(identity.PlacementKey))
            return identity.PlacementKey;

        return enemy != null ? MapProgressKey.ForSceneEnemy(enemy.GetCurrentGrid()) : string.Empty;
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
    }
}
