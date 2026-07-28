using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum ChatBranchConditionMode
{
    Single,
    Any,
    All
}

public sealed class ChatBranchOptionState
{
    public BranchDBEventData Option { get; }
    public bool IsInteractable { get; }

    public ChatBranchOptionState(BranchDBEventData option, bool isInteractable)
    {
        Option = option;
        IsInteractable = isInteractable;
    }
}

public static class DHChatBranchRuleEvaluator
{
    public static bool IsBranchAvailable(BranchDBEventData option)
    {
        if (option == null)
            return false;

        IReadOnlyList<string> values = option.Trigger_Value;
        if ((values == null || values.Count == 0) && string.IsNullOrWhiteSpace(option.Trigger_Type))
            return true;

        var conditions = BuildConditions(option.Trigger_Type, values);
        if (conditions.Count == 0)
            return true;

        ChatBranchConditionMode mode = GetConditionMode(option.Selection_Index, conditions.Count);
        bool result = mode == ChatBranchConditionMode.All;

        for (int i = 0; i < conditions.Count; i++)
        {
            bool passed = EvaluateCondition(conditions[i].triggerType, conditions[i].expression);

            if (mode == ChatBranchConditionMode.Single)
                return passed;

            if (mode == ChatBranchConditionMode.Any && passed)
                return true;

            if (mode == ChatBranchConditionMode.All && !passed)
                return false;
        }

        return result;
    }

    public static void ExecuteTriggerEffect(BranchDBEventData option)
    {
        if (option == null || string.IsNullOrWhiteSpace(option.Trigger_Effect))
            return;

        DHEventEffectRuntimeManager.EnsureInstance().ExecuteEffects(option.Trigger_Effect);
    }

    private static List<(string triggerType, string expression)> BuildConditions(
        string triggerTypeRaw,
        IReadOnlyList<string> triggerValues)
    {
        var conditions = new List<(string triggerType, string expression)>();
        var types = SplitTokens(triggerTypeRaw);

        if (triggerValues == null)
            return conditions;

        for (int i = 0; i < triggerValues.Count; i++)
        {
            string expression = triggerValues[i]?.Trim();
            if (string.IsNullOrEmpty(expression))
                continue;

            string triggerType = types.Count == 0
                ? string.Empty
                : types[Mathf.Min(i, types.Count - 1)];

            conditions.Add((triggerType, expression));
        }

        return conditions;
    }

