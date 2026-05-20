using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class KJ_MapObject
{
    public int id;
    public string objectName;
    public Vector2Int gridPosition;
    public List<Vector2Int> occupiedOffsets = new List<Vector2Int>();
    public bool isWalkable = false;

    public List<Vector2Int> GetOccupiedCells()
    {
        var cells = new List<Vector2Int>(occupiedOffsets.Count);
        foreach (var offset in occupiedOffsets)
            cells.Add(gridPosition + offset);
        return cells;
    }
}
