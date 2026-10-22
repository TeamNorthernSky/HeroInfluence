using TMPro;
using UnityEngine;

/// <summary>검정 로딩 화면에서 글자별 높이와 푸른 글로우를 순차적으로 움직인다.</summary>
public sealed class SceneLoadingLabel : MonoBehaviour
{
    private RectTransform[] letters;
    private TMP_Text[] glows;
    private Material glowMaterial;
    public void Initialize(Transform parent)
    {
        transform.SetParent(parent, false);
        var root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
        root.sizeDelta = new Vector2(420, 100);
        const string text = "Loading...";
        var widths = new float[text.Length];
        letters = new RectTransform[text.Length];
        glows = new TMP_Text[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            var host = new GameObject("Letter " + i, typeof(RectTransform));
            host.transform.SetParent(root, false);
            var rect = (RectTransform)host.transform;
            rect.sizeDelta = new Vector2(45, 90);
            rect.anchoredPosition = new Vector2((i - (text.Length - 1) * .5f) * 28, 0);
            letters[i] = rect;
            var glow = CreateGlyph(rect, text[i], new Color(.12f, .45f, 1f, 1));
            if (glowMaterial == null)
            {
                glowMaterial = new Material(glow.fontSharedMaterial);
                glowMaterial.SetFloat("_OutlineWidth", .45f);
                glowMaterial.SetFloat("_OutlineSoftness", .5f);
                glowMaterial.SetColor("_OutlineColor", new Color(.08f, .35f, 1f, 1));
                glowMaterial.SetFloat("_FaceDilate", .15f);
            }
            glow.fontSharedMaterial = glowMaterial;
            glows[i] = glow;
            var face = CreateGlyph(rect, text[i], Color.white);
            widths[i] = face.GetPreferredValues(text[i].ToString()).x + 3;
        }
        float total = 0;
        foreach (float width in widths) total += width;
        float x = -total * .5f;
        for (int i = 0; i < letters.Length; i++)
        {
            letters[i].anchoredPosition = new Vector2(x + widths[i] * .5f, 0);
            x += widths[i];
        }
    }
    private static TMP_Text CreateGlyph(RectTransform parent, char value, Color color)
    {
        var go = new GameObject("Glyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 42;
        label.alignment = TextAlignmentOptions.Center;
        label.text = value.ToString();
        label.color = color;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        var rect = label.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return label;
    }
    public void RenderAnimation(float time)
    {
        if (letters == null) return;
        for (int i = 0; i < letters.Length; i++)
        {
            float phase = Mathf.Repeat(time * .65f - i * .075f, 1f);
            float wave = phase < .4f ? Mathf.Sin(phase / .4f * Mathf.PI) : 0;
            var position = letters[i].anchoredPosition;
            position.y = wave * 16;
            letters[i].anchoredPosition = position;
            glows[i].alpha = Mathf.Lerp(.12f, 1f, wave);
        }
    }
    private void Update() => RenderAnimation(Time.unscaledTime);
    private void OnDestroy()
    {
        if (glowMaterial == null) return;
        if (Application.isPlaying) Destroy(glowMaterial); else DestroyImmediate(glowMaterial);
    }
}
