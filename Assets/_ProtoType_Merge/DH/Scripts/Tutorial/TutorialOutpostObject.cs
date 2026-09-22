using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(TutorialBuildingObject))]
public sealed class TutorialOutpostObject : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string objectKey;

    [Header("State")]
    [SerializeField] private TutorialOutpostClaimState initialClaimState = TutorialOutpostClaimState.EnemyClaimed;

    [Header("Combat")]
    [SerializeField] private string enemyGroupKey;
    [SerializeField, Min(1)] private int enemyLevel = 1;
    [Tooltip("Tutorial battle flow key used by the tutorial battle scene, such as TUT_01.")]
    [SerializeField] private string tutorialBattleKey;
    [Tooltip("Tutorial flow zone id. -1 lets the battle side use its default.")]
    [SerializeField, Min(-1)] private int tutorialZoneId = -1;

    [Header("Tutorial Lobby")]
    [SerializeField] private string tutorialLobbySceneName = "TutorialLobbyScene";

    [Header("Visual")]
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private Material enemyClaimedMaterial;
    [SerializeField] private Material heroClaimedMaterial;

    [Header("Overlay")]
    [SerializeField] private Color enemyClaimedOverlayColor = new Color(1f, 0.15f, 0.15f, 0.28f);
    [SerializeField] private Color heroClaimedOverlayColor = new Color(0f, 0.35f, 1f, 0.28f);

    [Header("References")]
    [SerializeField] private TutorialBuildingObject buildingObject;
    [SerializeField] private TutorialCombatLauncher combatLauncher;

    private TutorialOutpostClaimState currentClaimState;
    private bool combatStarting;

    public string ObjectKey => ResolveObjectKey();
    public TutorialOutpostClaimState CurrentClaimState => currentClaimState;
    public string EnemyGroupKey => string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
    public int EnemyLevel => Mathf.Max(1, enemyLevel);
    public string TutorialBattleKey => string.IsNullOrWhiteSpace(tutorialBattleKey) ? string.Empty : tutorialBattleKey.Trim();
    public int TutorialZoneId => tutorialZoneId;

    private void Awake()
    {
        ResolveReferences();
        LoadStateFromRepository();
        ApplyStateVisuals();
    }

    private void OnEnable()
    {
        ResolveReferences();
        LoadStateFromRepository();
        ApplyStateVisuals();
    }

    private void OnDisable()
    {
        combatStarting = false;
    }

    private void OnValidate()
    {
        enemyLevel = Mathf.Max(1, enemyLevel);
        ResolveReferences();
    }

    public void SetClaimState(TutorialOutpostClaimState nextState, bool persist)
    {
        if (currentClaimState == nextState && !persist)
            return;

        currentClaimState = nextState;
        if (persist)
            TutorialProgressRepository.EnsureInstance()?.SetOutpostState(ObjectKey, nextState);

        ApplyStateVisuals();
    }

    public bool TryInteract()
    {
        LoadStateFromRepository();

        if (currentClaimState == TutorialOutpostClaimState.HeroClaimed)
            return false;

        return TryStartCaptureCombat();
    }

    public void RefreshStateVisuals()
    {
        LoadStateFromRepository();
        ApplyStateVisuals();
    }

    public bool IsInteractionCell(Vector2Int grid)
    {
        ResolveReferences();
        return buildingObject != null && buildingObject.IsInteractionCell(grid);
    }

    public bool TryStartCaptureCombat()
    {
        LoadStateFromRepository();

        if (combatStarting)
            return false;

        if (currentClaimState != TutorialOutpostClaimState.EnemyClaimed)
            return false;

        if (string.IsNullOrWhiteSpace(EnemyGroupKey))
        {
            Debug.LogWarning("[TutorialOutpostObject] EnemyGroupKey is empty.", this);
            return false;
        }

        ResolveCombatLauncher();
        if (combatLauncher == null)
        {
            Debug.LogWarning("[TutorialOutpostObject] TutorialCombatLauncher is missing.", this);
            return false;
        }

        TutorialProgressRepository repository = TutorialProgressRepository.EnsureInstance();
        repository?.SetPendingCombatSource(TutorialCombatSourceType.Outpost, ObjectKey);

        combatStarting = true;
        bool started = combatLauncher.BeginCombat(EnemyGroupKey, EnemyLevel, TutorialBattleKey, TutorialZoneId);
        if (!started)
        {
            repository?.ClearPendingCombatSource();
            combatStarting = false;
        }

        return started;
    }

    public bool TryEnterTutorialLobbyScene()
    {
        if (string.IsNullOrWhiteSpace(tutorialLobbySceneName))
        {
            Debug.LogWarning("[TutorialOutpostObject] TutorialLobbySceneName is empty.", this);
            return false;
        }

        string sceneName = tutorialLobbySceneName.Trim();
        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);

        return true;
    }

    private void LoadStateFromRepository()
    {
        currentClaimState = initialClaimState;
        if (!Application.isPlaying)
            return;

        TutorialProgressRepository repository = TutorialProgressRepository.EnsureInstance();
        if (repository != null &&
            repository.TryGetOutpostState(ObjectKey, out TutorialOutpostClaimState savedState))
        {
            currentClaimState = savedState;
        }
    }

    private void ApplyStateVisuals()
    {
        ApplyMaterial();
        ApplyOverlayColor();
    }

    private void ApplyMaterial()
    {
        Material material = currentClaimState == TutorialOutpostClaimState.HeroClaimed
            ? heroClaimedMaterial
            : enemyClaimedMaterial;

        if (material == null)
            return;

        EnsureRenderersCached();
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer renderer = targetRenderers[i];
            if (renderer == null)
                continue;

            renderer.sharedMaterial = material;
        }
    }

    private void ApplyOverlayColor()
    {
        if (buildingObject == null)
            return;

        Color color = currentClaimState == TutorialOutpostClaimState.HeroClaimed
            ? heroClaimedOverlayColor
            : enemyClaimedOverlayColor;

        buildingObject.SetInteractionOverlayColor(color);
    }

    private void ResolveReferences()
    {
        if (buildingObject == null)
            buildingObject = GetComponent<TutorialBuildingObject>();

        ResolveCombatLauncher();
    }

    private void ResolveCombatLauncher()
    {
        if (combatLauncher == null)
            combatLauncher = TutorialCombatLauncher.EnsureSceneLauncher();
    }

    private void EnsureRenderersCached()
    {
        if (targetRenderers != null && targetRenderers.Length > 0)
            return;

        targetRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private string ResolveObjectKey()
    {
        if (!string.IsNullOrWhiteSpace(objectKey))
            return objectKey.Trim();

        if (buildingObject != null && !string.IsNullOrWhiteSpace(buildingObject.BuildingKey))
            return buildingObject.BuildingKey;

        Vector2Int grid = buildingObject != null ? buildingObject.GetAnchorGrid() : Vector2Int.zero;
        return $"tutorial_outpost:{grid.x}_{grid.y}";
    }
}
