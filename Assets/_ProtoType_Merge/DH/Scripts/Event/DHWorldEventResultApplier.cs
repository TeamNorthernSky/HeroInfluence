using System;
using System.Collections.Generic;
using UnityEngine;

public static class DHWorldEventResultApplier
{
    public static bool CanApply(DHWorldEventTemplate template, PartyGridMover party, out string reason)
    {
        reason = string.Empty;
        if (template == null)
        {
            reason = "World event template is null.";
            return false;
        }

        EconomyManager economy = Game.Economy;
        IReadOnlyList<DHWorldEventResultTemplate> results = template.Results;
        for (int i = 0; i < results.Count; i++)
        {
            DHWorldEventResultTemplate result = results[i];
            if (result.ResultKind != DHWorldEventResultKind.ResourceCost)
                continue;

            if (!DHWorldEventCodeMap.TryGetResourceType(result.EffectType, out ResourceType resourceType))
            {
                reason = $"Unknown resource code: {result.EffectType}";
                return false;
            }

            int amount = Mathf.Abs(result.EffectAmount);
            if (amount <= 0)
                continue;

            if (economy == null || !economy.Has(resourceType, amount))
            {
                reason = $"Not enough resource: {resourceType} {amount}";
                return false;
            }
        }

        return true;
    }

    public static bool TryApply(DHWorldEventTemplate template, PartyGridMover party, out string reason)
    {
        if (!CanApply(template, party, out reason))
            return false;

        IReadOnlyList<DHWorldEventResultTemplate> results = template.Results;
        bool appliedAny = false;

        for (int i = 0; i < results.Count; i++)
        {
            DHWorldEventResultTemplate result = results[i];
            switch (result.ResultKind)
            {
                case DHWorldEventResultKind.ResourceCost:
                    appliedAny |= ApplyResourceCost(result);
                    break;
                case DHWorldEventResultKind.StatusEffect:
                case DHWorldEventResultKind.ChoiceEffect:
                    appliedAny |= ApplyStatusEffect(result, party);
                    break;
            }
        }

        if (appliedAny)
            SyncPartyUnits(party);

        reason = string.Empty;
        return true;
    }

    private static bool ApplyResourceCost(DHWorldEventResultTemplate result)
    {
        if (!DHWorldEventCodeMap.TryGetResourceType(result.EffectType, out ResourceType resourceType))
            return false;

        int amount = Mathf.Abs(result.EffectAmount);
        return amount > 0 && Game.Economy != null && Game.Economy.Spend(resourceType, amount);
    }

    private static bool ApplyStatusEffect(DHWorldEventResultTemplate result, PartyGridMover party)
    {
        if (!DHWorldEventCodeMap.TryGetStatusType(result.EffectType, out DHWorldEventStatusType statusType))
            return false;

        if (!TryGetTargetUnitIndices(result.TargetScope, party, out IReadOnlyList<int> unitIndices))
            return false;

        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null)
            return false;

        bool appliedAny = false;
        for (int i = 0; i < unitIndices.Count; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex <= 0)
                continue;

            appliedAny |= ApplyStatusToUnit(repository, unitIndex, statusType, result.EffectAmount);
        }

        return appliedAny;
    }

    private static bool ApplyStatusToUnit(
        PersistentUnitRepository repository,
        int unitIndex,
        DHWorldEventStatusType statusType,
        int amount)
    {
        switch (statusType)
        {
            case DHWorldEventStatusType.CurrentIP:
                return repository.ApplyEventRewardStats(unitIndex, 0f, 0f, 0f, amount, 0f);
            case DHWorldEventStatusType.CurrentHP:
                return repository.ApplyEventRewardStats(unitIndex, 0f, 0f, 0f, 0f, amount);
            case DHWorldEventStatusType.MaxIP:
                return repository.ApplyEventRewardStats(unitIndex, 0f, 0f, 0f, 0f, 0f, amount);
            case DHWorldEventStatusType.MaxHP:
                return repository.ApplyEventRewardStats(unitIndex, amount, 0f, 0f, 0f, 0f);
            case DHWorldEventStatusType.Atk:
                return repository.ApplyEventRewardStats(unitIndex, 0f, amount, 0f, 0f, 0f);
            default:
                return false;
        }
    }

    private static bool TryGetTargetUnitIndices(
        int targetScope,
        PartyGridMover party,
        out IReadOnlyList<int> unitIndices)
    {
        unitIndices = Array.Empty<int>();

        PartyComposition composition = party != null ? party.GetComponent<PartyComposition>() : null;
        if (composition != null && composition.UnitIndices.Length > 0)
        {
            unitIndices = composition.UnitIndices;
            return true;
        }

        PartyRegistry registry = UnityEngine.Object.FindFirstObjectByType<PartyRegistry>();
        PartyGridMover playerParty = registry != null ? registry.PlayerParty : null;
        composition = playerParty != null ? playerParty.GetComponent<PartyComposition>() : null;
        if (composition != null && composition.UnitIndices.Length > 0)
        {
            unitIndices = composition.UnitIndices;
            return true;
        }

        return TryGetDefaultUnitIndices(out unitIndices);
    }

    private static bool TryGetDefaultUnitIndices(out IReadOnlyList<int> unitIndices)
    {
        var result = new List<int>(4);
        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null)
        {
            unitIndices = result;
            return false;
        }

        TryAddUnitIndexByTemplateKey(repository, result, "10001");
        TryAddUnitIndexByTemplateKey(repository, result, "10002");
        TryAddUnitIndexByTemplateKey(repository, result, "10003");
        TryAddUnitIndexByTemplateKey(repository, result, "10004");
        unitIndices = result;
        return result.Count > 0;
    }

    private static void TryAddUnitIndexByTemplateKey(
        PersistentUnitRepository repository,
        List<int> result,
        string unitTemplateKey)
    {
        IReadOnlyList<UnitPersistentData> units = repository.Units;
        for (int i = 0; i < units.Count; i++)
        {
            UnitPersistentData data = units[i];
            if (data == null)
                continue;

            if (string.Equals(data.UnitTemplateKey, unitTemplateKey, StringComparison.Ordinal))
            {
                result.Add(data.UnitIndex);
                return;
            }
        }
    }

    private static void SyncPartyUnits(PartyGridMover party)
    {
        if (!TryGetTargetUnitIndices(0, party, out IReadOnlyList<int> unitIndices))
            return;

        PartyRepositorySync.ApplyUnitsToScene(unitIndices);
    }
}
