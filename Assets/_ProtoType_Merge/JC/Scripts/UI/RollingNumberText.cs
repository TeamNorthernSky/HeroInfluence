using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>정수 하나의 표시만 담당합니다. 실제 데이터나 보상 이벤트를 변경하지 않습니다.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(TextMeshProUGUI))]
public sealed class RollingNumberText : MonoBehaviour
{
    public enum HorizontalAlignment { Left, Center, Right }

    [Tooltip("씬의 숫자 연출 설정입니다. 미연결 시 0.8초 감속을 사용합니다. 전투처럼 외부에서 진행률을 전달할 때는 필요 없습니다.")]
    public NumberRollSettings settings;
    [Tooltip("숫자의 가로 정렬입니다. 표시 영역은 기존 TMP의 RectTransform을 사용합니다. 재생 시작 시 적용됩니다.")]
    public HorizontalAlignment alignment = HorizontalAlignment.Right;
    [Tooltip("켜면 정수에 천 단위 쉼표를 표시합니다. 쉼표는 회전하지 않습니다. 소수·단위·이름은 별도 TMP에 표시합니다.")]
    public bool useThousandsSeparator;

    private sealed class Cell { public TextMeshProUGUI current, next; }
    private readonly List<Cell> cells = new List<Cell>();
    private TextMeshProUGUI source;
    private RectTransform mask;
    private bool sourceEnabled, initialized, running, manual;
    private long target;
    private decimal startPosition, position;
    private float elapsed, duration, progress, cellWidth, height;
    private AnimationCurve curve;
    private int capacity;
    public bool IsAnimating => running;
    public long DisplayedValue { get; private set; }
    public long TargetValue => target;
    public decimal DisplayedPosition => position;

    private void Update() { if (running && !manual) Tick(Time.unscaledDeltaTime); }

    /// <summary>최초 표시, 불러오기, 대상 교체 시 연출 없이 확정값을 표시합니다.</summary>
    public void SetValue(long value)
    {
        target = value; startPosition = position = value; running = manual = false;
        Prepare(value, value); Draw(value, value, 0);
    }

    /// <summary>현재 표시 위치에서 재생합니다. 최초 호출은 즉시 표시하고 같은 목표는 재시작하지 않습니다.</summary>
    public void AnimateTo(long value)
    {
        if (!initialized || !isActiveAndEnabled) { SetValue(value); return; }
        if (value == target && (running || position == value)) return;
        startPosition = position; target = value; elapsed = progress = 0; manual = false;
        duration = settings ? Mathf.Max(0, settings.duration) : .8f;
        curve = settings
            ? (settings.progressCurve != null ? new AnimationCurve(settings.progressCurve.keys) : null)
            : new AnimationCurve(new Keyframe(0, 0, 2, 2), new Keyframe(1, 1, 0, 0));
        long reserveStart = (long)(position >= 0 ? decimal.Ceiling(position) : decimal.Floor(position));
        Prepare(reserveStart, target); running = position != target;
        if (duration <= 0 || !running) Complete(); else RenderPosition();
    }

    /// <summary>외부 제어용입니다. 시작과 종료 자릿수를 확보하고 SetProgress로 진행률을 받습니다.</summary>
    public void BeginManual(long from, long to)
    {
        startPosition = position = from; target = to; elapsed = progress = 0;
        manual = true; running = from != to;
        Prepare(from, to); Draw(from, from, 0);
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (!running || manual || !isActiveAndEnabled) return;
        elapsed += Mathf.Max(0, unscaledDeltaTime);
        float t = duration > 0 ? Mathf.Clamp01(elapsed / duration) : 1;
        ApplyProgress(t >= 1 ? 1 : (curve != null ? curve.Evaluate(t) : t));
    }

    /// <summary>BeginManual 이후 외부 진행률입니다. 0~1로 제한하고 역행을 막습니다.</summary>
    public void SetProgress(float normalizedProgress)
    {
        if (initialized && manual) ApplyProgress(normalizedProgress);
    }

    private void ApplyProgress(float value)
    {
        progress = Mathf.Max(progress, Mathf.Clamp01(value));
        position = startPosition + ((decimal)target - startPosition) * (decimal)progress;
        RenderPosition();
        if (progress >= 1) running = false;
    }

    private void RenderPosition()
    {
        bool increasing = target >= startPosition;
        long a = (long)(increasing ? decimal.Floor(position) : decimal.Ceiling(position));
        long b = a == target ? a : (increasing ? a + 1 : a - 1);
        float fraction = (float)Math.Abs(position - a);
        Draw(a, b, fraction);
    }

    /// <summary>현재 확정 정수에서 멈춥니다. 목표값으로 이동하지 않습니다.</summary>
    public void Stop() { if (initialized) SetValue(DisplayedValue); }
    /// <summary>확인 버튼 등에서 호출하여 목표값으로 즉시 확정합니다.</summary>
    public void Complete() { if (initialized) { position = target; running = false; progress = 1; Draw(target, target, 0); } }

