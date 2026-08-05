using System;
using System.Collections.Generic;

[Serializable]
public sealed class DHEventBranchTemplate
{
    private readonly List<string> triggerValues;

    public int BranchId { get; }
    public string SelectionIndex { get; }
    public string TriggerType { get; }
    public IReadOnlyList<string> TriggerValues => triggerValues;
    public string SelectionText { get; }
    public int TargetTalkId { get; }
    public string TriggerEffect { get; }
    public string Note { get; }

    public DHEventBranchTemplate(
        int branchId,
        string selectionIndex,
        string triggerType,
        IEnumerable<string> triggerValues,
        string selectionText,
        int targetTalkId,
        string triggerEffect,
        string note)
    {
        BranchId = branchId;
        SelectionIndex = Normalize(selectionIndex);
        TriggerType = Normalize(triggerType);
        this.triggerValues = NormalizeValues(triggerValues);
        SelectionText = Normalize(selectionText);
        TargetTalkId = targetTalkId;
        TriggerEffect = Normalize(triggerEffect);
        Note = Normalize(note);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static List<string> NormalizeValues(IEnumerable<string> values)
    {
        var result = new List<string>();
        if (values == null)
            return result;

        foreach (string value in values)
        {
            string normalized = Normalize(value);
            if (!string.IsNullOrEmpty(normalized))
                result.Add(normalized);
        }

        return result;
    }
}
