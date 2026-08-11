using System.Collections.Generic;
using UnityEngine;

public static class ExplorationDefeatResultHandler
{
    private const float DefeatInfluenceRatio = 0.9f;
    private const float DefeatRecoveryHpRatio = 0.3f;

    public static BattleRewardPlan BuildDefeatPlan(CombatContext context)
    {
        BattleRewardPlan plan = new BattleRewardPlan { Result = BattleResult.Defeat };
        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        IReadOnlyList<int> unitIndices = context != null ? context.CombatParty?.UnitIndices : null;
        if (repository == null || unitIndices == null)
            return plan;

        for (int i = 0; i < unitIndices.Count; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex <= 0 || !repository.TryGetUnit(unitIndex, out UnitPersistentData data) || data == null)
                continue;

            float oldInfluence = data.CurrentInfluence;
            float newInfluence = CalculateDefeatInfluence(data);
            plan.UnitPreviews.Add(new UnitRewardPreview
            {
                UnitIndex = data.UnitIndex,
                UnitName = ResolveUnitDisplayName(data),
                OldLevel = data.Level,
                NewLevel = data.Level,
                GainedExp = 0,
                CurrentClassSkillId = data.CurrentSkillIndex,
                OldInfluence = oldInfluence,
                NewInfluence = newInfluence,
                UnlockCandidateSkillIds = new List<int>(),
            });
        }

        return plan;
    }

    public static void CommitDefeat(CombatContext context)
    {
        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        IReadOnlyList<int> unitIndices = context != null ? context.CombatParty?.UnitIndices : null;
        if (repository == null || unitIndices == null)
            return;

        for (int i = 0; i < unitIndices.Count; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex <= 0 || !repository.TryGetUnit(unitIndex, out UnitPersistentData data) || data == null)
                continue;

            repository.UpdateUnitRuntimeState(
                data.UnitIndex,
                data.UnitTemplateKey,
                data.Level,
                data.BaseStats,
                data.LevelupStats,
                data.CurrentSkillIndex,
                data.CurrentWeaponKey,
                data.CurrentWeaponStats,
                data.IngameStats,
                0f,
                data.Exp,
                data.MaxExp,
                data.SkillLevel,
                data.EquippedWeaponInstanceIndex,
                CalculateDefeatInfluence(data),
                true);
        }

        repository.SaveRuntimeStateToDisk();
        PartyRepositorySync.ApplyUnitsToScene(unitIndices);
    }

    public static int RecoverDefeatedUnitsForReturn(IReadOnlyList<int> unitIndices)
    {
        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null || unitIndices == null)
            return 0;

        int recoveredCount = 0;
        for (int i = 0; i < unitIndices.Count; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex <= 0 || !repository.TryGetUnit(unitIndex, out UnitPersistentData data) || data == null)
                continue;

            float recoveredHp = CalculateDefeatRecoveryHp(data);
            repository.UpdateUnitRuntimeState(
                data.UnitIndex,
                data.UnitTemplateKey,
                data.Level,
                data.BaseStats,
                data.LevelupStats,
                data.CurrentSkillIndex,
                data.CurrentWeaponKey,
                data.CurrentWeaponStats,
                data.IngameStats,
                recoveredHp,
                data.Exp,
                data.MaxExp,
                data.SkillLevel,
                data.EquippedWeaponInstanceIndex,
                data.CurrentInfluence,
                false);

            recoveredCount++;
        }

        if (recoveredCount > 0)
        {
            repository.SaveRuntimeStateToDisk();
            PartyRepositorySync.ApplyUnitsToScene(unitIndices);
        }

        return recoveredCount;
    }

    private static float CalculateDefeatInfluence(UnitPersistentData data)
    {
        if (data == null)
            return 0f;

        float maxInfluence = Mathf.Max(0f, data.IngameStats.Influence);
        return Mathf.Clamp(data.CurrentInfluence * DefeatInfluenceRatio, 0f, maxInfluence);
    }

    private static float CalculateDefeatRecoveryHp(UnitPersistentData data)
    {
        if (data == null)
            return 0f;

        float maxHp = Mathf.Max(0f, data.IngameStats.HP);
        if (maxHp <= 0f)
            return 0f;

        return Mathf.Clamp(Mathf.Ceil(maxHp * DefeatRecoveryHpRatio), 1f, maxHp);
    }

    private static string ResolveUnitDisplayName(UnitPersistentData data)
    {
        if (data == null)
            return string.Empty;

        if (DHCsvTemplateCatalog.Instance != null &&
            DHCsvTemplateCatalog.Instance.TryGetPlayerUnitTemplate(data.UnitTemplateKey, out DHPlayerUnitTemplate unitData) &&
            unitData != null &&
            !string.IsNullOrWhiteSpace(unitData.UnitName))
        {
            return unitData.UnitName;
        }

        return string.IsNullOrWhiteSpace(data.UnitTemplateKey)
            ? $"Unit {data.UnitIndex}"
            : data.UnitTemplateKey;
    }

}
