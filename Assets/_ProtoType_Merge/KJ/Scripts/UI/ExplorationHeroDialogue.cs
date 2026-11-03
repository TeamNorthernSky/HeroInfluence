using UnityEngine;

/// <summary>Maps exploration character templates to the dialogue table's independent hero IDs.</summary>
public static class ExplorationHeroDialogue
{
    public const string ResourceName = "WorldHeroDialogueDataTable";
    public enum Situation { GateOpened = 6, Idle = 7, BaseArrival = 8 }
    private static WorldHeroDialogueDataTable table;
    private static bool warnedMissing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { table = null; warnedMissing = false; }

    public static string GetMovementMessage(PartyUnitState unit) => GetMessage(unit, 1);

    public static string GetSituationMessage(PartyUnitState unit, Situation situation)
        => GetMessage(unit, (int)situation);

    public static string GetResourceMessage(PartyUnitState unit, ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Money: return GetMessage(unit, 2);
            case ResourceType.Crystal: return GetMessage(unit, 3);
            case ResourceType.Chip: return GetMessage(unit, 4);
            case ResourceType.Supply: return GetMessage(unit, 5);
            default: return null;
        }
    }

    public static int GetTemplateId(PartyUnitState unit)
    {
        if (unit == null) return 0;
        int storedId = GetTemplateId(unit.UnitIndex);
        if (storedId > 0) return storedId;
        string key = unit.UnitTemplateKey;
        return int.TryParse(key, out int templateId) ? templateId : 0;
    }

    public static int GetTemplateId(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        return repo != null && repo.TryGetUnit(unitIndex, out var data) && data != null &&
            int.TryParse(data.UnitTemplateKey, out int templateId) ? templateId : 0;
    }

    private static string GetMessage(PartyUnitState unit, int column)
    {
        int templateId = GetTemplateId(unit);
        int heroId;
        // Hero IDs follow the workbook's field definitions, independently of template order.
        switch (templateId)
        {
            case 10001: heroId = 0; break;
            case 10002: heroId = 2; break;
            case 10003: heroId = 3; break;
            case 10004: heroId = 1; break;
            default: return null;
        }

        if (table == null) table = Resources.Load<WorldHeroDialogueDataTable>(ResourceName);
        if (table == null)
        {
            if (!warnedMissing) Debug.LogWarning("[HeroDialogue] Missing world dialogue data table.");
            warnedMissing = true;
            return null;
        }
        foreach (var row in table.DataList)
        {
            if (row == null || row.hero_id != heroId) continue;
            string message = row.GetDialogue(column);
            return !string.IsNullOrWhiteSpace(message) ? message : null;
        }
        return null;
    }
}