    private static List<string> SplitTokens(string raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
            return result;

        string[] parts = raw.Split(new[] { '\\', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string token = parts[i]?.Trim();
            if (!string.IsNullOrEmpty(token))
                result.Add(token);
        }

        return result;
    }

    private static ChatBranchConditionMode GetConditionMode(string selectionIndex, int conditionCount)
    {
        if (!string.IsNullOrWhiteSpace(selectionIndex))
        {
            string trimmed = selectionIndex.Trim();
            char suffix = char.ToUpperInvariant(trimmed[trimmed.Length - 1]);
            if (suffix == 'A')
                return ChatBranchConditionMode.Single;
            if (suffix == 'B')
                return ChatBranchConditionMode.Any;
            if (suffix == 'C')
                return ChatBranchConditionMode.All;
        }

        return conditionCount <= 1 ? ChatBranchConditionMode.Single : ChatBranchConditionMode.All;
    }

    private static bool EvaluateCondition(string triggerType, string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return true;

        if (TryEvaluateChainedComparison(triggerType, expression, out bool chainedResult))
            return chainedResult;

        string[] operators = { "==", "!=", ">=", "<=", "=>", "=<", "=", ">", "<" };
        for (int i = 0; i < operators.Length; i++)
        {
            string op = operators[i];
            int index = expression.IndexOf(op, StringComparison.Ordinal);
            if (index < 0)
                continue;

            string left = expression.Substring(0, index).Trim();
            string right = expression.Substring(index + op.Length).Trim();
            if (!TryResolveOperand(triggerType, left, out float leftValue) ||
                !TryResolveOperand(triggerType, right, out float rightValue))
            {
                return false;
            }

            return Compare(leftValue, NormalizeOperator(op), rightValue);
        }

        return TryResolveOperand(triggerType, expression, out float value) && value != 0f;
    }

    private static bool TryEvaluateChainedComparison(string triggerType, string expression, out bool result)
    {
        result = false;
        string[] operators = { ">=", "<=", "=>", "=<", ">", "<" };

        for (int firstOpIndex = 0; firstOpIndex < operators.Length; firstOpIndex++)
        {
            string firstOp = operators[firstOpIndex];
            int first = expression.IndexOf(firstOp, StringComparison.Ordinal);
            if (first < 0)
                continue;

            string left = expression.Substring(0, first).Trim();
            string remainder = expression.Substring(first + firstOp.Length).Trim();

            for (int secondOpIndex = 0; secondOpIndex < operators.Length; secondOpIndex++)
            {
                string secondOp = operators[secondOpIndex];
                int second = remainder.IndexOf(secondOp, StringComparison.Ordinal);
                if (second < 0)
                    continue;

                string middle = remainder.Substring(0, second).Trim();
                string right = remainder.Substring(second + secondOp.Length).Trim();
                if (!TryResolveOperand(triggerType, left, out float leftValue) ||
                    !TryResolveOperand(triggerType, middle, out float middleValue) ||
                    !TryResolveOperand(triggerType, right, out float rightValue))
                {
                    return true;
                }

                result = Compare(leftValue, NormalizeOperator(firstOp), middleValue) &&
                         Compare(middleValue, NormalizeOperator(secondOp), rightValue);
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveOperand(string triggerType, string raw, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        string token = raw.Trim();
        if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        DHEventStateRepository eventStateRepository = DHEventStateRepository.EnsureInstance();
        if (eventStateRepository.TryGetNumericValue(token, out value))
            return true;

        if (string.Equals(triggerType, "Stat", StringComparison.OrdinalIgnoreCase) &&
            TryResolveUnitStat(token, out value))
        {
            return true;
        }

        if (string.Equals(triggerType, "Flag", StringComparison.OrdinalIgnoreCase))
        {
            value = eventStateRepository.GetFlag(token) ? 1f : 0f;
            return true;
        }

        Debug.LogWarning($"[DHChatBranchRuleEvaluator] Branch condition value was not found. Type: {triggerType}, Key: {token}");
        return false;
    }

    private static bool TryResolveUnitStat(string token, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        int separatorIndex = token.LastIndexOf('_');
        if (separatorIndex <= 0 || separatorIndex >= token.Length - 1)
            return false;

        string unitAlias = token.Substring(0, separatorIndex).Trim();
        string statKey = token.Substring(separatorIndex + 1).Trim();
        if (!TryResolveUnitTemplateKey(unitAlias, out string unitTemplateKey) ||
            !TryFindPersistentUnit(unitTemplateKey, out UnitPersistentData unitData))
        {
            return false;
        }

        return TryReadUnitStat(unitData, statKey, out value);
    }

    private static bool TryResolveUnitTemplateKey(string alias, out string unitTemplateKey)
    {
        unitTemplateKey = string.Empty;
        if (string.IsNullOrWhiteSpace(alias))
            return false;

        string normalized = NormalizeAlias(alias);
        switch (normalized)
        {
            case "10001":
            case "JUSTICE":
            case "저스티스":
                unitTemplateKey = "10001";
                return true;
            case "10002":
            case "RUMINA":
            case "LUMINA":
            case "루미나":
                unitTemplateKey = "10002";
                return true;
            case "10003":
            case "BLACKBULLET":
            case "BLACK_BULLET":
            case "블랙불릿":
                unitTemplateKey = "10003";
                return true;
            case "10004":
            case "NEKOMING":
            case "네코밍":
                unitTemplateKey = "10004";
                return true;
            default:
                return false;
        }
    }

    private static string NormalizeAlias(string alias)
    {
        return alias.Trim().Replace(" ", string.Empty).Replace("-", "_").ToUpperInvariant();
    }

    private static bool TryFindPersistentUnit(string unitTemplateKey, out UnitPersistentData unitData)
    {
        unitData = null;
        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null || string.IsNullOrWhiteSpace(unitTemplateKey))
            return false;

        IReadOnlyList<UnitPersistentData> units = repository.Units;
        for (int i = 0; i < units.Count; i++)
        {
            UnitPersistentData candidate = units[i];
            if (candidate == null)
                continue;

            if (!string.Equals(candidate.UnitTemplateKey, unitTemplateKey, StringComparison.Ordinal))
                continue;

            unitData = candidate;
            return true;
        }

        return false;
    }

    private static bool TryReadUnitStat(UnitPersistentData unitData, string statKey, out float value)
    {
        value = 0f;
        if (unitData == null || string.IsNullOrWhiteSpace(statKey))
            return false;

        switch (statKey.Trim().ToUpperInvariant())
        {
            case "IP":
            case "INFLUENCE":
            case "CURRENTIP":
            case "CURRENTINFLUENCE":
                value = unitData.CurrentInfluence;
                return true;
            case "MAXIP":
            case "MAXINFLUENCE":
                value = unitData.IngameStats.Influence;
                return true;
            case "HP":
            case "CURRENTHP":
                value = unitData.CurrentHp;
                return true;
            case "MAXHP":
                value = unitData.IngameStats.HP;
                return true;
            case "ATK":
            case "ATTACK":
                value = unitData.IngameStats.Atk;
                return true;
            case "DEF":
            case "DEFENSE":
                value = unitData.IngameStats.DEF;
                return true;
            default:
                return false;
        }
    }

    private static string NormalizeOperator(string op)
    {
        if (op == "=")
            return "==";
        if (op == "=>")
            return ">=";
        if (op == "=<")
            return "<=";
        return op;
    }

    private static bool Compare(float left, string op, float right)
    {
        switch (op)
        {
            case "==": return Mathf.Approximately(left, right);
            case "!=": return !Mathf.Approximately(left, right);
            case ">=": return left >= right;
            case "<=": return left <= right;
            case ">": return left > right;
            case "<": return left < right;
            default: return false;
        }
    }
}
