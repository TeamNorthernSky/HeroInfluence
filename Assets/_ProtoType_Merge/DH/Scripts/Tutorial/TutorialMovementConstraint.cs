using System.Collections.Generic;
using UnityEngine;

public class TutorialMovementConstraint : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private bool constraintEnabled = true;

    [Header("Allowed Targets")]
    [SerializeField] private bool allowAllWhenListEmpty = true;
    [SerializeField] private bool allowCurrentPartyCell = true;
    [SerializeField] private List<Vector2Int> allowedTargetCells = new List<Vector2Int>();

    private static TutorialMovementConstraint instance;
    private readonly HashSet<Vector2Int> allowedTargetLookup = new HashSet<Vector2Int>();
    private bool lookupDirty = true;

    public static bool IsActive =>
        instance != null &&
        instance.isActiveAndEnabled &&
        instance.constraintEnabled;

    public bool ConstraintEnabled
    {
        get => constraintEnabled;
        set => constraintEnabled = value;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[TutorialMovementConstraint] Multiple constraints were found. The latest one will be used.", this);
        }

        instance = this;
        RebuildLookup();
    }

    private void OnEnable()
    {
        instance = this;
        MarkLookupDirty();
    }

    private void OnDisable()
    {
        if (instance == this)
            instance = null;
    }

    private void OnValidate()
    {
        MarkLookupDirty();
    }

    public static bool IsTargetAllowed(Vector2Int targetCell, PartyGridMover party)
    {
        if (!IsActive)
            return true;

        return instance.IsAllowed(targetCell, party);
    }

    public void SetAllowedTargets(IEnumerable<Vector2Int> cells)
    {
        allowedTargetCells.Clear();
        if (cells != null)
        {
            foreach (Vector2Int cell in cells)
                allowedTargetCells.Add(cell);
        }

        RebuildLookup();
    }

    public void SetSingleAllowedTarget(Vector2Int cell)
    {
        allowedTargetCells.Clear();
        allowedTargetCells.Add(cell);
        RebuildLookup();
    }

    public void ClearAllowedTargets()
    {
        allowedTargetCells.Clear();
        RebuildLookup();
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
        for (int i = 0; i < allowedTargetCells.Count; i++)
            allowedTargetLookup.Add(allowedTargetCells[i]);

        lookupDirty = false;
    }
}
