[System.Serializable]
public struct KJ_GridCell
{
    public enum VisibilityState : byte { Unexplored, Fogged, Visible }

    public KJ_TerrainType terrainType;
    public VisibilityState visibility;
    public bool isWalkable;
    public int occupantId;
    public int eventId;

    public bool IsOccupied => occupantId >= 0;

    public static KJ_GridCell CreateDefault()
    {
        return new KJ_GridCell
        {
            terrainType = KJ_TerrainType.Plain,
            visibility = VisibilityState.Unexplored,
            isWalkable = true,
            occupantId = -1,
            eventId = -1,
        };
    }
}
