using System.Collections.Generic;
using UnityEngine;

public readonly struct CombatEnemyTemplatePreviewUnit
{
    public readonly bool IsValid;
    public readonly string UnitTemplateKey;
    public readonly string DisplayName;
    public readonly int CombatSlot;
    public readonly int Level;
    public readonly StatBlock IngameStats;
    public readonly float CurrentHp;
    public readonly int ExperiencePoint;

    public CombatEnemyTemplatePreviewUnit(
        string unitTemplateKey,
        string displayName,
        int combatSlot,
        int level,
        StatBlock ingameStats,
        int experiencePoint)
    {
        IsValid = !string.IsNullOrWhiteSpace(unitTemplateKey);
        UnitTemplateKey = string.IsNullOrWhiteSpace(unitTemplateKey) ? string.Empty : unitTemplateKey.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? UnitTemplateKey : displayName.Trim();
        CombatSlot = Mathf.Max(0, combatSlot);
        Level = Mathf.Max(1, level);
        IngameStats = ingameStats;
        CurrentHp = Mathf.Max(1f, ingameStats.HP);
        ExperiencePoint = Mathf.Max(0, experiencePoint);
    }
}

public static class CombatEnemyTemplatePreviewBuilder
{
    private const int MaxCombatSlot = 6;

    public static bool TryBuildFromContext(
        CombatContext context,
        out IReadOnlyList<CombatEnemyTemplatePreviewUnit> units)
    {
        units = System.Array.Empty<CombatEnemyTemplatePreviewUnit>();
        if (context == null || context.CombatEnemy == null)
            return false;

        return TryBuild(context.CombatEnemy.EnemyGroupKey, context.EnemyLevel, out units);
    }

    public static bool TryBuild(
        string enemyGroupKey,
        int enemyLevel,
        out IReadOnlyList<CombatEnemyTemplatePreviewUnit> units)
    {
        var slotUnits = new CombatEnemyTemplatePreviewUnit[MaxCombatSlot];
        units = slotUnits;

        string groupKey = string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
        if (string.IsNullOrEmpty(groupKey))
            return false;

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog == null ||
            !catalog.TryGetEnemyGroupTemplate(groupKey, out DHEnemyGroupTemplate group) ||
            group == null ||
            group.Members == null ||
            group.Members.Count == 0)
        {
            return false;
        }

        int safeLevel = Mathf.Max(1, enemyLevel);
        bool hasAny = false;
        bool[] occupiedSlots = new bool[MaxCombatSlot];
        for (int i = 0; i < group.Members.Count; i++)
        {
            DHEnemyGroupMember member = group.Members[i];
            int combatSlot = member.CombatSlot > 0 ? member.CombatSlot : i + 1;
            if (combatSlot < 1 || combatSlot > MaxCombatSlot)
            {
                Debug.LogWarning($"Enemy group '{groupKey}' has an out-of-range combat slot '{combatSlot}'.");
                return false;
            }

            int slotIndex = combatSlot - 1;
            if (occupiedSlots[slotIndex])
            {
                Debug.LogWarning($"Enemy group '{groupKey}' has a duplicated combat slot '{combatSlot}'.");
                return false;
            }

            string unitKey = member.EnemyUnitIndex.ToString();
            if (!catalog.TryGetEnemyUnitTemplate(unitKey, out DHEnemyUnitTemplate unitTemplate) || unitTemplate == null)
            {
                Debug.LogWarning($"Enemy group '{groupKey}' references missing enemy unit '{unitKey}'.");
                return false;
            }

            StatBlock ingameStats = UnitStatCalculator.CalculateLevelAdjustedBaseStats(
                unitTemplate.BaseStats,
                unitTemplate.LevelupStats,
                safeLevel);

            slotUnits[slotIndex] = new CombatEnemyTemplatePreviewUnit(
                unitTemplate.EnemyKey,
                unitTemplate.EnemyName,
                combatSlot,
                safeLevel,
                ingameStats,
                unitTemplate.ExperiencePoint);
            occupiedSlots[slotIndex] = true;
            hasAny = true;
        }

        return hasAny;
    }

    public static string[] BuildPortraitKeys(IReadOnlyList<CombatEnemyTemplatePreviewUnit> units)
    {
        if (units == null || units.Count == 0)
            return System.Array.Empty<string>();

        string[] keys = new string[units.Count];
        for (int i = 0; i < units.Count; i++)
            keys[i] = units[i].IsValid ? units[i].UnitTemplateKey : string.Empty;

        return keys;
    }
}
