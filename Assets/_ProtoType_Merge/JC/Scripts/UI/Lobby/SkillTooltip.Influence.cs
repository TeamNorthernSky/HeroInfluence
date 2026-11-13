using TMPro;
using UnityEngine;
using UnityEngine.UI;

// JC | 2026-11-13 | IP 설명만 좌측 HUD와 같은 패널을 사용합니다. 기존 스킬/강화 툴팁 레이아웃은 건드리지 않습니다.
public partial class SkillTooltip
{
    RectTransform influenceRoot;
    TMP_Text influenceValue, influenceGrade, influenceBody;
    RectTransform influenceDivider;
    const float InfluencePaddingX = 32f * 1.5f, InfluencePaddingY = 20f * 1.5f;
    const float InfluenceRowGap = 8f * 1.5f;
    static readonly Color InfluenceInk = new Color(.02f, .04f, .16f);

    public void ShowInfluence(float current, float maximum, string grade, RectTransform target, TMP_FontAsset font)
    {
        Hide();
        if (!target || !font) return;
        if (!influenceRoot)
        {
            var panel = new GameObject("IPTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(transform, false); panel.layer = gameObject.layer;
            influenceRoot = panel.GetComponent<RectTransform>();
            var image = panel.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>("BattleEntry/HudPanel");
            image.type = Image.Type.Sliced; image.raycastTarget = false;
            influenceValue = CreateInfluenceText("CurrentIP", 26);
            influenceGrade = CreateInfluenceText("Grade", 26);
            influenceBody = CreateInfluenceText("Description", 23);
            var divider = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            divider.transform.SetParent(influenceRoot, false); divider.layer = panel.layer;
            var line = divider.GetComponent<Image>(); line.color = new Color(.38f,.56f,.9f,.6f); line.raycastTarget = false;
            influenceDivider = divider.GetComponent<RectTransform>();
        }
        influenceValue.text = $"현재 IP: {current:0} / {maximum:0}";
        influenceGrade.text = "등급: " + grade;
        influenceBody.text = "IP가 높을수록 일반 스킬 공격의 피해량이 증가합니다.\n반격에는 이 배율이 적용되지 않습니다.";
        influenceValue.font = influenceGrade.font = influenceBody.font = font;
        // 글자를 줄이거나 자동 줄바꿈하지 않고 가장 긴 원문에 패딩을 더해 패널 폭을 결정합니다.
        Vector2 a = influenceValue.GetPreferredValues(), b = influenceGrade.GetPreferredValues(), c = influenceBody.GetPreferredValues();
        float contentWidth = Mathf.Ceil(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
        float y = InfluencePaddingY;
        PlaceInfluence(influenceValue.rectTransform, InfluencePaddingX, y, contentWidth, a.y); y += a.y + InfluenceRowGap;
        PlaceInfluence(influenceGrade.rectTransform, InfluencePaddingX, y, contentWidth, b.y); y += b.y + 18;
        PlaceInfluence(influenceDivider, InfluencePaddingX, y, contentWidth, 1); y += 19;
        PlaceInfluence(influenceBody.rectTransform, InfluencePaddingX, y, contentWidth, c.y);
        influenceRoot.sizeDelta = new Vector2(contentWidth + InfluencePaddingX * 2, y + c.y + InfluencePaddingY);
        influenceRoot.pivot = new Vector2(0, 1);
        influenceRoot.gameObject.SetActive(true); transform.SetAsLastSibling();
        var center = Center(target);
        influenceRoot.position = new Vector3(center.x + infoOffset.x, center.y + infoOffset.y, influenceRoot.position.z);
        SkillTooltipCardLayout.KeepOnScreen(influenceRoot);
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
