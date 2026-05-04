using UnityEngine;

[DisallowMultipleComponent]
public class FogSceneReferences : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private AStarPathfinder pathfinder;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private ResourceManager resourceManager;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private KJ_DHCompatibleFogManager fogManager;
    [SerializeField] private PartyGridMover[] partyMovers;

    public Camera MainCamera => mainCamera;
    public GridManager GridManager => gridManager;
    public AStarPathfinder Pathfinder => pathfinder;
    public PartyRegistry PartyRegistry => partyRegistry;
    public ResourceManager ResourceManager => resourceManager;
    public TurnManager TurnManager => turnManager;
    public KJ_DHCompatibleFogManager FogManager => fogManager;
    public PartyGridMover[] PartyMovers => partyMovers;

    public void CollectReferences()
    {
        if ((partyMovers == null || partyMovers.Length == 0) && partyRegistry != null)
            partyMovers = partyRegistry.PartyMovers;

        if (partyMovers == null || partyMovers.Length == 0)
            partyMovers = FindObjectsByType<PartyGridMover>(FindObjectsSortMode.None);

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (gridManager == null)
        {
            var allGridManagers = Resources.FindObjectsOfTypeAll<GridManager>();
            for (int i = 0; i < allGridManagers.Length; i++)
            {
                var candidate = allGridManagers[i];
                if (candidate == null || !candidate.gameObject.scene.IsValid())
                    continue;

                gridManager = candidate;
                break;
            }
        }

        if (pathfinder == null)
            pathfinder = FindFirstObjectByType<AStarPathfinder>();

        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();

        if (resourceManager == null)
            resourceManager = FindFirstObjectByType<ResourceManager>();

        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();

        if (fogManager == null)
            fogManager = FindFirstObjectByType<KJ_DHCompatibleFogManager>(FindObjectsInactive.Include);

        if (fogManager == null)
        {
            var allFogManagers = Resources.FindObjectsOfTypeAll<KJ_DHCompatibleFogManager>();
            for (int i = 0; i < allFogManagers.Length; i++)
            {
                var candidate = allFogManagers[i];
                if (candidate == null || !candidate.gameObject.scene.IsValid())
                    continue;

                fogManager = candidate;
                break;
            }
        }
    }
}
