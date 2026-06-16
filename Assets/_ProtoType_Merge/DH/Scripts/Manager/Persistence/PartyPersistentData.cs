using System;
using System.Collections.Generic;

[Serializable]
public class PartyPersistentData
{
    public string PartyId => partyId;
    public IReadOnlyList<int> UnitIndices => unitIndices;
    public IReadOnlyList<int> UnitSlots => unitSlots;

    [UnityEngine.SerializeField] private string partyId;
    [UnityEngine.SerializeField] private List<int> unitIndices = new List<int>();
    [UnityEngine.SerializeField] private List<int> unitSlots = new List<int>();

    // [JC 260511] Keeps runtime party grid/move state stable across DHScene reloads.
    [UnityEngine.SerializeField] private UnityEngine.Vector2Int lastGrid;
    [UnityEngine.SerializeField] private bool hasLastGrid;
    [UnityEngine.SerializeField] private int remainingMovePoints;
    [UnityEngine.SerializeField] private bool hasRemainingMovePoints;
    public UnityEngine.Vector2Int LastGrid => lastGrid;
    public bool HasLastGrid => hasLastGrid;
    public int RemainingMovePoints => remainingMovePoints;
    public bool HasRemainingMovePoints => hasRemainingMovePoints;

    public void SetLastGrid(UnityEngine.Vector2Int grid)
    {
        lastGrid = grid;
        hasLastGrid = true;
    }

    public void ClearLastGrid()
    {
        lastGrid = default;
        hasLastGrid = false;
    }

    public void SetRemainingMovePoints(int value)
    {
        remainingMovePoints = Math.Max(0, value);
        hasRemainingMovePoints = true;
    }

    public void ClearRemainingMovePoints()
    {
        remainingMovePoints = 0;
        hasRemainingMovePoints = false;
    }

    public PartyPersistentData(string partyId, IReadOnlyList<int> unitIndices)
        : this(partyId, unitIndices, null)
    {
    }

    public PartyPersistentData(string partyId, IReadOnlyList<int> unitIndices, IReadOnlyList<int> unitSlots)
    {
        this.partyId = partyId ?? string.Empty;
        SetUnitIndices(unitIndices);
        SetUnitSlots(unitSlots);
    }

    public void SetUnitIndices(IReadOnlyList<int> source)
    {
        // [JC 260511] Unity serialization can restore this field as null.
        if (unitIndices == null) unitIndices = new List<int>();
        unitIndices.Clear();

        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
            unitIndices.Add(source[i]);
    }

    public void SetUnitSlots(IReadOnlyList<int> source)
    {
        if (unitSlots == null) unitSlots = new List<int>();
        unitSlots.Clear();

        if (unitIndices == null)
            unitIndices = new List<int>();

        if (source != null)
        {
            for (int i = 0; i < source.Count && i < unitIndices.Count; i++)
                unitSlots.Add(Math.Max(1, source[i]));
        }

        for (int i = unitSlots.Count; i < unitIndices.Count; i++)
            unitSlots.Add(i + 1);
    }
}
