using UnityEngine;

public static class TutorialCombatUnitFactory
{
    public const int RuntimeUnitIndexBase = 910000000;
    public const int MaxTutorialLevel = 999;

    public static bool TryCreateRuntimeData(
        string unitTemplateKey,
        int slotIndex,
        out SimulationAllyRuntimeData runtimeData,
        out UnitData unitData,
        out string error)
    {
        runtimeData = null;
        unitData = null;
        error = string.Empty;

        TutorialCatalog catalog = TutorialCatalog.Instance;
        if (catalog == null)
        {
            error = "TutorialCatalog is not ready.";
            return false;
        }

        string unitKey = string.IsNullOrWhiteSpace(unitTemplateKey)
            ? string.Empty
            : unitTemplateKey.Trim();
        if (!catalog.TryGetPlayerUnitTemplate(unitKey, out DHPlayerUnitTemplate template) || template == null)
        {
            error = $"Tutorial player unit template was not found: '{unitKey}'";
            return false;
        }

        unitData = CreateUnitData(template);
        if (!SimulationUnitFactory.HasPlayerBattlePrefab(unitData))
        {
            error = $"Tutorial player battle prefab was not found: UnitKey='{unitKey}', Index='{unitData.Index}'";
            return false;
        }

        int level = 1;
        int skillIndex = ResolveDefaultSkillIndex(template);
        string weaponKey = ResolveDefaultWeapon(catalog, template, out DHWeaponTemplate weaponTemplate);
        EquipmentStatBlock weaponStats = weaponTemplate != null
            ? EquipmentStatBlock.FromStatBlock(weaponTemplate.GetBonusStatsAtLevel(1))
            : default;
        int weaponIndex = weaponTemplate != null ? weaponTemplate.NumericWeaponId : 0;
        StatBlock ingameStats = UnitStatCalculator.CalculateIngameStats(
            template.BaseStats,
            template.LevelupStats,
            level,
            weaponStats,
            default,
            catalog.GetUnitGrowthTemplates());
        float currentHp = Mathf.Max(1f, ingameStats.HP);
        int runtimeUnitIndex = RuntimeUnitIndexBase + Mathf.Clamp(slotIndex + 1, 1, 999);

        UnitPersistentData transientUnit = new UnitPersistentData(
            runtimeUnitIndex,
            template.UnitKey,
            level,
            template.BaseStats,
            template.LevelupStats,
            skillIndex,
            weaponKey,
            weaponIndex,
            weaponStats,
            ingameStats,
            currentHp,
            0,
            0,
            default,
            1,
            0,
            0f,
            false);

        runtimeData = new SimulationAllyRuntimeData(runtimeUnitIndex, transientUnit);
        return true;
    }

    public static bool TryCreateUnitData(string unitTemplateKey, out UnitData unitData)
    {
        unitData = null;
        TutorialCatalog catalog = TutorialCatalog.Instance;
        if (catalog == null)
            return false;

        if (!catalog.TryGetPlayerUnitTemplate(unitTemplateKey, out DHPlayerUnitTemplate template) || template == null)
            return false;

        unitData = CreateUnitData(template);
        return true;
    }

    public static UnitData CreateUnitData(DHPlayerUnitTemplate template)
    {
        if (template == null)
            return null;

        return new UnitData
        {
            Index = template.UnitKey,
            UnitType = template.ClassConcept,
            Name = template.UnitName,
            baseStats = template.BaseStats,
            levelupStats = template.LevelupStats,
            IsEnemyRow = false
        };
    }

    public static EnemyData CreateEnemyData(DHEnemyUnitTemplate template, int enemyLevel)
    {
        if (template == null)
            return null;

        StatBlock ingameStats = UnitStatCalculator.CalculateLevelAdjustedBaseStats(
            template.BaseStats,
            template.LevelupStats,
            Mathf.Max(1, enemyLevel));

        return new EnemyData
        {
            Index = template.EnemyKey,
            UnitType = template.EnemyConcept,
            Name = template.EnemyName,
            baseStats = ingameStats,
            levelupStats = default,
            IsEnemyRow = true,
            UnitAI = template.UnitAI,
            ExperiencePoint = Mathf.Max(0, template.ExperiencePoint)
        };
    }

    private static int ResolveDefaultSkillIndex(DHPlayerUnitTemplate template)
    {
        if (template != null && template.ClassSkillIndices != null && template.ClassSkillIndices.Count > 0)
            return Mathf.Max(0, template.ClassSkillIndices[0]);

        return 0;
    }

    private static string ResolveDefaultWeapon(
        TutorialCatalog catalog,
        DHPlayerUnitTemplate template,
        out DHWeaponTemplate weaponTemplate)
    {
        weaponTemplate = null;
        if (catalog == null || template == null || template.WeaponIndices == null)
            return string.Empty;

        for (int i = 0; i < template.WeaponIndices.Count; i++)
        {
            int weaponIndex = template.WeaponIndices[i];
            if (catalog.TryGetWeaponTemplate(weaponIndex, out weaponTemplate) && weaponTemplate != null)
                return weaponTemplate.WeaponKey;
        }

        return string.Empty;
    }
}
