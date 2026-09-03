using System;
using System.Collections.Generic;
using UnityEngine;

public static class SimulationUnitFactory
{
    public const int RuntimeUnitIndexBase = 900000000;
    public const int MaxSimulationLevel = 999;
    private const float MinimumHpRatio = 0.01f;

    public static bool TryCreateRuntimeData(
        SimulationAllyInput input,
        int slotIndex,
        out SimulationAllyRuntimeData runtimeData,
        out string error)
    {
        runtimeData = null;
        error = string.Empty;

        if (input == null || !input.Enabled)
        {
            error = "The ally slot is disabled.";
            return false;
        }

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog == null)
        {
            error = "DHCsvTemplateCatalog is not ready.";
            return false;
        }

        string unitKey = string.IsNullOrWhiteSpace(input.UnitTemplateKey)
            ? string.Empty
            : input.UnitTemplateKey.Trim();
        if (!catalog.TryGetPlayerUnitTemplate(unitKey, out DHPlayerUnitTemplate template) || template == null)
        {
            error = $"Player unit template was not found: '{unitKey}'";
            return false;
        }

        if (!catalog.TryGetPlayerTemplate(unitKey, out UnitData unitData) || unitData == null)
        {
            error = $"Player battle data was not found: '{unitKey}'";
            return false;
        }

        if (!HasPlayerBattlePrefab(unitData))
        {
            error = $"Player battle prefab was not found: UnitKey='{unitKey}', Index='{unitData.Index}'";
            return false;
        }

        int level = Mathf.Clamp(input.Level, 1, MaxSimulationLevel);
        string weaponKey = ResolveWeaponKey(catalog, template, input.WeaponKey, out DHWeaponTemplate weaponTemplate);
        EquipmentStatBlock weaponStats = weaponTemplate != null
            ? EquipmentStatBlock.FromStatBlock(weaponTemplate.GetBonusStatsAtLevel(WeaponPersistentRepository.BaseWeaponLevel))
            : default;
        int weaponIndex = weaponTemplate != null ? weaponTemplate.NumericWeaponId : 0;

        int skillIndex = Mathf.Max(0, input.SkillIndex);
        if (skillIndex == 0 && template.ClassSkillIndices != null && template.ClassSkillIndices.Count > 0)
            skillIndex = Mathf.Max(0, template.ClassSkillIndices[0]);

        StatBlock ingameStats = UnitStatCalculator.CalculateIngameStats(
            template.BaseStats,
            template.LevelupStats,
            level,
            weaponStats,
            default,
            catalog.GetUnitGrowthTemplates());

        float maxHp = Mathf.Max(1f, ingameStats.HP);
        float hpRatio = Mathf.Clamp(input.HpRatio, MinimumHpRatio, 1f);
        float currentHp = Mathf.Clamp(maxHp * hpRatio, 1f, maxHp);
        int runtimeUnitIndex = RuntimeUnitIndexBase + Mathf.Clamp(slotIndex + 1, 1, 999);

        var transientUnit = new UnitPersistentData(
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

    public static bool ValidateEnemyGroup(string enemyGroupKey, out string error)
    {
        error = string.Empty;
        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog == null)
        {
            error = "DHCsvTemplateCatalog is not ready.";
            return false;
        }

        string groupKey = string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
        if (!catalog.TryGetEnemyGroupTemplate(groupKey, out DHEnemyGroupTemplate group) || group == null)
        {
            error = $"Enemy group was not found: '{groupKey}'";
            return false;
        }

        if (group.Members == null || group.Members.Count == 0)
        {
            error = $"Enemy group has no members: '{groupKey}'";
            return false;
        }

        var usedSlots = new HashSet<int>();
        for (int i = 0; i < group.Members.Count; i++)
        {
            DHEnemyGroupMember member = group.Members[i];
            if (!usedSlots.Add(member.CombatSlot))
            {
                error = $"Enemy group has a duplicate CombatSlot. Group='{groupKey}', Slot={member.CombatSlot}";
                return false;
            }

            string enemyKey = member.EnemyUnitIndex.ToString();
            if (!catalog.TryGetEnemyTemplate(enemyKey, out EnemyData enemyData) || enemyData == null)
            {
                error = $"Enemy template was not found. Group='{groupKey}', Unit='{enemyKey}'";
                return false;
            }

            if (!HasEnemyBattlePrefab(enemyData))
            {
                error = $"Enemy battle prefab was not found. Group='{groupKey}', Index='{enemyData.Index}'";
                return false;
            }
        }

        return true;
    }

    public static bool HasPlayerBattlePrefab(UnitData data)
    {
        return HasBattlePrefab("prefab/BattlePrefab/PlayerUnit", data != null ? data.Index : string.Empty);
    }

    public static bool HasEnemyBattlePrefab(EnemyData data)
    {
        return HasBattlePrefab("prefab/BattlePrefab/EnemyUnit", data != null ? data.Index : string.Empty);
    }

    private static bool HasBattlePrefab(string resourcePath, string index)
    {
        if (string.IsNullOrWhiteSpace(index))
            return false;

        string suffix = "_" + index.Trim();
        GameObject[] prefabs = Resources.LoadAll<GameObject>(resourcePath);
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null && prefabs[i].name.EndsWith(suffix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string ResolveWeaponKey(
        DHCsvTemplateCatalog catalog,
        DHPlayerUnitTemplate template,
        string requestedWeaponKey,
        out DHWeaponTemplate weaponTemplate)
    {
        weaponTemplate = null;
        string normalized = string.IsNullOrWhiteSpace(requestedWeaponKey)
            ? string.Empty
            : requestedWeaponKey.Trim();

        if (!string.IsNullOrEmpty(normalized) &&
            catalog.TryGetWeaponTemplate(normalized, out weaponTemplate) &&
            weaponTemplate != null)
        {
            return weaponTemplate.WeaponKey;
        }

        if (template.WeaponIndices != null && template.WeaponIndices.Count > 0)
        {
            int defaultWeaponIndex = template.WeaponIndices[0];
            if (catalog.TryGetWeaponTemplate(defaultWeaponIndex, out weaponTemplate) && weaponTemplate != null)
                return weaponTemplate.WeaponKey;
        }

        if (catalog.TryGetWeaponTemplate("HC001", out weaponTemplate) && weaponTemplate != null)
            return weaponTemplate.WeaponKey;

        weaponTemplate = null;
        return string.Empty;
    }
}
