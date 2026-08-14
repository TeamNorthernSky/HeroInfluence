using System.Collections.Generic;
using UnityEngine;

public static class ExplorationCombatSkipResultHandler
{
    private const float VictoryInfluenceRatio = 1.1f;

    public static BattleRewardPlan BuildVictoryPlan(CombatContext context)
    {
        BattleRewardPlan plan = new BattleRewardPlan { Result = BattleResult.Victory };
        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        IReadOnlyList<int> partyUnitIndices = context != null ? context.CombatParty?.UnitIndices : null;
        if (unitRepository == null || partyUnitIndices == null)
            return plan;

        int expPerUnit = CalculateExpPerSurvivor(context, partyUnitIndices, unitRepository);

        for (int i = 0; i < partyUnitIndices.Count; i++)
        {
            int unitIndex = partyUnitIndices[i];
            if (unitIndex <= 0 || !unitRepository.TryGetUnit(unitIndex, out UnitPersistentData data) || data == null)
                continue;

            bool isSurvivor = IsCombatReady(data);
            int gainedExp = isSurvivor ? expPerUnit : 0;
            int newLevel = gainedExp > 0
                ? PersistentUnitRepository.SimulateFinalLevel(data, gainedExp)
                : data.Level;
            int classIndex = int.TryParse(data.UnitTemplateKey, out int parsed) ? parsed : -1;
            List<int> candidates = gainedExp > 0 && classIndex > 0
                ? DHCsvTemplateCatalog.Instance?.GetNewlyUnlockedStudySkills(classIndex, data.Level, newLevel) ?? new List<int>()
                : new List<int>();

            plan.UnitPreviews.Add(new UnitRewardPreview
            {
                UnitIndex = data.UnitIndex,
                UnitName = ResolveUnitDisplayName(data),
                OldLevel = data.Level,
                NewLevel = newLevel,
                GainedExp = gainedExp,
                CurrentClassSkillId = data.CurrentSkillIndex,
                OldInfluence = data.CurrentInfluence,
                NewInfluence = CalculateVictoryInfluence(data),
                UnlockCandidateSkillIds = candidates,
            });
        }

        return plan;
    }

    public static void CommitVictory(
        CombatContext context,
        BattleRewardPlan plan,
        IReadOnlyList<SkillSelectionResult> skillResults,
        CombatSkipHpResult hpResult)
    {
        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        IReadOnlyList<int> partyUnitIndices = context != null ? context.CombatParty?.UnitIndices : null;

        if (unitRepository != null && partyUnitIndices != null)
        {
            for (int i = 0; i < partyUnitIndices.Count; i++)
            {
                int unitIndex = partyUnitIndices[i];
                if (unitIndex <= 0 || !unitRepository.TryGetUnit(unitIndex, out UnitPersistentData data) || data == null)
                    continue;

                float nextHp = ResolveHeroHpAfter(hpResult, data.UnitIndex, data.CurrentHp);
                unitRepository.UpdateUnitRuntimeState(
                    data.UnitIndex,
                    data.UnitTemplateKey,
                    data.Level,
                    data.BaseStats,
                    data.LevelupStats,
                    data.CurrentSkillIndex,
                    data.CurrentWeaponKey,
                    data.CurrentWeaponStats,
                    data.IngameStats,
                    nextHp,
                    data.Exp,
                    data.MaxExp,
                    data.SkillLevel,
                    data.EquippedWeaponInstanceIndex,
                    CalculateVictoryInfluence(data),
                    nextHp <= 0f);
            }

            if (plan != null)
            {
                for (int i = 0; i < plan.UnitPreviews.Count; i++)
                {
                    UnitRewardPreview preview = plan.UnitPreviews[i];
                    if (preview != null && preview.GainedExp > 0)
                        unitRepository.AddExp(preview.UnitIndex, preview.GainedExp);
                }
            }

            ApplySkillSelections(unitRepository, skillResults);
            unitRepository.SaveRuntimeStateToDisk();
            PartyRepositorySync.ApplyUnitsToScene(partyUnitIndices);
        }
    }

    public static void CommitDefeat(CombatContext context, CombatSkipHpResult hpResult)
    {
        ExplorationDefeatResultHandler.CommitDefeat(context);
    }

    private static int CalculateExpPerSurvivor(
        CombatContext context,
        IReadOnlyList<int> partyUnitIndices,
        PersistentUnitRepository unitRepository)
    {
        int survivorCount = CountSurvivors(partyUnitIndices, unitRepository);
        if (survivorCount <= 0)
            return 0;

        float totalExp = CalculateTotalEnemyExp(context);
        if (totalExp <= 0f)
            return 0;

        return Mathf.CeilToInt(totalExp / survivorCount);
    }

    private static int CountSurvivors(IReadOnlyList<int> unitIndices, PersistentUnitRepository repository)
    {
        if (unitIndices == null || repository == null)
            return 0;

        int count = 0;
        for (int i = 0; i < unitIndices.Count; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex <= 0 || !repository.TryGetUnit(unitIndex, out UnitPersistentData data) || data == null)
                continue;

            if (IsCombatReady(data))
                count++;
        }

        return count;
    }
    private static float CalculateTotalEnemyExp(CombatContext context)
    {
        if (!CombatEnemyTemplatePreviewBuilder.TryBuildFromContext(
                context,
                out IReadOnlyList<CombatEnemyTemplatePreviewUnit> units))
        {
            return 0f;
        }

        float totalExp = 0f;
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i].IsValid)
                totalExp += Mathf.Max(0f, units[i].ExperiencePoint);
        }

        return totalExp;
    }

    private static void ApplySkillSelections(
        PersistentUnitRepository repository,
        IReadOnlyList<SkillSelectionResult> skillResults)
    {
        if (repository == null || skillResults == null)
            return;

        for (int i = 0; i < skillResults.Count; i++)
        {
            SkillSelectionResult selection = skillResults[i];
            if (!repository.TryGetUnit(selection.UnitIndex, out UnitPersistentData data) || data == null)
                continue;

            repository.UpdateUnitRuntimeState(
                data.UnitIndex,
                data.UnitTemplateKey,
                data.Level,
                data.BaseStats,
                data.LevelupStats,
                selection.SelectedSkillId,
                data.CurrentWeaponKey,
                data.CurrentWeaponStats,
                data.IngameStats,
                data.CurrentHp,
                data.Exp,
                data.MaxExp,
                data.SkillLevel,
                data.EquippedWeaponInstanceIndex,
                data.CurrentInfluence,
                data.IsIncapacitated);
        }
    }

    private static bool IsCombatReady(UnitPersistentData data)
    {
        return data != null && !data.IsIncapacitated && data.CurrentHp > 0f;
    }

    private static float ResolveHeroHpAfter(CombatSkipHpResult hpResult, int unitIndex, float fallback)
    {
        if (hpResult != null && hpResult.HeroHpAfter.TryGetValue(unitIndex, out float hp))
            return Mathf.Max(0f, hp);

        return Mathf.Max(0f, fallback);
    }

    private static float CalculateVictoryInfluence(UnitPersistentData data)
    {
        if (data == null)
            return 0f;

        float maxInfluence = Mathf.Max(0f, data.IngameStats.Influence);
        return Mathf.Clamp(data.CurrentInfluence * VictoryInfluenceRatio, 0f, maxInfluence);
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
