using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows the selected training card's comparison in its illustrated number boxes.</summary>
[DisallowMultipleComponent]
public sealed class TrainingCardSelectionView : MonoBehaviour
{
    private GameObject panel;
    private Image statIcon;
    private TMP_Text statText;
    private TMP_Text currentText;
    private TMP_Text nextText;
    private static readonly Color Ink = new Color32(24, 20, 34, 255);

    private void Awake() => InitializeLayout();

    public void InitializeLayout()
    {
        var rect = (RectTransform)transform;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 239f);
        Place(transform.Find("Category") as RectTransform, 18f, 20f, 64f, 64f);
        Place(transform.Find("TrainingName") as RectTransform, 98f, 17f, 138f, 26f);
        Place(transform.Find("TrainingLevel") as RectTransform, 249f, 17f, 59f, 26f);
        for (int i = 0; i < 3; i++)
            Place(transform.Find("Cell" + i) as RectTransform, 129f + i * 43f, 53f, 40f, 36f);
        SetHeaderText("TrainingName", 18f, TextAlignmentOptions.MidlineLeft);
        SetHeaderText("TrainingLevel", 18f, TextAlignmentOptions.MidlineRight);
    }

    public void Show(Sprite icon, TMP_FontAsset font, string name, float current, float? next, string reason)
    {
        InitializeLayout();
        if (panel == null) Build(font);
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        // The shared icon library can still point to the old status folder.
        if (icon == null)
        {
            string status = name == "공격력" ? "ATK" : name == "생명력" ? "HP" : null;
            if (status != null) icon = Resources.Load<Sprite>("Status/UI_icon_" + status);
        }
        statIcon.sprite = icon;
        statIcon.enabled = icon != null;
        statText.text = string.IsNullOrEmpty(reason) ? name : reason;
        currentText.text = current.ToString("0.##");
        nextText.text = next.HasValue ? next.Value.ToString("0.##") : "—";
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void OnDisable() => Hide();

    private void Build(TMP_FontAsset font)
    {
        panel = new GameObject("SelectedTrainingDetails", typeof(RectTransform));
        panel.layer = gameObject.layer;
        var root = (RectTransform)panel.transform;
        root.SetParent(transform, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        var iconGo = new GameObject("StatIcon", typeof(RectTransform), typeof(Image));
        iconGo.layer = gameObject.layer;
        statIcon = iconGo.GetComponent<Image>();
        statIcon.rectTransform.SetParent(root, false);
        Place(statIcon.rectTransform, 20f, 119f, 32f, 32f);
        statIcon.preserveAspect = true;
        statIcon.raycastTarget = false;
        statText = CreateText("StatName", font, 60f, 116f, 246f, 39f, 26f);
        statText.alignment = TextAlignmentOptions.MidlineLeft;
        currentText = CreateText("Current", font, 37f, 166f, 101f, 46f, 32f);
        nextText = CreateText("Next", font, 190f, 166f, 101f, 46f, 32f);
    }

    private TMP_Text CreateText(string name, TMP_FontAsset font, float x, float y, float width, float height, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        var text = go.GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(panel.transform, false);
        Place(text.rectTransform, x, y, width, height);
        text.font = font;
        text.fontStyle = FontStyles.Bold;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = size;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Ink;
        text.raycastTarget = false;
        return text;
    }

    private void SetHeaderText(string name, float size, TextAlignmentOptions alignment)
    {
        var child = transform.Find(name);
        var text = child != null ? child.GetComponent<TMP_Text>() : null;
        if (text == null) return;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = size;
        text.alignment = alignment;
        text.color = Ink;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
