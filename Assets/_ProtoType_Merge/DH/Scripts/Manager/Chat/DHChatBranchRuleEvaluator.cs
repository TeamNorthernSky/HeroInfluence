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

        DHChatEffectRuntimeManager.EnsureInstance().ExecuteEffects(option.Trigger_Effect);
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

        DHChatEffectRuntimeManager runtime = DHChatEffectRuntimeManager.EnsureInstance();
        if (runtime.TryGetNumericValue(token, out value))
            return true;

        if (string.Equals(triggerType, "Resource", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse(token, true, out ResourceType resourceType) &&
            Game.Economy != null)
        {
            value = Game.Economy.Get(resourceType);
            return true;
        }

        if (string.Equals(triggerType, "Flag", StringComparison.OrdinalIgnoreCase))
        {
            value = runtime.GetFlag(token) ? 1f : 0f;
            return true;
        }

        Debug.LogWarning($"[DHChatBranchRuleEvaluator] Branch condition value was not found. Type: {triggerType}, Key: {token}");
        return false;
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
