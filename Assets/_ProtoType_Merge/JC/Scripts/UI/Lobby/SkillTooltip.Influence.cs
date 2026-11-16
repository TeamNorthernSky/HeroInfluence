using TMPro;
using UnityEngine;

// JC | 2026-11-15 | 현재 IP와 등급별 피해 배율을 2열로 안내합니다.
public partial class SkillTooltip
{
    RectTransform influenceRoot;
    TMP_Text influenceValue, influenceBody, influenceNote;
    readonly TMP_Text[] influenceTitles = new TMP_Text[4];
    readonly TMP_Text[] influenceMultipliers = new TMP_Text[4];
    readonly TMP_Text[] influenceRanges = new TMP_Text[4];
    readonly UnityEngine.UI.Image[] influenceBadges = new UnityEngine.UI.Image[4];
    JcInfluenceBadgeArtwork influenceArtwork;
    const float InfluencePaddingX = 32f, InfluencePaddingY = 40f;
    const float InfluenceColumnGap = 36f, InfluenceTitleGap = 14f;
    static readonly Color InfluenceInk = new Color(.25f, .25f, .25f);

    public void ShowInfluence(float current, float maximum, string grade, RectTransform target, TMP_FontAsset font)
    {
        Hide();
        if (!target || !font) return;
        if (!influenceRoot)
        {
            var panel = new GameObject("IPTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(transform, false); panel.layer = gameObject.layer;
            influenceRoot = panel.GetComponent<RectTransform>();
            influenceArtwork = JcInfluenceBadgeArtwork.Load();
            var image = panel.GetComponent<UnityEngine.UI.Image>();
            image.sprite = Resources.Load<Sprite>("BattleEntry/HudPanel");
            image.type = UnityEngine.UI.Image.Type.Sliced; image.raycastTarget = false;
            influenceValue = CreateInfluenceText("CurrentIP", 28);
            influenceValue.fontStyle = FontStyles.Bold;
            influenceBody = CreateInfluenceText("Description", 23);
            influenceNote = CreateInfluenceText("DamageNote", 16);
            influenceNote.text = "IP 0 대비 일반 스킬 피해 배율 · 반격 제외";
            // CombatCalculator.CalculateDamage: baseDamage * (100 + CurrentInfluence) / 100.
            // JcHeroInfluenceBadge.Tier: <50, <100, <140, 그 이상. 정수 IP 기준 구간입니다.
            string[] titles = { JcHeroInfluenceBadge.Title(0), "유망주", "베테랑", "국가권력" };
            string[] multipliers = { "1 ~ 1.49배", "1.5 ~ 1.99배", "2 ~ 2.39배", "2.4 ~ 3배" };
            string[] ranges = { "0 ~ 49", "50 ~ 99", "100 ~ 139", "140 ~ 200" };
            for (int i = 0; i < 4; i++)
            {
                if (influenceArtwork && influenceArtwork.Frame(i))
                {
                    var badge = new GameObject("TierFrame" + i, typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    badge.transform.SetParent(influenceRoot, false); badge.layer = panel.layer;
                    influenceBadges[i] = badge.GetComponent<UnityEngine.UI.Image>();
                    influenceBadges[i].sprite = influenceArtwork.Frame(i);
                    influenceBadges[i].type = UnityEngine.UI.Image.Type.Simple;
                    influenceBadges[i].preserveAspect = true;
                    influenceBadges[i].raycastTarget = false;
                }
                influenceTitles[i] = CreateInfluenceText("Tier" + i, 36);
                influenceTitles[i].fontStyle = FontStyles.Bold;
                influenceTitles[i].text = titles[i];
                influenceMultipliers[i] = CreateInfluenceText("Multiplier" + i, 24);
                influenceMultipliers[i].text = multipliers[i];
                influenceRanges[i] = CreateInfluenceText("Range" + i, 16);
                influenceRanges[i].text = ranges[i];
                influenceRanges[i].color = new Color(.45f, .45f, .45f);
            }
        }
        // CombatCalculator와 같은 IP 피해 배율입니다. 최대 IP는 선택 영웅의 실제 상한을 사용합니다.
        float multiplier = (100f + Mathf.Max(0f, current)) / 100f;
        influenceValue.text = $"I.P : {current:0.##} / {maximum:0.##} || 현재 배율 : {multiplier:0.##}배";
        influenceBody.text = "I.P가 높을수록 피해량이 증가합니다.\n랭크업 시 최대 I.P가 10~20씩 증가합니다.";
        influenceValue.font = influenceBody.font = influenceNote.font = font;
        float columnWidth = 0f, titleHeight = 0f, rangeHeight = 0f, badgeWidth = 0f;
        for (int i = 0; i < 4; i++)
        {
            influenceTitles[i].font = influenceMultipliers[i].font = influenceRanges[i].font = font;
            if (influenceArtwork) influenceArtwork.Style(influenceTitles[i]);
            Vector2 titleSize = influenceTitles[i].GetPreferredValues();
            Vector2 multiplierSize = influenceMultipliers[i].GetPreferredValues();
            columnWidth = Mathf.Max(columnWidth, titleSize.x + InfluenceTitleGap + multiplierSize.x);
            badgeWidth = Mathf.Max(badgeWidth, titleSize.x + 28);
            columnWidth = Mathf.Max(columnWidth, influenceRanges[i].GetPreferredValues().x);
            titleHeight = Mathf.Max(titleHeight, Mathf.Max(titleSize.y, multiplierSize.y));
            rangeHeight = Mathf.Max(rangeHeight, influenceRanges[i].GetPreferredValues().y);
        }
        // 프레임 전체를 원본 3:1 비율로 표시해 이중 테두리와 모서리가 찌그러지지 않게 합니다.
        float badgeHeight = titleHeight + 32;
        badgeWidth = Mathf.Max(badgeWidth, badgeHeight * 3f);
        for (int i = 0; i < 4; i++)
            columnWidth = Mathf.Max(columnWidth, badgeWidth + InfluenceTitleGap + influenceMultipliers[i].GetPreferredValues().x);
        Vector2 valueSize = influenceValue.GetPreferredValues(), bodySize = influenceBody.GetPreferredValues();
        Vector2 noteSize = influenceNote.GetPreferredValues();
        float contentWidth = Mathf.Ceil(Mathf.Max(columnWidth * 2 + InfluenceColumnGap,
            Mathf.Max(valueSize.x, Mathf.Max(bodySize.x, noteSize.x))));
        columnWidth = (contentWidth - InfluenceColumnGap) * .5f;
        float y = InfluencePaddingY;
        PlaceInfluence(influenceValue.rectTransform, InfluencePaddingX, y, contentWidth, valueSize.y);
        y += valueSize.y + 16;
        PlaceInfluence(influenceBody.rectTransform, InfluencePaddingX, y, contentWidth, bodySize.y);
        y += bodySize.y + 32;
        float rowHeight = badgeHeight + 4 + rangeHeight;
        for (int i = 0; i < 4; i++)
        {
            // 윗줄은 듣보/유망주, 아랫줄은 베테랑/국가권력.
            float x = InfluencePaddingX + (i % 2) * (columnWidth + InfluenceColumnGap);
            float rowY = y + (i / 2) * (rowHeight + 32);
            float multiplierHeight = influenceMultipliers[i].GetPreferredValues().y;
            if (influenceBadges[i]) PlaceInfluence(influenceBadges[i].rectTransform, x, rowY, badgeWidth, badgeHeight);
            influenceTitles[i].alignment = TextAlignmentOptions.Center;
            PlaceInfluence(influenceTitles[i].rectTransform, x + 14, rowY + (badgeHeight - titleHeight) * .5f, badgeWidth - 28, titleHeight);
            PlaceInfluence(influenceMultipliers[i].rectTransform, x + badgeWidth + InfluenceTitleGap,
                rowY + (badgeHeight - multiplierHeight) * .5f, columnWidth - badgeWidth - InfluenceTitleGap, multiplierHeight);
            influenceRanges[i].alignment = TextAlignmentOptions.Center;
            PlaceInfluence(influenceRanges[i].rectTransform, x, rowY + badgeHeight + 4, badgeWidth, rangeHeight);
        }
        y += rowHeight * 2 + 32 + 26;
        PlaceInfluence(influenceNote.rectTransform, InfluencePaddingX, y, contentWidth, noteSize.y);
        influenceRoot.sizeDelta = new Vector2(contentWidth + InfluencePaddingX * 2, y + noteSize.y + InfluencePaddingY);
        influenceRoot.pivot = new Vector2(0, 1);
        influenceRoot.gameObject.SetActive(true); transform.SetAsLastSibling();
        PositionInfluenceTooltip(target);
    }
    public void ShowInfluenceAt(float current, float maximum, string grade, RectTransform anchor, TMP_FontAsset font)
    {
        ShowInfluence(current, maximum, grade, anchor, font);
        if (influenceRoot && anchor) PositionInfluenceTooltip(anchor, true);
    }

    void PositionInfluenceTooltip(RectTransform target, bool atAnchor = false)
        => PositionBadgeTooltip(influenceRoot, target, atAnchor);

    void PositionBadgeTooltip(RectTransform root, RectTransform target, bool atAnchor = false)
    {
        // IP 전용: 공용 스킬 툴팁 오프셋 대신 실제로 가리킨 아이콘/배지 가장자리에 붙입니다.
        var canvas = root.GetComponentInParent<Canvas>();
        var area = canvas ? canvas.transform as RectTransform : transform as RectTransform;
        if (!area) return;
        var sourceCanvas = target.GetComponentInParent<Canvas>();
        var sourceCamera = sourceCanvas && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? sourceCanvas.worldCamera : null;
        var tooltipCamera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in corners)
        {
            var screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, corner);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, tooltipCamera, out local);
            min = Vector2.Min(min, local); max = Vector2.Max(max, local);
        }
        const float gap = 40f;
        float x = atAnchor ? min.x : max.x + gap;
        if (!atAnchor && x + root.rect.width > area.rect.xMax)
            x = min.x - gap - root.rect.width;
        root.position = area.TransformPoint(new Vector3(x, max.y, 0));
        SkillTooltipCardLayout.KeepOnScreen(root);
    }
    TMP_Text CreateInfluenceText(string name, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(influenceRoot, false); go.layer = influenceRoot.gameObject.layer;
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size; text.enableAutoSizing = false; text.enableWordWrapping = false;
        text.color = InfluenceInk; text.raycastTarget = false; text.margin = Vector4.zero;
        text.alignment = TextAlignmentOptions.TopLeft; text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }
    static void PlaceInfluence(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
}
