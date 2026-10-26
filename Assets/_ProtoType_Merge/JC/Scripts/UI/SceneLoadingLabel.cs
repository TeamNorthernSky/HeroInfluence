using TMPro;
using UnityEngine;

/// <summary>검정 로딩 화면에 고정된 글자와 푸른 글로우를 표시한다.</summary>
public sealed class SceneLoadingLabel : MonoBehaviour
{
    private Material glowMaterial;
    public void Initialize(Transform parent)
    {
        transform.SetParent(parent, false);
        var root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
        root.sizeDelta = new Vector2(420, 100);
        const string text = "Loading...";
        var widths = new float[text.Length];
        var letters = new RectTransform[text.Length];
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
            glow.alpha = .4f;
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
    private void OnDestroy()
    {
        if (glowMaterial == null) return;
        if (Application.isPlaying) Destroy(glowMaterial); else DestroyImmediate(glowMaterial);
    }
}
