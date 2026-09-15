using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Facility presentation only; spending and upgrade eligibility remain in HQUpgradeFlowController.
public sealed class HQUpgradeLayoutView : MonoBehaviour
{
    public TMP_Text heading, facilityName, currentLevel, nextLevel, description;
    public TMP_Text currentEffectTitle, nextEffectTitle, currentEffect, nextEffect;
    public TMP_Text requirements, money, supply, extraCosts, resultTitle, resultBody;
    public Image facilityImage;
    public Sprite[] facilitySprites;

    public void RefreshDisplay(HQDepartment dept, int level, HQStateManager hq, EconomyManager economy,
        IReadOnlyDictionary<ResourceType, int> costs)
    {
        bool maxed = level >= hq.GetMaxLevel(dept);
        heading.text = level == 0 ? "시설 건설" : "시설 업그레이드";
        facilityName.text = HQStateManager.GetDepartmentKoreanName(dept);
        currentLevel.text = $"Lv{level}";
        nextLevel.text = maxed ? "MAX" : $"Lv{level + 1}";
        description.text = Description(dept);
        if (facilitySprites != null && (int)dept < facilitySprites.Length)
            facilityImage.sprite = facilitySprites[(int)dept];
        currentEffectTitle.text = $"Lv{level} 효과";
        nextEffectTitle.text = maxed ? "최고 레벨" : $"Lv{level + 1} 효과";
        currentEffect.text = Effect(dept, level, hq);
        nextEffect.text = maxed ? "최고 단계에 도달했습니다." : Effect(dept, level + 1, hq);
        string unmet = hq.GetUnmetReasonText(dept);
        requirements.text = string.IsNullOrEmpty(unmet) ? "조건 충족" : unmet;
        requirements.color = string.IsNullOrEmpty(unmet) ? new Color(.15f,.3f,.8f) : new Color(.75f,.12f,.12f);
        money.text = maxed ? "-" : Ratio(ResourceType.Money, economy, costs);
        supply.text = maxed ? "-" : Ratio(ResourceType.Supply, economy, costs);
        extraCosts.text = "";
        foreach (var pair in costs)
            if (!maxed && pair.Value > 0 && (pair.Key == ResourceType.Chip || pair.Key == ResourceType.Crystal))
                extraCosts.text += (pair.Key == ResourceType.Chip ? "메달 " : "크리스탈 ") + Ratio(pair.Key, economy, costs) + "  ";
    }

    public void ShowResult(HQDepartment dept, int before, int after, HQStateManager hq)
    {
        resultTitle.text = $"{HQStateManager.GetDepartmentKoreanName(dept)} Lv{after} " +
            (before == 0 ? "건설이 완료되었습니다." : "업그레이드가 완료되었습니다.");
        resultBody.text = Effect(dept, after, hq);
    }

    private static string Ratio(ResourceType type, EconomyManager economy, IReadOnlyDictionary<ResourceType,int> costs)
    {
        costs.TryGetValue(type, out int need);
        int have = economy != null ? economy.Get(type) : 0;
        string color = have >= need ? "365CFF" : "C00000";
        return $"<color=#{color}>{have:N0}</color>/{need:N0}";
    }

    private static string Description(HQDepartment dept)
    {
        switch (dept)
        {
            case HQDepartment.Headquarters: return "협회의 운영과 시설 확장을 지원합니다.";
            case HQDepartment.Publicity: return "히어로를 홍보하여 IP를 올립니다.";
            case HQDepartment.Workshop: return "히어로 코어를 제작하고 강화합니다.";
            case HQDepartment.Research: return "히어로의 스킬을 강화합니다.";
            case HQDepartment.Training: return "훈련으로 히어로의 능력치를 올립니다.";
            case HQDepartment.Infirmary: return "다친 히어로의 체력을 회복합니다.";
            default: return "보유 자원을 다른 자원으로 교환합니다.";
        }
    }

    private static string Effect(HQDepartment dept, int level, HQStateManager hq)
    {
        if (level <= 0) return "미건설";
        switch (dept)
        {
            case HQDepartment.Headquarters:
                return $"• 매 턴 획득 자금 {hq.GetTurnIncomeAt(dept, level):N0}\n• 시설 건설 / 업그레이드";
            case HQDepartment.Publicity:
                int i = Mathf.Clamp(level - 1, 0, PublicityManager.CostPerLevel.Length - 1);
                return $"• 홍보 1회 비용 {PublicityManager.CostPerLevel[i]:N0}\n• 주간 홍보 한도 {PublicityManager.WeeklyMaxPerLevel[i]}";
            case HQDepartment.Training: return $"• 능력치 훈련 가능\n• 훈련 최대 Lv{Mathf.Min(level, 3)}";
            case HQDepartment.Research: return $"• 히어로 스킬 강화\n• 스킬 강화 최대 Lv{Mathf.Min(level + 1, 5)}";
            case HQDepartment.Infirmary: return "• 히어로 체력 회복\n• 전투 불능 히어로 소생";
            case HQDepartment.Workshop: return "• 히어로 코어 제작 / 강화\n• 코어 장착 변경";
            default: return "• 자원 교환";
        }
    }
}