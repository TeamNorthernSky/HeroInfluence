/// <summary>
/// 그리드 셀 하나의 데이터. 값 타입(struct)으로 구현.
/// 좌표(x,y)는 보유하지 않으며, PlayGridManagerJC의 인덱스(y * width + x)로 도출.
/// 8192² 같은 대형 그리드에서 메모리 사용량을 최소화하기 위한 구조.
/// </summary>
[System.Serializable]
public struct GridCell
{
    public enum VisibilityState : byte { Unexplored, Fogged, Visible }

    public TerrainType terrainType;
    public VisibilityState visibility;
    public bool isWalkable;

    /// <summary>
    /// 이 셀을 점유 중인 MapObject의 ID. -1이면 비어있음.
    /// </summary>
    public int occupantId;

    /// <summary>
    /// 이 셀에 연결된 이벤트 ID. -1이면 없음.
    /// </summary>
    public int eventId;

    public bool IsOccupied => occupantId >= 0;

    /// <summary>
    /// 기본값으로 초기화된 셀을 생성.
    /// </summary>
    public static GridCell CreateDefault()
    {
        return new GridCell
        {
            terrainType = TerrainType.Plain,
            visibility = VisibilityState.Unexplored,
            isWalkable = true,
            occupantId = -1,
            eventId = -1,
        };
    }
}
