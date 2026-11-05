using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Keeps words together by reducing only the spacing on a slightly overfull line.
/// The original TMP text remains unchanged. Attach to a TextMeshPro UI object.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(TextMeshProUGUI))]
[AddComponentMenu("UI/Compressible Word Wrap")]
public sealed class CompressibleWordWrap : MonoBehaviour, ITextPreprocessor
{
    [SerializeField, Range(0f, 0.2f)]
    [Tooltip("글자 사이를 좁힐 수 있는 최대 거리. 글씨 크기의 비율 (0.08 = 8%).")]
    private float maxSpacingReduction = 0.20f;
    
    private TMP_Text target;
    private TextMeshProUGUI measure;
    private GameObject measurementRoot;
    private bool originalAutoSize, originalWrapping, originalRichText;
    private bool installed;
    private float lastWidth;
    private static readonly string[] LayoutTags =
    {
        "<cspace", "<nobr", "<width", "<margin", "<indent", "<line-indent", "<pos", "<space",
        "<align", "<rotate", "<voffset", "<page", "<style", "<noparse", "<mspace"
    };

    private void OnEnable()
    {
        target = GetComponent<TMP_Text>();
        if (target.textPreprocessor != null && target.textPreprocessor != this)
        {
            Debug.LogWarning("CompressibleWordWrap: 다른 텍스트 전처리 기능이 이미 연결되어 있습니다.", this);
            return;
        }
        originalAutoSize = target.enableAutoSizing;
        originalWrapping = target.enableWordWrapping;
        originalRichText = target.richText;
        target.enableAutoSizing = false;
        target.enableWordWrapping = true;
        target.richText = true;
        target.textPreprocessor = this;
        installed = true;
        Refresh();
    }

    private void OnDisable()
    {
        if (installed && target != null && target.textPreprocessor == this)
        {
            target.textPreprocessor = null;
            target.enableAutoSizing = originalAutoSize;
            target.enableWordWrapping = originalWrapping;
            target.richText = originalRichText;
            target.SetAllDirty();
        }
        installed = false;
        if (measurementRoot != null)
        {
            if (Application.isPlaying) Destroy(measurementRoot);
            else DestroyImmediate(measurementRoot);
        }
        measurementRoot = null;
        measure = null;
    }

    private void OnValidate() { Refresh(); }
    private void OnRectTransformDimensionsChange() { Refresh(); }

    private void LateUpdate()
    {
        if (target != null && !Mathf.Approximately(lastWidth, AvailableWidth())) Refresh();
    }

    /// <summary>Call after changing layout settings from code if an immediate rebuild is needed.</summary>
    public void Refresh()
    {
        if (!installed || target == null) return;
        lastWidth = AvailableWidth();
        target.havePropertiesChanged = true;
        target.SetAllDirty();
    }

    private float AvailableWidth()
    {
        return target.rectTransform.rect.width - target.margin.x - target.margin.z;
    }

