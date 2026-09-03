using System.Collections.Generic;
using UnityEngine;

public class TutorialMovementConstraint : MonoBehaviour
{
    [System.Serializable]
    public class TutorialMovementStep
    {
        [SerializeField] private int order = 1;
        [SerializeField] private Vector2Int targetCell;

        public TutorialMovementStep()
        {
        }

        public TutorialMovementStep(int order, Vector2Int targetCell)
        {
            this.order = order;
            this.targetCell = targetCell;
        }

        public int Order => order;
        public Vector2Int TargetCell => targetCell;
    }

    [Header("State")]
    [SerializeField] private bool constraintEnabled = true;
    [SerializeField] private int currentOrder = 1;
    [SerializeField] private bool autoAdvanceOnArrive = true;

    [Header("Allowed Targets")]
    [SerializeField] private bool allowAllWhenListEmpty = true;
    [SerializeField] private bool allowCurrentPartyCell = true;
    [SerializeField] private List<TutorialMovementStep> movementSteps = new List<TutorialMovementStep>();

    [Header("Overlay")]
    [SerializeField] private bool showCurrentTargets = true;
    [SerializeField] private Color currentTargetColor = new Color(1f, 0.9f, 0f, 0.42f);
    [SerializeField] private InteractionCellOverlayController overlayController;
    [SerializeField] private PartyRegistry partyRegistry;

    private static TutorialMovementConstraint instance;
    private readonly HashSet<Vector2Int> allowedTargetLookup = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> currentTargetCells = new List<Vector2Int>();
    private bool lookupDirty = true;
    private PartyGridMover subscribedParty;

    public static bool IsActive =>
        instance != null &&
        instance.isActiveAndEnabled &&
        instance.constraintEnabled;

    public bool ConstraintEnabled
    {
        get => constraintEnabled;
        set => constraintEnabled = value;
    }

    public int CurrentOrder => currentOrder;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[TutorialMovementConstraint] Multiple constraints were found. The latest one will be used.", this);
        }

        instance = this;
        ResolveReferences();
        RebuildLookup();
    }

    private void OnEnable()
    {
        instance = this;
        ResolveReferences();
        SubscribeParty();
        MarkLookupDirty();
        RefreshOverlay();
    }

    private void OnDisable()
    {
        UnsubscribeParty();
        ClearOverlay();

        if (instance == this)
            instance = null;
    }

    private void OnValidate()
    {
        MarkLookupDirty();
        RefreshOverlay();
    }

    private void Update()
    {
        if (subscribedParty == null || !subscribedParty.gameObject.activeInHierarchy)
            SubscribeParty();
    }

    public static bool IsTargetAllowed(Vector2Int targetCell, PartyGridMover party)
    {
        if (!IsActive)
            return true;

        return instance.IsAllowed(targetCell, party);
    }

    public void SetCurrentOrder(int order)
    {
        if (currentOrder == order)
            return;

        currentOrder = order;
        RebuildLookup();
        RefreshOverlay();
    }

    public void AdvanceOrder()
    {
        int nextOrder = FindNextOrder(currentOrder);
        if (nextOrder == currentOrder)
            return;

        SetCurrentOrder(nextOrder);
    }

    public void SetAllowedTargets(IEnumerable<Vector2Int> cells)
    {
        movementSteps.Clear();
        if (cells != null)
        {
            foreach (Vector2Int cell in cells)
                AddRuntimeStep(currentOrder, cell);
        }

        RebuildLookup();
        RefreshOverlay();
    }

    public void SetSingleAllowedTarget(Vector2Int cell)
    {
        movementSteps.Clear();
        AddRuntimeStep(currentOrder, cell);
        RebuildLookup();
        RefreshOverlay();
    }

    public void ClearAllowedTargets()
    {
        movementSteps.Clear();
        RebuildLookup();
        RefreshOverlay();
    }

    private bool IsAllowed(Vector2Int targetCell, PartyGridMover party)
    {
        EnsureLookup();

        if (allowCurrentPartyCell && party != null && party.GetCurrentGrid() == targetCell)
            return true;

        if (allowedTargetLookup.Count == 0)
            return allowAllWhenListEmpty;

        return allowedTargetLookup.Contains(targetCell);
    }

    private void MarkLookupDirty()
    {
        lookupDirty = true;
    }

    private void EnsureLookup()
    {
        if (!lookupDirty)
            return;

        RebuildLookup();
    }

    private void RebuildLookup()
    {
        allowedTargetLookup.Clear();
        currentTargetCells.Clear();

        for (int i = 0; i < movementSteps.Count; i++)
        {
            TutorialMovementStep step = movementSteps[i];
            if (step == null || step.Order != currentOrder)
                continue;

            allowedTargetLookup.Add(step.TargetCell);
            currentTargetCells.Add(step.TargetCell);
        }

        lookupDirty = false;
    }

    private void HandlePartyGridEntered(Vector2Int grid)
    {
        if (!autoAdvanceOnArrive || !constraintEnabled)
            return;

        EnsureLookup();
        if (!allowedTargetLookup.Contains(grid))
            return;

        AdvanceOrder();
    }

    private int FindNextOrder(int order)
    {
        int nextOrder = int.MaxValue;
        for (int i = 0; i < movementSteps.Count; i++)
        {
            TutorialMovementStep step = movementSteps[i];
            if (step == null || step.Order <= order)
                continue;

            if (step.Order < nextOrder)
                nextOrder = step.Order;
        }

        return nextOrder == int.MaxValue ? order : nextOrder;
    }

    private void RefreshOverlay()
    {
        if (!Application.isPlaying && !isActiveAndEnabled)
            return;

        ResolveReferences();
        if (overlayController == null)
            return;

        EnsureLookup();
        if (!constraintEnabled || !showCurrentTargets || currentTargetCells.Count == 0)
        {
            ClearOverlay();
            return;
        }

        overlayController.SetExternalCells(this, currentTargetCells, currentTargetColor);
    }

    private void ClearOverlay()
    {
        if (overlayController != null)
            overlayController.ClearExternalCells(this);
    }

    private void ResolveReferences()
    {
        if (overlayController == null)
            overlayController = FindFirstObjectByType<InteractionCellOverlayController>();

        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();
    }

    private void SubscribeParty()
    {
        ResolveReferences();

        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (subscribedParty == party)
            return;

        UnsubscribeParty();
        subscribedParty = party;
        if (subscribedParty != null)
            subscribedParty.GridEntered += HandlePartyGridEntered;
    }

    private void UnsubscribeParty()
    {
        if (subscribedParty != null)
            subscribedParty.GridEntered -= HandlePartyGridEntered;

        subscribedParty = null;
    }

    private void AddRuntimeStep(int order, Vector2Int cell)
    {
        movementSteps.Add(new TutorialMovementStep(order, cell));
    }
}
