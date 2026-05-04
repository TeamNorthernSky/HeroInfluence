using System;
using System.Collections.Generic;
using UnityEngine;

public class FogGridManager : MonoBehaviour
{
    [Header("Fog Rules")]
    [SerializeField, Min(1)] private int refogDelayDays = 3;

    private readonly Dictionary<Vector2Int, FogCellData> fogCells = new Dictionary<Vector2Int, FogCellData>();
    private int currentDay = 1;

    public event Action FogChanged;
    public event Action<Vector2Int, FogVisibilityState> CellVisibilityChanged;

    public int CurrentDay => currentDay;
    public int RefogDelayDays => refogDelayDays;

    public readonly struct FogCellSnapshot
    {
        public FogCellSnapshot(Vector2Int grid, FogVisibilityState visibility, int lastRevealedDay)
        {
            Grid = grid;
            Visibility = visibility;
            LastRevealedDay = lastRevealedDay;
        }

        public Vector2Int Grid { get; }
        public FogVisibilityState Visibility { get; }
        public int LastRevealedDay { get; }
    }

    private sealed class FogCellData
    {
        public FogVisibilityState Visibility;
        public int LastRevealedDay;
    }

    public void SetCurrentDay(int day)
    {
        currentDay = Mathf.Max(1, day);
    }

    public FogVisibilityState GetVisibility(Vector2Int grid)
    {
        if (!fogCells.TryGetValue(grid, out FogCellData cell))
            return FogVisibilityState.Unexplored;

        return cell.Visibility;
    }

    public bool IsVisible(Vector2Int grid)
    {
        return GetVisibility(grid) == FogVisibilityState.Visible;
    }

    public bool IsExplored(Vector2Int grid)
    {
        return GetVisibility(grid) != FogVisibilityState.Unexplored;
    }

    public void RevealArea(Vector2Int center, int radius)
    {
        bool anyChanged = false;

        for (int y = center.y - radius; y <= center.y + radius; y++)
        {
            for (int x = center.x - radius; x <= center.x + radius; x++)
                anyChanged |= RevealCell(new Vector2Int(x, y));
        }

        if (anyChanged)
            FogChanged?.Invoke();
    }

    public void RevealCells(IEnumerable<Vector2Int> grids)
    {
        if (grids == null)
            return;

        bool anyChanged = false;
        foreach (Vector2Int grid in grids)
            anyChanged |= RevealCell(grid);

        if (anyChanged)
            FogChanged?.Invoke();
    }

    public void ApplyDayProgression()
    {
        if (refogDelayDays <= 0)
            return;

        bool anyChanged = false;
        foreach (KeyValuePair<Vector2Int, FogCellData> pair in fogCells)
        {
            FogCellData cell = pair.Value;
            if (cell.Visibility != FogVisibilityState.Visible)
                continue;

            if (currentDay - cell.LastRevealedDay < refogDelayDays)
                continue;

            cell.Visibility = FogVisibilityState.Fogged;
            CellVisibilityChanged?.Invoke(pair.Key, cell.Visibility);
            anyChanged = true;
        }

        if (anyChanged)
            FogChanged?.Invoke();
    }

    public IEnumerable<FogCellSnapshot> EnumerateKnownCells()
    {
        foreach (KeyValuePair<Vector2Int, FogCellData> pair in fogCells)
            yield return new FogCellSnapshot(pair.Key, pair.Value.Visibility, pair.Value.LastRevealedDay);
    }

    public void ClearFogData()
    {
        if (fogCells.Count == 0)
            return;

        fogCells.Clear();
        FogChanged?.Invoke();
    }

    private bool RevealCell(Vector2Int grid)
    {
        if (!fogCells.TryGetValue(grid, out FogCellData cell))
        {
            cell = new FogCellData();
            fogCells.Add(grid, cell);
        }

        bool changed = cell.Visibility != FogVisibilityState.Visible || cell.LastRevealedDay != currentDay;
        cell.Visibility = FogVisibilityState.Visible;
        cell.LastRevealedDay = currentDay;

        if (changed)
            CellVisibilityChanged?.Invoke(grid, cell.Visibility);

        return changed;
    }

}
