using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵 위에 배치되는 오브젝트의 데이터.
/// 건물, 지형물, 캐릭터 등 그리드를 점유하는 모든 것.
/// </summary>
[System.Serializable]
public class MapObject
{
    public int id;
    public string objectName;

    /// <summary>
    /// 기준점 (앵커) 그리드 좌표.
    /// </summary>
    public Vector2Int gridPosition;

    /// <summary>
    /// 점유하는 셀들의 상대 좌표 목록.
    /// 예: L자 형태 → (0,0), (0,1), (0,2), (1,2)
    /// </summary>
    public List<Vector2Int> occupiedOffsets = new List<Vector2Int>();

    public bool isWalkable = false;

    /// <summary>
    /// 이 오브젝트가 실제로 점유하는 월드 좌표 목록 반환.
    /// </summary>
    public List<Vector2Int> GetOccupiedCells()
    {
        var cells = new List<Vector2Int>(occupiedOffsets.Count);
        foreach (var offset in occupiedOffsets)
        {
            cells.Add(gridPosition + offset);
        }
        return cells;
    }
}
