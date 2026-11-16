using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class SkillTooltip
{
    private RectTransform rankRoot, rankTarget, currentRankFrame;
    private TMP_Text rankHeading, rankDescription, currentRankLabel;
    private readonly Image[] rankIcons = new Image[9];
    private static readonly string[] RankOrder = { "F", "E", "D", "C", "B", "A", "S", "SS", "SSS" };

    public void ShowRank(string current, Sprite[] sprites, RectTransform target, TMP_FontAsset font)
    {
        Hide();
        if (!target || !font) return;
        if (!rankRoot) BuildRankTooltip();
        rankTarget = target;
        rankHeading.font = rankDescription.font = currentRankLabel.font = font;
        int currentIndex = System.Array.IndexOf(RankOrder, current);
        rankRoot.sizeDelta = new Vector2(600, currentIndex >= 5 ? 360 : 328);
        for (int i = 0; i < rankIcons.Length; i++)
        {
            var icon = rankIcons[i];
            icon.sprite = HeroRankBadge.FindSprite(sprites, RankOrder[i]);
            icon.enabled = icon.sprite != null;
            // 윗줄 5개, 아랫줄 4개를 각각 가운데 정렬한다.
            float x = i < 5 ? 64 + i * 100 : 114 + (i - 5) * 100;
            float rowY = i < 5 ? 124 : 232;
            // A는 왕관이 없어 방패와 글자가 높아 보이므로 승인된 초안처럼 5px 내린다.
            float y = rowY + (RankOrder[i] == "A" ? 5 : 0);
            PlaceInfluence(icon.rectTransform, x, y, 72, 72);
            if (RankOrder[i] != current) continue;
            // 왕관·날개를 포함한 72px 아이콘 전체에 사방 12px 여유를 둔다.
            PlaceInfluence(currentRankFrame, x - 12, y - 12, 96, 96);
            PlaceInfluence(currentRankLabel.rectTransform, x - 14, y + 88, 100, 22);
        }
        bool known = currentIndex >= 0;
        currentRankFrame.gameObject.SetActive(known);
        currentRankLabel.gameObject.SetActive(known);
        rankRoot.gameObject.SetActive(true);
        transform.SetAsLastSibling();
        PositionBadgeTooltip(rankRoot, target);
    }

    public void RefreshRankPosition(RectTransform target)
    {
        if (rankRoot && rankRoot.gameObject.activeSelf && rankTarget == target)
            PositionBadgeTooltip(rankRoot, target);
    }

    public void HideRank(RectTransform target)
    {
        if (rankTarget == target && rankRoot && rankRoot.gameObject.activeSelf) Hide();
    }

    private void BuildRankTooltip()
    {
        var go = new GameObject("RankTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        rankRoot = go.GetComponent<RectTransform>();
        rankRoot.SetParent(transform, false);
        rankRoot.pivot = new Vector2(0, 1);
        rankRoot.sizeDelta = new Vector2(600, 328);
        var background = go.GetComponent<Image>();
        background.sprite = Resources.Load<Sprite>("BattleEntry/HudPanel");
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;
        rankHeading = RankText("Heading", "랭크 단계", 28, FontStyles.Bold);
        PlaceInfluence(rankHeading.rectTransform, 28, 24, 544, 38);
        rankDescription = RankText("Description", "랭크 상승 시 공격력·방어력·최대 체력이 증가합니다.", 20, FontStyles.Normal);
        PlaceInfluence(rankDescription.rectTransform, 28, 70, 544, 30);
        var divider = new GameObject("HeaderDivider", typeof(RectTransform), typeof(Image));
        divider.layer = go.layer;
        divider.transform.SetParent(rankRoot, false);
        var dividerImage = divider.GetComponent<Image>();
        dividerImage.color = new Color32(190, 201, 217, 160);
        dividerImage.raycastTarget = false;
        PlaceInfluence(dividerImage.rectTransform, 28, 108, 544, 1);
        for (int i = 0; i < rankIcons.Length; i++)
        {
            var icon = new GameObject("Rank" + RankOrder[i], typeof(RectTransform), typeof(Image));
            icon.layer = go.layer;
            icon.transform.SetParent(rankRoot, false);
            rankIcons[i] = icon.GetComponent<Image>();
            rankIcons[i].preserveAspect = true;
            rankIcons[i].raycastTarget = false;
        }
        currentRankFrame = new GameObject("CurrentRankCorners", typeof(RectTransform)).GetComponent<RectTransform>();
        currentRankFrame.gameObject.layer = go.layer;
        currentRankFrame.SetParent(rankRoot, false);
        var marker = currentRankFrame.gameObject.AddComponent<Image>();
        marker.sprite = Resources.Load<Sprite>("UI/RankCurrentFrame");
        marker.raycastTarget = false;
        currentRankLabel = RankText("CurrentRankLabel", "현재 랭크", 15, FontStyles.Bold);
        currentRankLabel.color = new Color32(28, 88, 167, 255);
        currentRankLabel.alignment = TextAlignmentOptions.Top;
    }

    private TMP_Text RankText(string name, string value, float size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        go.transform.SetParent(rankRoot, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = InfluenceInk;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.margin = Vector4.zero;
        return text;
    }
}
