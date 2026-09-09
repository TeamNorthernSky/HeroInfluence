using System;
using System.Collections.Generic;
using UnityEngine;

public static class DHWorldEventChoiceConditionEvaluator
{
    private static readonly string[] OrderedUnitTemplateKeys =
    {
        "10001",
        "10004",
        "10003",
        "10002"
    };

    public static bool IsChoiceEnabled(DHWorldEventChoiceTemplate choice, PartyGridMover party, out string reason)
    {
        reason = string.Empty;
        if (choice == null)
        {
            reason = "Choice template is null.";
            return false;
        }

        DHWorldEventConditionTemplate condition = choice.EnableCondition;
        if (IsEmptyCondition(condition))
            return true;

        switch (condition.ConditionType)
        {
            case 0:
                return true;
            case 1:
                return EvaluateResourceCondition(condition, out reason);
            case 2:
                return EvaluateStatusCondition(condition, party, out reason);
            default:
                reason = $"Unknown choice condition type: {condition.ConditionType}";
                return false;
        }
    }

    public static bool TryGetAverageCurrentInfluence(PartyGridMover party, out float average)
    {
        average = 0f;
        if (!TryGetUnitDataForCondition(5, party, out IReadOnlyList<UnitPersistentData> units) || units.Count == 0)
            return false;

        float sum = 0f;
        for (int i = 0; i < units.Count; i++)
            sum += units[i].CurrentInfluence;

        average = sum / units.Count;
        return true;
    }

    private static bool EvaluateResourceCondition(DHWorldEventConditionTemplate condition, out string reason)
    {
        reason = string.Empty;
        if (!DHWorldEventCodeMap.TryGetResourceType(condition.Target, out ResourceType resourceType))
        {
            reason = $"Unknown resource condition target: {condition.Target}";
            return false;
        }

        EconomyManager economy = Game.Economy;
        if (economy == null)
        {
            reason = "Economy manager is missing.";
            return false;
        }

        int currentAmount = economy.Get(resourceType);
        if (Compare(currentAmount, condition.Value, condition.Operator))
            return true;

        reason = $"Resource condition failed: {resourceType} {currentAmount} {condition.Operator} {condition.Value}";
        return false;
    }

    private static bool EvaluateStatusCondition(
        DHWorldEventConditionTemplate condition,
        PartyGridMover party,
        out string reason)
    {
        reason = string.Empty;
        if (!DHWorldEventCodeMap.TryGetStatusType(condition.Target, out DHWorldEventStatusType statusType))
        {
            reason = $"Unknown status condition target: {condition.Target}";
            return false;
        }

        if (!TryGetUnitDataForCondition(condition.StatType, party, out IReadOnlyList<UnitPersistentData> units) || units.Count == 0)
        {
            reason = $"Target unit was not found: {condition.StatType}";
            return false;
        }

        float value = 0f;
        for (int i = 0; i < units.Count; i++)
            value += ReadStatus(units[i], statusType);

        if (condition.StatType == 5 && condition.CalculationType == 0)
            value /= units.Count;

        if (Compare(value, condition.Value, condition.Operator))
            return true;

        reason = $"Status condition failed: {statusType} {value:0.##} {condition.Operator} {condition.Value}";
        return false;
    }

    private static bool IsEmptyCondition(DHWorldEventConditionTemplate condition)
    {
        return condition.StatType == 0 && condition.ConditionType == 0 && condition.Target == 0;
    }

    private static bool TryGetUnitDataForCondition(
        int unitCode,
        PartyGridMover party,
        out IReadOnlyList<UnitPersistentData> units)
    {
        units = Array.Empty<UnitPersistentData>();
        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null)
            return false;

        if (unitCode >= 1 && unitCode <= OrderedUnitTemplateKeys.Length)
            return TryFindUnitByTemplateKey(repository, OrderedUnitTemplateKeys[unitCode - 1], out units);

        if (unitCode == 5)
            return TryGetPartyUnits(repository, party, out units);

        return false;
    }

    private static bool TryFindUnitByTemplateKey(
        PersistentUnitRepository repository,
        string templateKey,
        out IReadOnlyList<UnitPersistentData> units)
    {
        var result = new List<UnitPersistentData>(1);
        IReadOnlyList<UnitPersistentData> allUnits = repository.Units;
        for (int i = 0; i < allUnits.Count; i++)
        {
            UnitPersistentData unit = allUnits[i];
            if (unit == null)
                continue;

            if (string.Equals(unit.UnitTemplateKey, templateKey, StringComparison.Ordinal))
            {
                result.Add(unit);
                units = result;
                return true;
            }
        }

        units = result;
        return false;
    }

    private static bool TryGetPartyUnits(
        PersistentUnitRepository repository,
        PartyGridMover party,
        out IReadOnlyList<UnitPersistentData> units)
    {
        var result = new List<UnitPersistentData>(4);
        PartyComposition composition = ResolvePartyComposition(party);
        if (composition != null)
        {
            IReadOnlyList<int> unitIndices = composition.UnitIndices;
            for (int i = 0; i < unitIndices.Count; i++)
            {
                int unitIndex = unitIndices[i];
                if (unitIndex > 0 && repository.TryGetUnit(unitIndex, out UnitPersistentData unit) && unit != null)
                    result.Add(unit);
            }
        }

        if (result.Count == 0)
        {
            for (int i = 0; i < OrderedUnitTemplateKeys.Length; i++)
            {
                if (TryFindUnitByTemplateKey(repository, OrderedUnitTemplateKeys[i], out IReadOnlyList<UnitPersistentData> found) &&
                    found.Count > 0)
                {
                    result.Add(found[0]);
                }
            }
        }

        units = result;
        return result.Count > 0;
    }

    private static PartyComposition ResolvePartyComposition(PartyGridMover party)
    {
        PartyComposition composition = party != null ? party.GetComponent<PartyComposition>() : null;
        if (composition != null && composition.UnitIndices.Length > 0)
            return composition;

        PartyRegistry registry = UnityEngine.Object.FindFirstObjectByType<PartyRegistry>();
        PartyGridMover playerParty = registry != null ? registry.PlayerParty : null;
        composition = playerParty != null ? playerParty.GetComponent<PartyComposition>() : null;
        return composition != null && composition.UnitIndices.Length > 0 ? composition : null;
    }

    private static float ReadStatus(UnitPersistentData unit, DHWorldEventStatusType statusType)
    {
        switch (statusType)
        {
            case DHWorldEventStatusType.CurrentIP:
                return unit.CurrentInfluence;
            case DHWorldEventStatusType.CurrentHP:
                return unit.CurrentHp;
            case DHWorldEventStatusType.MaxIP:
                return unit.IngameStats.Influence;
            case DHWorldEventStatusType.MaxHP:
                return unit.IngameStats.HP;
            case DHWorldEventStatusType.Atk:
                return unit.IngameStats.Atk;
            default:
                return 0f;
        }
    }

    private static bool Compare(float current, float target, string operatorText)
    {
        string op = string.IsNullOrWhiteSpace(operatorText) ? ">=" : operatorText.Trim();
        return op switch
        {
            "0" or ">=" or "=>" => current >= target,
            "1" or "<=" or "=<" => current <= target,
            "2" or "==" or "=" => Mathf.Approximately(current, target),
            _ => false
        };
    }
}
