using System;
using System.Collections.Generic;

[Serializable]
public sealed class DHAssociationBuildTemplate
{
    private readonly List<DHAssociationResourceCost> costs;

    public string RoomType { get; }
    public int ActionType { get; }
    public int TargetRoomLevel { get; }
    public IReadOnlyList<DHAssociationResourceCost> Costs => costs;
    public string RoomActionCondition { get; }
    public string Note { get; }

    public DHAssociationBuildTemplate(
        string roomType,
        int actionType,
        int targetRoomLevel,
        IEnumerable<DHAssociationResourceCost> costs,
        string roomActionCondition,
        string note)
    {
        RoomType = roomType?.Trim() ?? string.Empty;
        ActionType = actionType;
        TargetRoomLevel = targetRoomLevel;
        this.costs = costs != null
            ? new List<DHAssociationResourceCost>(costs)
            : new List<DHAssociationResourceCost>();
        RoomActionCondition = roomActionCondition?.Trim() ?? string.Empty;
        Note = note?.Trim() ?? string.Empty;
    }
}
