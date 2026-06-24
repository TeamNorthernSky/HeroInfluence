using System.Collections.Generic;
using UnityEngine;

public static class ExplorationDefeatResultHandler
{
    private const float DefeatInfluenceRatio = 0.9f;

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
                data.CurrentWeaponIndex,
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
        RefreshScenePartyUnitStates(unitIndices, repository);
    }

    private static float CalculateDefeatInfluence(UnitPersistentData data)
    {
        if (data == null)
            return 0f;

        float maxInfluence = Mathf.Max(0f, data.IngameStats.Influence);
        return Mathf.Clamp(data.CurrentInfluence * DefeatInfluenceRatio, 0f, maxInfluence);
    }

    private static string ResolveUnitDisplayName(UnitPersistentData data)
    {
        if (data == null)
            return string.Empty;

        if (DHCsvTemplateCatalog.Instance != null &&
            DHCsvTemplateCatalog.Instance.TryGetPlayerTemplate(data.UnitTemplateKey, out UnitData unitData) &&
            unitData != null &&
            !string.IsNullOrWhiteSpace(unitData.Name))
        {
            return unitData.Name;
        }

        return string.IsNullOrWhiteSpace(data.UnitTemplateKey)
            ? $"Unit {data.UnitIndex}"
            : data.UnitTemplateKey;
    }

    private static void RefreshScenePartyUnitStates(IReadOnlyList<int> unitIndices, PersistentUnitRepository repository)
    {
        PartyUnitState[] sceneUnitStates = Object.FindObjectsByType<PartyUnitState>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneUnitStates.Length; i++)
        {
            PartyUnitState state = sceneUnitStates[i];
            if (state == null || state.UnitIndex <= 0 || !ContainsUnitIndex(unitIndices, state.UnitIndex))
                continue;

            if (repository.TryGetUnit(state.UnitIndex, out UnitPersistentData data))
                state.ApplyPersistentData(data);
        }
    }

    private static bool ContainsUnitIndex(IReadOnlyList<int> unitIndices, int unitIndex)
    {
        if (unitIndices == null || unitIndex <= 0)
            return false;

        for (int i = 0; i < unitIndices.Count; i++)
        {
            if (unitIndices[i] == unitIndex)
                return true;
        }

        return false;
    }
}
