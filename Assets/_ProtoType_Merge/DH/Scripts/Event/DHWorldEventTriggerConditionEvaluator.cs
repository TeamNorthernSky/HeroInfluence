using System;
using System.Collections.Generic;
using UnityEngine;

public static class DHWorldEventTriggerConditionEvaluator
{
    private static readonly string[] OrderedUnitTemplateKeys =
    {
        "10001",
        "10004",
        "10003",
        "10002"
    };

    public static bool IsSatisfied(
        DHWorldEventTemplate template,
        PartyGridMover party,
        int currentTurn,
        out string reason)
    {
        reason = string.Empty;
        if (template == null)
        {
            reason = "World event template is null.";
            return false;
        }

        IReadOnlyList<DHWorldEventConditionTemplate> conditions = template.TriggerConditions;
        if (conditions == null || conditions.Count == 0)
            return true;

        for (int i = 0; i < conditions.Count; i++)
        {
            if (!IsSatisfied(conditions[i], party, currentTurn, out reason))
                return false;
        }

        return true;
    }

    private static bool IsSatisfied(
        DHWorldEventConditionTemplate condition,
        PartyGridMover party,
        int currentTurn,
        out string reason)
    {
        reason = string.Empty;
        switch (condition.ConditionType)
        {
            case 0:
                return EvaluateResourceCondition(condition, out reason);
            case 1:
                return EvaluateUnitCondition(condition, party, out reason);
            case 2:
                return Compare(currentTurn, condition.Value, condition.Operator);
            default:
                reason = $"Unknown trigger condition type: {condition.ConditionType}";
                return false;
        }
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

    private static bool EvaluateUnitCondition(
        DHWorldEventConditionTemplate condition,
        PartyGridMover party,
        out string reason)
    {
        reason = string.Empty;
        if (!DHWorldEventCodeMap.TryGetStatusType(condition.StatType, out DHWorldEventStatusType statusType))
        {
            reason = $"Unknown status condition type: {condition.StatType}";
            return false;
        }

        if (!TryGetUnitData(condition.Target, party, out IReadOnlyList<UnitPersistentData> units) || units.Count == 0)
        {
            reason = $"Target unit was not found: {condition.Target}";
            return false;
        }

        float value = 0f;
        for (int i = 0; i < units.Count; i++)
            value += ReadStatus(units[i], statusType);

        if (condition.Target == 4 && condition.CalculationType == 0)
            value /= units.Count;

        if (Compare(value, condition.Value, condition.Operator))
            return true;

        reason = $"Unit condition failed: {statusType} {value:0.##} {condition.Operator} {condition.Value}";
        return false;
    }

    private static bool TryGetUnitData(
        int targetCode,
        PartyGridMover party,
        out IReadOnlyList<UnitPersistentData> units)
    {
        units = Array.Empty<UnitPersistentData>();
        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null)
            return false;

        if (targetCode >= 0 && targetCode < OrderedUnitTemplateKeys.Length)
            return TryFindUnitByTemplateKey(repository, OrderedUnitTemplateKeys[targetCode], out units);

        if (targetCode == 4)
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
