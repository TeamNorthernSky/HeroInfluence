using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public static class DHWorldEventChoiceResolver
{
    public const string CancelChoiceId = "__cancel";
    private const string DefaultCancelText = "취소";

    public static IReadOnlyList<DHWorldEventChoicePresentationOption> BuildPresentationOptions(
        DHWorldEventTemplate template,
        PartyGridMover party,
        string cancelText = DefaultCancelText)
    {
        var result = new List<DHWorldEventChoicePresentationOption>(3);
        if (template != null)
        {
            IReadOnlyList<DHWorldEventChoiceTemplate> choices = template.Choices;
            for (int i = 0; i < choices.Count; i++)
            {
                DHWorldEventChoiceTemplate choice = choices[i];
                bool enabled = DHWorldEventChoiceConditionEvaluator.IsChoiceEnabled(choice, party, out string disabledReason);
                float successRate = CalculateSuccessRate(choice, party);
                result.Add(new DHWorldEventChoicePresentationOption(
                    choice.ChoiceId,
                    choice.ChoiceText,
                    false,
                    enabled,
                    enabled ? string.Empty : disabledReason,
                    successRate,
                    $"{Mathf.RoundToInt(successRate)}%",
                    choice.SuccessResultGroupId,
                    choice.FailureResultGroupId));
            }
        }

        result.Add(new DHWorldEventChoicePresentationOption(
            CancelChoiceId,
            string.IsNullOrWhiteSpace(cancelText) ? DefaultCancelText : cancelText,
            true,
            true,
            string.Empty,
            -1f,
            string.Empty,
            string.Empty,
            string.Empty));

        return result;
    }

    public static bool TryResolveResultGroupId(
        DHWorldEventChoicePresentationOption option,
        out string resultGroupId,
        out bool succeeded)
    {
        resultGroupId = string.Empty;
        succeeded = false;

        if (option == null || option.IsCancel || !option.IsEnabled)
            return false;

        succeeded = RollSuccess(option.SuccessRatePercent);
        resultGroupId = succeeded ? option.SuccessResultGroupId : option.FailureResultGroupId;
        return !string.IsNullOrWhiteSpace(resultGroupId);
    }

    private static bool RollSuccess(float successRate)
    {
        if (successRate >= 100f)
            return true;

        if (successRate <= 0f)
            return false;

        return Random.Range(0f, 100f) < successRate;
    }

    private static float CalculateSuccessRate(DHWorldEventChoiceTemplate choice, PartyGridMover party)
    {
        if (choice == null)
            return 0f;

        float baseRate = ParsePercent(choice.BaseSuccessRateRaw);
        if (choice.UseIpSuccessRateBonus != 1)
            return Mathf.Clamp(baseRate, 0f, 100f);

        if (!DHWorldEventChoiceConditionEvaluator.TryGetAverageCurrentInfluence(party, out float averageCurrentInfluence))
            averageCurrentInfluence = 0f;

        return Mathf.Clamp(baseRate + averageCurrentInfluence + 0.5f, 0f, 100f);
    }

    private static float ParsePercent(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0f;

        var builder = new StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (char.IsDigit(c) || c == '.' || c == '-')
                builder.Append(c);
        }

        return float.TryParse(
            builder.ToString(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float value)
            ? value
            : 0f;
    }
}
