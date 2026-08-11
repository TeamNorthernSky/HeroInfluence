using System;

[Serializable]
public sealed class DHAssociationTrainingTemplate
{
    public int RequiredTrainingRoomLevel { get; }
    public string TrainingName { get; }
    public DHAssociationResourceCost Cost { get; }
    public int StatusType { get; }
    public int StatusAmountPerTraining { get; }
    public int TrainingCountPerUnit { get; }
    public string TrainingCondition { get; }
    public string Note { get; }

    public DHAssociationTrainingTemplate(
        int requiredTrainingRoomLevel,
        string trainingName,
        DHAssociationResourceCost cost,
        int statusType,
        int statusAmountPerTraining,
        int trainingCountPerUnit,
        string trainingCondition,
        string note)
    {
        RequiredTrainingRoomLevel = requiredTrainingRoomLevel;
        TrainingName = trainingName?.Trim() ?? string.Empty;
        Cost = cost;
        StatusType = statusType;
        StatusAmountPerTraining = statusAmountPerTraining;
        TrainingCountPerUnit = trainingCountPerUnit;
        TrainingCondition = trainingCondition?.Trim() ?? string.Empty;
        Note = note?.Trim() ?? string.Empty;
    }
}