    public string PreprocessText(string source)
    {
        float width = AvailableWidth();
        if (string.IsNullOrEmpty(source) || width <= 0f || target.font == null || target.isRightToLeftText)
            return source;
        // These tags own line layout/spacing themselves. Do not nest incompatible controls.
        if (HasLayoutTags(source)) return source;
        EnsureMeasurement();
        measure.font = target.font;
        measure.fontSharedMaterial = target.fontSharedMaterial;
        measure.fontSize = target.fontSize;
        measure.fontStyle = target.fontStyle;
        measure.fontWeight = target.fontWeight;
        measure.characterSpacing = target.characterSpacing;
        measure.wordSpacing = target.wordSpacing;
        measure.enableKerning = target.enableKerning;
        measure.richText = originalRichText;
        measure.parseCtrlCharacters = target.parseCtrlCharacters;
        measure.spriteAsset = target.spriteAsset;
        measure.styleSheet = target.styleSheet;
        TMP_TextInfo info = measure.GetTextInfo(source);
        if (info.characterCount == 0) return source;

        var output = new StringBuilder(source.Length + 128);
        int start = 0;
        int rawStart = 0;
        while (start < info.characterCount)
        {
            int paragraphEnd = start;
            while (paragraphEnd < info.characterCount && !IsNewline(info.characterInfo[paragraphEnd].character))
                paragraphEnd++;
            if (start == paragraphEnd)
            {
                int end = RawEnd(info, paragraphEnd);
                output.Append(source, rawStart, end - rawStart);
                rawStart = end;
                start++;
                continue;
            }

            int lineEnd = start;
            int scan = start;
            while (scan < paragraphEnd)
            {
                // A token includes preceding spaces and the next whole word.
                int tokenEnd = scan;
                while (tokenEnd < paragraphEnd && IsBreakSpace(info.characterInfo[tokenEnd].character)) tokenEnd++;
                while (tokenEnd < paragraphEnd && !IsBreakSpace(info.characterInfo[tokenEnd].character)) tokenEnd++;
                int visibleEnd = tokenEnd;
                while (visibleEnd > start && IsBreakSpace(info.characterInfo[visibleEnd - 1].character)) visibleEnd--;
                if (Fits(info, start, visibleEnd, width))
                {
                    lineEnd = tokenEnd;
                    scan = tokenEnd;
                    continue;
                }
                if (lineEnd == start)
                {
                    // An entire word wider than the box must split; never compress without a limit.
                    lineEnd = start + 1;
                    while (lineEnd < tokenEnd && NaturalWidth(info, start, lineEnd + 1) <= width) lineEnd++;
                }
                break;
            }

            int contentEnd = lineEnd;
            while (contentEnd > start && IsBreakSpace(info.characterInfo[contentEnd - 1].character)) contentEnd--;
            float excess = NaturalWidth(info, start, contentEnd) - width;
            float spacing = contentEnd - start > 1 ? Mathf.Max(0f, excess + 0.05f) / (contentEnd - start - 1) : 0f;
            int rawEnd = RawEnd(info, lineEnd - 1);
            output.Append("<nobr>");
            if (spacing > 0f) output.Append("<cspace=-").Append(spacing.ToString("0.####", CultureInfo.InvariantCulture)).Append('>');
            output.Append(source, rawStart, rawEnd - rawStart);
            if (spacing > 0f) output.Append("</cspace>");
            output.Append("</nobr>");
            rawStart = rawEnd;
            start = lineEnd;
            if (start < paragraphEnd)
            {
                // Consume the separator at a new line, preserving all non-space source characters/tags.
                int spaceEnd = start;
                while (spaceEnd < paragraphEnd && IsBreakSpace(info.characterInfo[spaceEnd].character)) spaceEnd++;
                if (spaceEnd > start)
                {
                    for (int i = start; i < spaceEnd; i++)
                    {
                        // Preserve tags between whitespace characters, including closing style tags.
                        int spaceIndex = info.characterInfo[i].index;
                        output.Append(source, rawStart, spaceIndex - rawStart);
                        rawStart = RawEnd(info, i);
                    }
                    start = spaceEnd;
                }
                output.Append('\n');
            }
        }
        output.Append(source, rawStart, source.Length - rawStart);
        return output.ToString();
    }

    private bool Fits(TMP_TextInfo info, int start, int end, float width)
    {
        float limit = target.fontSize * Mathf.Clamp(maxSpacingReduction, 0f, 0.2f);
        return NaturalWidth(info, start, end) - Mathf.Max(0, end - start - 1) * limit <= width - 0.05f;
    }

    private static float NaturalWidth(TMP_TextInfo info, int start, int end)
    {
        if (end <= start) return 0f;
        return info.characterInfo[end - 1].xAdvance - info.characterInfo[start].origin;
    }

    private static int RawEnd(TMP_TextInfo info, int index)
    {
        var character = info.characterInfo[index];
        return character.index + character.stringLength;
    }

    private static bool IsNewline(char value) => value == '\r' || value == '\n';
    private static bool IsBreakSpace(char value) => value == ' ' || value == '\t' || value == '\u200B';

    private static bool HasLayoutTags(string source)
    {
        foreach (string tag in LayoutTags)
            if (source.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private void EnsureMeasurement()
    {
        if (measure != null) return;
        measurementRoot = new GameObject("Word wrap measurement", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        measurementRoot.hideFlags = HideFlags.HideAndDontSave;
        // Invisible standalone canvas; it never participates in the user's hierarchy or layout.
        measurementRoot.GetComponent<CanvasGroup>().alpha = 0f;
        measurementRoot.GetComponent<CanvasGroup>().blocksRaycasts = false;
        var child = new GameObject("Text", typeof(RectTransform));
        child.transform.SetParent(measurementRoot.transform, false);
        measure = child.AddComponent<TextMeshProUGUI>();
        measure.rectTransform.sizeDelta = new Vector2(1000000f, 1000000f);
        measure.enableAutoSizing = false;
        measure.enableWordWrapping = false;
        measure.overflowMode = TextOverflowModes.Overflow;
        measure.alignment = TextAlignmentOptions.TopLeft;
        measure.raycastTarget = false;
    }
}

