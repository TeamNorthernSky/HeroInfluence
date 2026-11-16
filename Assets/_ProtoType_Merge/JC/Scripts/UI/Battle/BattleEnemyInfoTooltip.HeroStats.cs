using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class BattleEnemyInfoTooltip
{
    private const float HeroStatsHeight = 442f;
    private RectTransform heroStatDetails;
    private TMP_Text criticalChance, criticalBreakdown, reactionChance, reactionBreakdown, formationDescription;
    private TMP_Text reactionRule;
    private Image reactionIcon;

    private void EnsureHeroStatDetails()
    {
        if (heroStatDetails != null || panel == null || defense == null) return;
        heroStatDetails = new GameObject("HeroStatDetails", typeof(RectTransform)).GetComponent<RectTransform>();
        heroStatDetails.SetParent(panel, false);
        heroStatDetails.anchorMin = heroStatDetails.anchorMax = heroStatDetails.pivot = new Vector2(0f, 1f);
        heroStatDetails.anchoredPosition = Vector2.zero;
        heroStatDetails.sizeDelta = new Vector2(panel.rect.width, HeroStatsHeight);
        criticalChance = CreateHeroStatText("CriticalChance", 232f, 32f, 23f, Color.white);
        criticalBreakdown = CreateHeroStatText("CriticalBreakdown", 265f, 28f, 17f, new Color32(168, 216, 255, 255));
        reactionChance = CreateHeroStatText("ReactionChance", 306f, 32f, 23f, Color.white);
        reactionBreakdown = CreateHeroStatText("ReactionBreakdown", 339f, 28f, 17f, new Color32(168, 216, 255, 255));
        reactionRule = CreateHeroStatText("ReactionRule", 372f, 25f, 16f, new Color32(168, 216, 255, 255));
        formationDescription = CreateHeroStatText("FormationDescription", 414f, 50f, 19f, new Color32(255, 213, 106, 255));
        foreach (var text in new[] { criticalChance, criticalBreakdown, reactionChance, reactionBreakdown, reactionRule })
        {
            text.rectTransform.anchoredPosition += new Vector2(36f, 0f);
            text.rectTransform.sizeDelta -= new Vector2(36f, 0f);
        }
        CreateHeroStatIcon("CriticalIcon", 234f, Sprites.UI.Status(UIStatusIconType.Critical));
        reactionIcon = CreateHeroStatIcon("ReactionIcon", 308f, null);
    }

    private Image CreateHeroStatIcon(string name, float top, Sprite sprite)
    {
        var icon = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var rect = icon.rectTransform;
        rect.SetParent(heroStatDetails, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(26f, -top);
        rect.sizeDelta = new Vector2(28f, 28f);
        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        return icon;
    }

    private TMP_Text CreateHeroStatText(string name, float top, float height, float size, Color color)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        var rect = text.rectTransform;
        rect.SetParent(heroStatDetails, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(26f, -top);
        rect.sizeDelta = new Vector2(Mathf.Max(1f, panel.rect.width - 52f), height);
        text.font = defense.font;
        text.fontSharedMaterial = defense.fontSharedMaterial;
        text.fontSize = size;
        text.fontStyle = size >= 19f ? FontStyles.Bold : FontStyles.Normal;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private void RefreshHeroStatDetails()
    {
        if (heroStatDetails == null) return;
        var stats = shownUnit.FinalStats;
        // 스탯 재계산이 사용하는 전열 우선 판정과 같은 순서를 유지한다.
        bool front = shownUnit.IsFrontRow();
        bool back = !front && shownUnit.IsInBackRow;
        LayoutHeroStatDetails(front, back);
        float criticalBonus = back ? 0.15f : 0f;
        criticalChance.text = $"치명타 확률: {Mathf.Clamp(stats.CriticalRate, 0f, 0.7f) * 100f:0.#}%";
        criticalBreakdown.text = back ? FormationBreakdown(stats.CriticalRate, criticalBonus, 0.7f) : string.Empty;
        reactionChance.text = front
            ? $"반격 확률: {Mathf.Clamp01(stats.CounterRate) * 100f:0.#}%"
            : $"피해 경감 확률: {Mathf.Clamp01(stats.AvoidRate) * 100f:0.#}%";
        reactionBreakdown.text = front ? FormationBreakdown(stats.CounterRate, 0.3f, 1f) : string.Empty;
        reactionIcon.sprite = Sprites.UI.Status(front ? UIStatusIconType.Counter : UIStatusIconType.DamageReduction);
        reactionRule.text = front ? "전열에서만 반격 발동" : "후열에서만 피해 경감 발동";
        formationDescription.text = front
            ? "전열 효과 적용 중: 반격 확률 30% 증가"
            : back ? "후열 효과 적용 중: 치명타 확률 15% 증가" : string.Empty;
    }

    private void LayoutHeroStatDetails(bool front, bool back)
    {
        // 설명이 없는 줄은 높이도 제외해 다음 항목을 바로 이어 붙인다.
        criticalBreakdown.gameObject.SetActive(back);
        reactionBreakdown.gameObject.SetActive(front);
        SetStatTop(criticalBreakdown.rectTransform, 264f);
        float reactionTop = 232f + 32f + (back ? 24f : 0f) + 10f;
        SetStatTop(reactionChance.rectTransform, reactionTop);
        SetStatTop(reactionIcon.rectTransform, reactionTop + 2f);
        SetStatTop(reactionBreakdown.rectTransform, reactionTop + 32f);
        float ruleTop = reactionTop + 32f + (front ? 24f : 0f) + 8f;
        SetStatTop(reactionRule.rectTransform, ruleTop);
        SetStatTop(formationDescription.rectTransform, ruleTop + 23f + 16f);
    }

    private static void SetStatTop(RectTransform rect, float top)
    {
        var position = rect.anchoredPosition;
        position.y = -top;
        rect.anchoredPosition = position;
    }

    private static string FormationBreakdown(float total, float bonus, float cap)
    {
        // 튜토리얼 등 최종 확률을 덮어쓰는 효과가 있으면 잘못된 합계식을 표시하지 않는다.
        if (total < bonus) return $"진형 효과 +{bonus * 100f:0.#}% · 다른 효과로 제한됨";
        string text = $"기본 {(total - bonus) * 100f:0.#}% + 진형 효과 {bonus * 100f:0.#}%";
        return total > cap ? text + $" (최대 {cap * 100f:0.#}%)" : text;
    }
}
