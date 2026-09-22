using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialEnemyObject : MonoBehaviour, IGridEnemyObject
{
    private static readonly Vector2Int[] AdjacentOffsets =
    {
        new Vector2Int(-1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(-1, 1),
        new Vector2Int(0, 1),
        new Vector2Int(1, 1)
    };

    [Header("Identity")]
    [SerializeField] private string objectKey;
    [SerializeField] private string enemyGroupKey;
    [SerializeField, Min(1)] private int enemyLevel = 1;

    [Header("Tutorial Flow")]
    [Tooltip("이 적을 만졌을 때 실행할 튜토리얼 수업 키(예: TUT_01). 비우면 전투 씬 override로 폴백.")]
    [SerializeField] private string tutorialBattleKey;
    [Tooltip("튜토리얼 flow/UI 해석에 쓸 존 ID. -1이면 레지스트리 와일드카드.")]
    [SerializeField, Min(-1)] private int tutorialZoneId = -1;

    [Header("Interaction")]
    [SerializeField] private bool disableWhenObjectMarkedInactive = true;

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private TutorialEnemyRegistry enemyRegistry;
    [SerializeField] private TutorialCombatLauncher combatLauncher;
    [SerializeField] private InteractionCellOverlayController overlayController;

    [Header("Overlay")]
    [SerializeField] private bool showInteractionOverlay = true;
    [SerializeField] private Color overlayColor = new Color(1f, 0.15f, 0.15f, 0.28f);

    private bool combatStarting;
    private readonly List<Vector2Int> interactionCellBuffer = new List<Vector2Int>(8);

    public string ObjectKey => ResolveObjectKey();
    public string EnemyGroupKey => string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
    public int EnemyLevel => Mathf.Max(1, enemyLevel);
    public string TutorialBattleKey => string.IsNullOrWhiteSpace(tutorialBattleKey) ? string.Empty : tutorialBattleKey.Trim();
    public int TutorialZoneId => tutorialZoneId;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (disableWhenObjectMarkedInactive &&
            TutorialProgressRepository.EnsureInstance()?.IsObjectInactive(ObjectKey) == true)
        {
            gameObject.SetActive(false);
            return;
        }

        enemyRegistry?.Register(this);
        RefreshInteractionOverlay();
    }

    private void OnDisable()
    {
        ClearInteractionOverlay();
        enemyRegistry?.Unregister(this);
        combatStarting = false;
    }

    private void OnValidate()
    {
        enemyLevel = Mathf.Max(1, enemyLevel);
    }

    public Vector2Int GetCurrentGrid()
    {
        ResolveGridManager();
        return gridManager != null ? gridManager.WorldToGrid(transform.position) : Vector2Int.zero;
    }

    public bool IsInteractionCell(Vector2Int grid)
    {
        Vector2Int currentGrid = GetCurrentGrid();
        for (int i = 0; i < AdjacentOffsets.Length; i++)
        {
            if (currentGrid + AdjacentOffsets[i] == grid)
                return true;
        }

        return false;
    }

    public void GetInteractionCells(List<Vector2Int> results)
    {
        if (results == null)
            return;

        results.Clear();
        Vector2Int currentGrid = GetCurrentGrid();
        for (int i = 0; i < AdjacentOffsets.Length; i++)
            results.Add(currentGrid + AdjacentOffsets[i]);
    }

    public bool TryStartTutorialCombat()
    {
        if (combatStarting)
            return false;

        if (string.IsNullOrWhiteSpace(EnemyGroupKey))
        {
            Debug.LogWarning("[TutorialEnemyObject] EnemyGroupKey is empty.", this);
            return false;
        }

        ResolveCombatLauncher();
        if (combatLauncher == null)
        {
            Debug.LogWarning("[TutorialEnemyObject] TutorialCombatLauncher is missing.", this);
            return false;
        }

        combatStarting = true;
        bool started = combatLauncher.BeginCombat(EnemyGroupKey, EnemyLevel, TutorialBattleKey, TutorialZoneId);
        if (!started)
            combatStarting = false;

        return started;
    }

    public void MarkInactiveAndHide()
    {
        TutorialProgressRepository.EnsureInstance()?.MarkObjectInactive(ObjectKey);
        gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        ResolveGridManager();
        ResolveRegistry();
        ResolveCombatLauncher();
        ResolveOverlayController();
    }

    private void ResolveGridManager()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
    }

    private void ResolveRegistry()
    {
        if (enemyRegistry == null)
            enemyRegistry = TutorialEnemyRegistry.EnsureSceneRegistry();
    }

    private void ResolveCombatLauncher()
    {
        if (combatLauncher == null)
            combatLauncher = FindFirstObjectByType<TutorialCombatLauncher>();
    }

    private void ResolveOverlayController()
    {
        if (overlayController == null)
            overlayController = FindFirstObjectByType<InteractionCellOverlayController>();
    }

    private void RefreshInteractionOverlay()
    {
        if (!showInteractionOverlay)
        {
            ClearInteractionOverlay();
            return;
        }

        ResolveOverlayController();
        if (overlayController == null)
            return;

        GetInteractionCells(interactionCellBuffer);
        overlayController.SetExternalCells(this, interactionCellBuffer, overlayColor);
    }

    private void ClearInteractionOverlay()
    {
        if (overlayController != null)
            overlayController.ClearExternalCells(this);
    }

    private string ResolveObjectKey()
    {
        if (!string.IsNullOrWhiteSpace(objectKey))
            return objectKey.Trim();

        Vector2Int grid = GetCurrentGrid();
        return $"tutorial_enemy:{grid.x}_{grid.y}";
    }
}
