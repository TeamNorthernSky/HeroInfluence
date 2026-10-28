using System.Collections.Generic;
using UnityEngine;

/// <summary>Maps exploration character templates to the dialogue table's independent hero IDs.</summary>
public static class ExplorationHeroDialogue
{
    private static Dictionary<int, string[]> rows;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => rows = null;

    public static string GetMovementMessage(PartyUnitState unit) => GetMessage(unit, 1);

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

    private static string GetMessage(PartyUnitState unit, int column)
    {
        if (unit == null) return null;
        string key = unit.UnitTemplateKey;
        var repo = PersistentUnitRepository.Instance;
        if (string.IsNullOrWhiteSpace(key) && repo != null && repo.TryGetUnit(unit.UnitIndex, out var data) && data != null)
            key = data.UnitTemplateKey;

        if (!int.TryParse(key, out int templateId)) return null;
        int heroId;
        // PlayerUnitDataTable.ClassIndex: Justice, Lumina, Black Bullet, Nekoming.
        switch (templateId)
        {
            case 10001: heroId = 0; break;
            case 10004: heroId = 1; break;
            case 10002: heroId = 2; break;
            case 10003: heroId = 3; break;
            default: return null;
        }

        EnsureLoaded();
        return rows.TryGetValue(heroId, out var row) && column < row.Length
            && !string.IsNullOrWhiteSpace(row[column]) ? row[column] : null;
    }

    private static void EnsureLoaded()
    {
        if (rows != null) return;
        rows = new Dictionary<int, string[]>();
        var source = Resources.Load<TextAsset>("ExplorationHeroDialogue");
        if (source == null)
        {
            Debug.LogWarning("[HeroDialogue] Missing Resources/ExplorationHeroDialogue.csv; using existing bubble messages.");
            return;
        }
        var lines = source.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            // This table contains plain comma-separated cells, without quoted fields.
            var cells = lines[i].TrimEnd('\r').Split(',');
            if (cells.Length != 9 || !int.TryParse(cells[0], out int heroId) || rows.ContainsKey(heroId))
            {
                Debug.LogWarning($"[HeroDialogue] Invalid or duplicate row {i + 1}; using existing messages for missing entries.");
                continue;
            }
            rows.Add(heroId, cells);
        }
    }
}
