using System.Collections.Generic;
using UnityEngine;

public static class TutorialEnemySpawnPlanBuilder
{
    public static bool TryBuildFromEnemyGroup(
        string enemyGroupKey,
        int enemyLevel,
        out EnemySpawnPlan plan,
        out string error)
    {
        plan = null;
        error = string.Empty;

        string groupKey = string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
        if (string.IsNullOrEmpty(groupKey))
        {
            error = "Tutorial EnemyGroupKey is empty.";
            return false;
        }

        TutorialCatalog catalog = TutorialCatalog.Instance;
        if (catalog == null)
        {
            error = "TutorialCatalog is not available.";
            return false;
        }

        if (!catalog.TryGetEnemyGroupTemplate(groupKey, out DHEnemyGroupTemplate group) ||
            group == null ||
            group.Members == null ||
            group.Members.Count == 0)
        {
            error = $"Tutorial enemy group template not found or empty. groupKey='{groupKey}'";
            return false;
        }

        int safeLevel = Mathf.Max(1, enemyLevel);
        List<EnemySpawnEntry> entries = new List<EnemySpawnEntry>(group.Members.Count);
        bool[] occupiedSlots = new bool[6];
        for (int i = 0; i < group.Members.Count; i++)
        {
            DHEnemyGroupMember member = group.Members[i];
            int combatSlot = member.CombatSlot > 0 ? member.CombatSlot : i + 1;
            if (combatSlot < 1 || combatSlot > 6)
            {
                error = $"Tutorial enemy group has an out-of-range combat slot. groupKey='{groupKey}', slot={combatSlot}";
                return false;
            }

            int slotIndex = combatSlot - 1;
            if (occupiedSlots[slotIndex])
            {
                error = $"Tutorial enemy group has a duplicated combat slot. groupKey='{groupKey}', slot={combatSlot}";
                return false;
            }

            string unitKey = member.EnemyUnitIndex.ToString();
            if (!catalog.TryGetEnemyUnitTemplate(unitKey, out DHEnemyUnitTemplate unitTemplate) || unitTemplate == null)
            {
                error = $"Tutorial enemy template not found. groupKey='{groupKey}', unit='{unitKey}'";
                return false;
            }

            EnemyData spawnData = TutorialCombatUnitFactory.CreateEnemyData(unitTemplate, safeLevel);
            if (spawnData == null || !SimulationUnitFactory.HasEnemyBattlePrefab(spawnData))
            {
                error = $"Tutorial enemy battle prefab was not found. groupKey='{groupKey}', unit='{unitKey}'";
                return false;
            }

            entries.Add(new EnemySpawnEntry(
                spawnData,
                combatSlot,
                spawnData.Index,
                BuildEnemySkills(catalog, unitTemplate),
                unitTemplate.EnemyKey));
            occupiedSlots[slotIndex] = true;
        }

        plan = new EnemySpawnPlan(entries, null);
        return true;
    }

    private static IReadOnlyList<SkillData> BuildEnemySkills(TutorialCatalog catalog, DHEnemyUnitTemplate unitTemplate)
    {
        List<SkillData> skills = new List<SkillData>(2);
        TryAddSkill(catalog, unitTemplate, 1, skills);
        TryAddSkill(catalog, unitTemplate, 2, skills);
        return skills;
    }

    private static void TryAddSkill(
        TutorialCatalog catalog,
        DHEnemyUnitTemplate unitTemplate,
        int slot,
        List<SkillData> skills)
    {
        if (catalog == null || unitTemplate == null || skills == null)
            return;

        string skillKey = EnemySkillKeyRules.Compose(unitTemplate.EnemyKey, slot);
        SkillData skill = catalog.GetSkillTemplate(skillKey);
        if (skill != null)
            skills.Add(skill);
    }
}
