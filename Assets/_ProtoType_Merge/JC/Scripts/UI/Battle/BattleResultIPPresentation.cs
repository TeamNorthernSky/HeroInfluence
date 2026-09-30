using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>전투 결과 카드 전용 표시 상태입니다. 재화와 저장 데이터는 변경하지 않습니다.</summary>
public sealed class BattleResultIPPresentation : IDisposable
{
    private readonly TMP_Text source;
    private readonly bool sourceEnabled;
    private readonly BattleResultIPGainSettings settings;
    private readonly GameObject root;
    private readonly RollingNumberText currentRow, gainRow;
    private readonly TextMeshProUGUI echo;
    private readonly long start, target, gain;
    private readonly float height;
    private float elapsed, progress, echoElapsed, delayElapsed;
    private bool pending = true, completed, echoActive;
    public bool IsComplete => completed;
    public bool HasStarted => !pending;
    public long DisplayedCurrent { get; private set; }
    public long DisplayedRemaining { get; private set; }

    public BattleResultIPPresentation(TMP_Text template, float before, float after, BattleResultIPGainSettings config)
    {
        source = template; sourceEnabled = template.enabled; settings = config;
        start = (long)Math.Round(before, MidpointRounding.ToEven);
        target = (long)Math.Round(after, MidpointRounding.ToEven);
        gain = Math.Max(0, target - start);
        root = new GameObject("IPGainPresentation", typeof(RectTransform));
        root.layer = source.gameObject.layer;
        var rect = (RectTransform)root.transform;
        rect.SetParent(source.transform, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        height = Mathf.Max(1f, source.rectTransform.rect.height);
        float width = Mathf.Max(1f, source.rectTransform.rect.width);
        float fontSize = source.fontSize;
        float digitWidth = RollingNumberText.MeasureCellWidth(source);
        int leftCount = Math.Max(start.ToString(CultureInfo.InvariantCulture).Length, target.ToString(CultureInfo.InvariantCulture).Length);
        int rightCount = gain.ToString(CultureInfo.InvariantCulture).Length;
        float currentGap = fontSize * Mathf.Max(0, config.currentToPlusGap);
        float gainGap = fontSize * Mathf.Max(0, config.plusToGainGap);
        float plusWidth = Mathf.Max(1, source.GetPreferredValues("+", Mathf.Infinity, Mathf.Infinity).x);
        float required = digitWidth * (leftCount + rightCount) + plusWidth + currentGap + gainGap;
        float fit = Mathf.Min(1f, width / required, height / Mathf.Max(1f, fontSize * 1.3f));
        digitWidth *= fit; currentGap *= fit; gainGap *= fit; plusWidth *= fit; fontSize *= fit;
        float left = -required * fit * .5f;
        currentRow = CreateRow(rect, "Current", leftCount, left, digitWidth, fontSize, config.currentColor, RollingNumberText.HorizontalAlignment.Right);
        float plusLeft = left + digitWidth * leftCount + currentGap;
        var plus = CreateText(rect, "Plus", plusWidth, fontSize, config.gainColor);
        plus.rectTransform.anchoredPosition = new Vector2(plusLeft + plusWidth * .5f, 0); plus.text = "+";
        gainRow = CreateRow(rect, "Remaining", rightCount, plusLeft + plusWidth + gainGap, digitWidth, fontSize, config.gainColor, RollingNumberText.HorizontalAlignment.Left);
        currentRow.BeginManual(start, target);
        gainRow.BeginManual(gain, 0);
        echo = CreateText(rect, "CompletionEcho", digitWidth * leftCount, fontSize, config.currentColor);
        echo.rectTransform.anchoredPosition = new Vector2(left + digitWidth * leftCount * .5f, 0);
        // 같은 자릿수 폭으로 최종 숫자를 복제합니다. 원래 숫자는 확대하지 않습니다.
        echo.text = "<mspace=" + digitWidth.ToString(CultureInfo.InvariantCulture) + ">" + target.ToString(CultureInfo.InvariantCulture) + "</mspace>";
        echo.gameObject.SetActive(false);
        source.enabled = false;
        Draw(0);
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, float width, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = source.gameObject.layer; go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = source.font; text.fontSharedMaterial = source.fontSharedMaterial; text.fontStyle = source.fontStyle;
        text.fontSize = size; text.enableAutoSizing = false; text.color = color;
        text.alignment = TextAlignmentOptions.Center; text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false;
        text.rectTransform.sizeDelta = new Vector2(width, height);
        return text;
    }

    private RollingNumberText CreateRow(Transform parent, string name, int count, float x, float width, float size, Color color, RollingNumberText.HorizontalAlignment alignment)
    {
        var text = CreateText(parent, name, count * width, size, color);
        text.rectTransform.anchoredPosition = new Vector2(x + count * width * .5f, 0);
        var row = text.gameObject.AddComponent<RollingNumberText>();
        row.alignment = alignment;
        return row;
    }

    public void Tick(float delta)
    {
        if (completed) return;
        if (!settings) { CompleteImmediately(); return; }
        if (pending)
        {
            RestorePendingDisplay();
            pending = false;
            if (gain == 0 || settings.duration <= 0) { CompleteImmediately(); return; }
        }
        delta = Mathf.Max(0, delta);
        if (echoActive)
        {
            echoElapsed += delta;
            float t = settings.echoDuration > 0 ? Mathf.Clamp01(echoElapsed / settings.echoDuration) : 1;
            echo.rectTransform.localScale = Vector3.one * Mathf.Lerp(1, Mathf.Max(1, settings.echoScale), t);
            var c = settings.currentColor; c.a *= Mathf.Clamp01(settings.echoAlpha) * (1 - t); echo.color = c;
            if (t >= 1) { echo.gameObject.SetActive(false); completed = true; }
            return;
        }
        // EXP·랭크 단계 완료 후 호출되며, 지연 경계를 넘긴 프레임의 남은 시간만 재생한다.
        float delayRemaining = Mathf.Max(0, settings.startDelay - delayElapsed);
        float waited = Mathf.Min(delta, delayRemaining);
        delayElapsed += waited;
        delta -= waited;
        if (delayElapsed < settings.startDelay) return;
        elapsed += delta;
        float time = settings.duration > 0 ? Mathf.Clamp01(elapsed / settings.duration) : 1;
        progress = time >= 1 ? 1 : Mathf.Max(progress, time <= 0 ? 0 : Mathf.Clamp01(settings.progressCurve != null ? settings.progressCurve.Evaluate(time) : time));
        Draw(gain * (double)progress);
        if (time >= 1)
        {
            BeginCompletionEcho();
        }
    }

    // 공용 컴포넌트는 숨겨질 때 최종화한다. 아직 시작하지 않은 전투 표시는 다시 준비한다.
    public void RestorePendingDisplay()
    {
        if (!pending || completed) return;
        currentRow.BeginManual(start, target);
        gainRow.BeginManual(gain, 0);
        Draw(0);
    }

    public void SkipToCompletionEcho()
    {
        if (completed || echoActive) return;
        RestorePendingDisplay();
        pending = false;
        Draw(gain);
        BeginCompletionEcho();
    }

    private void BeginCompletionEcho()
    {
        if (gain == 0 || !settings || settings.echoDuration <= 0) { completed = true; return; }
        echoActive = true; echoElapsed = 0; echo.gameObject.SetActive(true);
        echo.rectTransform.localScale = Vector3.one;
        var c = settings.currentColor; c.a *= Mathf.Clamp01(settings.echoAlpha); echo.color = c;
    }

    private void Draw(double transfer)
    {
        float value = gain > 0 ? (float)(transfer / gain) : 1;
        currentRow.SetProgress(value);
        gainRow.SetProgress(value);
        DisplayedCurrent = currentRow.DisplayedValue;
        DisplayedRemaining = gainRow.DisplayedValue;
    }

    public void CompleteImmediately()
    {
        Draw(gain); echo.gameObject.SetActive(false); completed = true; pending = false;
    }

    public void Dispose()
    {
        if (source) source.enabled = sourceEnabled;
        if (root) { root.SetActive(false); if (Application.isPlaying) UnityEngine.Object.Destroy(root); else UnityEngine.Object.DestroyImmediate(root); }
    }
}