    private string Format(long value) => value.ToString(useThousandsSeparator ? "N0" : "0", CultureInfo.InvariantCulture);

    private void Prepare(long from, long to)
    {
        if (!source) source = GetComponent<TextMeshProUGUI>();
        if (!initialized) { sourceEnabled = source.enabled; initialized = true; }
        if (!mask)
        {
            var go = new GameObject("RollingNumberMask", typeof(RectTransform), typeof(RectMask2D));
            go.layer = gameObject.layer; mask = (RectTransform)go.transform;
            mask.SetParent(transform, false); mask.anchorMin = Vector2.zero; mask.anchorMax = Vector2.one;
            mask.offsetMin = mask.offsetMax = Vector2.zero;
        }
        mask.gameObject.SetActive(isActiveAndEnabled);
        capacity = Math.Max(Format(from).Length, Format(to).Length);
        height = Mathf.Max(1, source.rectTransform.rect.height);
        float size = source.fontSize;
        cellWidth = MeasureCellWidth(source);
        float fit = Mathf.Min(1, Mathf.Max(1, source.rectTransform.rect.width) / (capacity * cellWidth), height / Mathf.Max(1, size * 1.3f));
        size *= fit; cellWidth *= fit;
        while (cells.Count < capacity)
        {
            int i = cells.Count;
            cells.Add(new Cell { current = CreateGlyph("Digit" + i), next = CreateGlyph("Next" + i) });
        }
        for (int i = 0; i < cells.Count; i++)
        {
            foreach (var glyph in new[] { cells[i].current, cells[i].next })
            {
                glyph.gameObject.SetActive(i < capacity);
                glyph.font = source.font; glyph.fontSharedMaterial = source.fontSharedMaterial;
                glyph.fontStyle = source.fontStyle; glyph.color = source.color; glyph.fontSize = size;
                glyph.rectTransform.sizeDelta = new Vector2(cellWidth, height);
            }
        }
        source.enabled = !isActiveAndEnabled && sourceEnabled;
    }

    public static float MeasureCellWidth(TMP_Text template)
    {
        float width = 1;
        foreach (char c in "0123456789,-") width = Mathf.Max(width, template.GetPreferredValues(c.ToString(), Mathf.Infinity, Mathf.Infinity).x);
        return width + template.fontSize * .08f;
    }

    private TextMeshProUGUI CreateGlyph(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer; go.transform.SetParent(mask, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.enableAutoSizing = false; text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false;
        return text;
    }

    private string Pad(string value) => alignment == HorizontalAlignment.Left ? value.PadRight(capacity) : value.PadLeft(capacity);

    private void Draw(long value, long next, float fraction)
    {
        DisplayedValue = value;
        if (!isActiveAndEnabled) source.text = Format(value);
        string a = Pad(Format(value)), b = Pad(Format(next));
        float areaWidth = Mathf.Max(1, source.rectTransform.rect.width);
        float left = alignment == HorizontalAlignment.Left ? -areaWidth * .5f
            : alignment == HorizontalAlignment.Right ? areaWidth * .5f - capacity * cellWidth : -capacity * cellWidth * .5f;
        for (int i = 0; i < capacity; i++)
        {
            var cell = cells[i];
            bool roll = fraction > 0 && a[i] != b[i]
                && (char.IsDigit(a[i]) || a[i] == ' ') && (char.IsDigit(b[i]) || b[i] == ' ');
            cell.current.SetText(a[i].ToString()); cell.next.SetText(b[i].ToString());
            cell.next.enabled = roll;
            float x = left + (i + .5f) * cellWidth;
            cell.current.rectTransform.anchoredPosition = new Vector2(x, roll ? fraction * height : 0);
            cell.next.rectTransform.anchoredPosition = new Vector2(x, (fraction - 1) * height);
        }
    }

    private void OnEnable()
    {
        if (initialized && source) { source.enabled = false; if (mask) mask.gameObject.SetActive(true); }
    }
    private void OnDisable()
    {
        if (!initialized) return;
        Complete(); // 닫힌 화면의 이전 연출을 다음 표시로 넘기지 않습니다.
        if (mask) mask.gameObject.SetActive(false);
        if (source) { source.text = Format(target); source.enabled = sourceEnabled; }
    }
    private void OnDestroy() => Release();

    /// <summary>생성물을 정리하고 마지막 확정값을 원본 TMP로 돌려놓습니다. 다시 사용할 수 있습니다.</summary>
    public void Release()
    {
        if (source && initialized) { source.text = Format(DisplayedValue); source.enabled = sourceEnabled; }
        if (mask) { mask.gameObject.SetActive(false); if (Application.isPlaying) Destroy(mask.gameObject); else DestroyImmediate(mask.gameObject); }
        mask = null; cells.Clear(); initialized = running = manual = false;
    }
}
