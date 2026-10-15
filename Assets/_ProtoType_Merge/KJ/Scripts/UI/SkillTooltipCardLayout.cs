using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Layout for the illustrated single-skill and vertical comparison cards.
public static class SkillTooltipCardLayout
{
    private static readonly Color Ink = new Color(0.02f, 0.04f, 0.16f);

    private static void Place(RectTransform rect, RectTransform parent, float x, float y, float w, float h)
    {
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        rect.localScale = Vector3.one;
    }

    private static void Text(TMP_Text text, RectTransform root, float x, float y, float w, float h,
        float size, bool bold, bool centered = false)
    {
        if (text == null) return;
        Place(text.rectTransform, root, x, y, w, h);
        text.gameObject.SetActive(true);
        text.enabled = true;
        text.color = Ink;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = size;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.alignment = centered ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
    }

    private static void Icon(Image icon, RectTransform root, float x, float y, float size)
    {
        if (icon == null) return;
        Place(icon.rectTransform, root, x, y, size, size);
        icon.gameObject.SetActive(icon.sprite != null);
        icon.enabled = icon.sprite != null;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }

    private static TMP_Text Label(RectTransform root, TMP_Text source, string name)
    {
        var child = root.Find(name);
        if (child != null) return child.GetComponent<TMP_Text>();
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(root, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        if (source != null) label.font = source.font;
        return label;
    }

    private static void HidePanelImage(RectTransform panel)
    {
        if (panel != null && panel.TryGetComponent<Image>(out var image)) image.enabled = false;
    }

    public static void Info(RectTransform root, Image icon, TMP_Text name, TMP_Text description,
        RectTransform oldPanel, float extraWidth, float iconDeltaX)
    {
        float width = Mathf.Max(400f, 500f + extraWidth);
        root.sizeDelta = new Vector2(width, width * (2f / 3f));
        HidePanelImage(oldPanel);
        Icon(icon, root, 27f + iconDeltaX, 24f, 82f);
        Text(name, root, 132f, 36f, width - 154f, 65f, 26f, true);
        if (name != null) name.color = Color.white;
        Text(description, root, 32f, 130f, width - 64f, root.sizeDelta.y - 148f, 23f, false);
        IgnoreInput(root);
    }

    private static string SplitEffect(string effect, out string upgrade)
    {
        const string marker = "협회 강화 ";
        effect = effect ?? "";
        int index = effect.LastIndexOf(marker, System.StringComparison.Ordinal);
        upgrade = index >= 0 ? effect.Substring(index).Trim() : "";
        return index >= 0 ? effect.Substring(0, index).TrimEnd() : effect;
    }

    public static void Compare(RectTransform root, Image icon, TMP_Text name, TMP_Text current,
        Image nextIcon, TMP_Text nextName, TMP_Text next, RectTransform oldPanel)
    {
        root.sizeDelta = new Vector2(500f, 646f);
        HidePanelImage(oldPanel);
        if (nextIcon != null) nextIcon.gameObject.SetActive(false);
        if (nextName != null) nextName.gameObject.SetActive(false);
        Icon(icon, root, 43f, 52f, 108f);
        string title = name != null ? name.text : "";
        if (name != null)
            name.text = System.Text.RegularExpressions.Regex.Replace(title, @"\s+(?:기본|\+\d+|Lv\.?\s*\d+)\s*$", "");
        Text(name, root, 170f, 52f, 295f, 48f, 31f, true);
        string currentBody = SplitEffect(current != null ? current.text : "", out string currentUpgrade);
        string nextBody = SplitEffect(next != null ? next.text : "", out string nextUpgrade);
        // Training and other shared callers do not include an association footer.
        if (string.IsNullOrEmpty(currentUpgrade)) currentUpgrade = title;
        if (string.IsNullOrEmpty(nextUpgrade)) nextUpgrade = nextName != null ? nextName.text : "";
        if (current != null) current.text = currentBody;
        if (next != null) next.text = nextBody;
        Text(current, root, 40f, 225f, 424f, 102f, 24f, false);
        Text(next, root, 40f, 475f, 424f, 97f, 24f, false);
        var level = Label(root, name, "SkillLevel");
        int number = 1;
        var match = System.Text.RegularExpressions.Regex.Match(currentUpgrade, @"\+(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int enhancement)) number = enhancement + 1;
        else
        {
            match = System.Text.RegularExpressions.Regex.Match(title, @"Lv\.?\s*(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int explicitLevel)) number = explicitLevel;
        }
        level.text = "Lv." + number;
        Text(level, root, 174f, 108f, 130f, 38f, 28f, true, true);
        var currentFooter = Label(root, name, "CurrentUpgrade");
        currentFooter.text = currentUpgrade;
        Text(currentFooter, root, 36f, 335f, 430f, 42f, 25f, true, true);
        var nextFooter = Label(root, name, "NextUpgrade");
        nextFooter.text = nextUpgrade;
        Text(nextFooter, root, 36f, 584f, 430f, 42f, 25f, true, true);
        IgnoreInput(root);
    }

    private static void IgnoreInput(RectTransform root)
    {
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
    }

    public static void KeepOnScreen(RectTransform root)
    {
        var canvas = root.GetComponentInParent<Canvas>();
        var area = canvas != null ? canvas.transform as RectTransform : null;
        if (area == null) return;
        var corners = new Vector3[4];
        root.GetWorldCorners(corners);
        var bounds = new Vector3[4];
        area.GetWorldCorners(bounds);
        float x = corners[0].x < bounds[0].x ? bounds[0].x - corners[0].x : 0f;
        if (corners[2].x + x > bounds[2].x) x += bounds[2].x - (corners[2].x + x);
        float y = corners[0].y < bounds[0].y ? bounds[0].y - corners[0].y : 0f;
        if (corners[2].y + y > bounds[2].y) y += bounds[2].y - (corners[2].y + y);
        root.position += new Vector3(x, y, 0f);
    }
}
