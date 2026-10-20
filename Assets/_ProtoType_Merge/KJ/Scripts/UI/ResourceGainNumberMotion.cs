using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Display-only resource count changes with signed notices below the top bar.</summary>
public sealed class ResourceGainNumberMotion
{
    private TMP_Text label;
    private Color originalColor;
    private int target;
    private double displayed, start;
    private float elapsed, progress;
    private bool initialized, active;
    private const float CountDuration = .9f;
    private const float NoticeDuration = 1.6f;
    private readonly List<ChangeNotice> notices = new List<ChangeNotice>();

    private sealed class ChangeNotice
    {
        public RectTransform rect;
        public CanvasGroup group;
        public float age;
    }

    public void Set(TMP_Text text, int value)
    {
        if (label != text)
        {
            Reset();
            label = text;
        }
        if (label == null) return;
        if (!initialized)
        {
            originalColor = label.color;
            initialized = true;
            Snap(value);
            return;
        }
        if (value == target) return;
        if (!label.isActiveAndEnabled)
        {
            Snap(value);
            return;
        }
        ShowChange((long)value - target);
        start = displayed;
        target = value;
        elapsed = progress = 0;
        active = true;
        Render(0);
    }

    public void Advance(float delta)
    {
        AdvanceNotices(Mathf.Max(0f, delta));
        if (!active || label == null) return;
        if (!label.isActiveAndEnabled) { Snap(target); return; }
        elapsed += Mathf.Max(0, delta);
        var motion = Motion;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(CountDuration, motion.duration));
        progress = Mathf.Max(progress, motion.Evaluate(t));
        displayed = start + ((double)target - start) * progress;
        if (t >= 1) Snap(target);
        else Render(t);
    }

    public void Reset()
    {
        foreach (var notice in notices)
            RemoveNotice(notice);
        notices.Clear();
        if (label != null && initialized) Snap(target);
        active = initialized = false;
        label = null;
    }

    private void ShowChange(long amount)
    {
        var canvas = label.canvas != null ? label.canvas.rootCanvas : null;
        if (canvas == null || amount == 0) return;
        if (notices.Count >= 4)
        {
            RemoveNotice(notices[0]);
            notices.RemoveAt(0);
        }
        var go = new GameObject("ResourceChange", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.layer = label.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(112f, 36f);
        var background = go.GetComponent<Image>();
        background.color = new Color(.025f, .055f, .12f, .94f);
        background.raycastTarget = false;
        var textGo = new GameObject("Amount", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.layer = go.layer;
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(rect, false);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.font = label.font;
        text.fontSharedMaterial = label.fontSharedMaterial;
        text.fontSize = 26f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 26f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = (amount > 0 ? "+" : "-") + Math.Abs(amount).ToString("N0");
        text.color = amount > 0 ? new Color32(106, 255, 165, 255) : new Color32(255, 126, 126, 255);
        var group = go.GetComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        notices.Add(new ChangeNotice { rect = rect, group = group });
        AdvanceNotices(0f);
    }

    private void AdvanceNotices(float delta)
    {
        for (int i = notices.Count - 1; i >= 0; i--)
        {
            var notice = notices[i];
            notice.age += delta;
            if (notice.rect == null || label == null || !label.isActiveAndEnabled || notice.age >= NoticeDuration)
            {
                RemoveNotice(notice);
                notices.RemoveAt(i);
                continue;
            }
            var anchor = label.rectTransform;
            var parent = (RectTransform)notice.rect.parent;
            Vector3 bottom = parent.InverseTransformPoint(anchor.TransformPoint(new Vector3(anchor.rect.center.x, anchor.rect.yMin, 0f)));
            notice.rect.localPosition = new Vector3(bottom.x, bottom.y - 52f - i * 40f - notice.age * 12f, 0f);
            notice.group.alpha = 1f - Mathf.Clamp01((notice.age - .8f) / .8f);
        }
    }

    private static void RemoveNotice(ChangeNotice notice)
    {
        if (notice.rect == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(notice.rect.gameObject);
        else UnityEngine.Object.DestroyImmediate(notice.rect.gameObject);
    }

    private BattleUnitBarVisualSettings Settings => label != null
        ? BattleUnitBarVisualSettings.ForScene(label.gameObject.scene) : null;

    private BattleUnitBarVisualSettings.LossMotion Motion => Settings != null && Settings.ipRecovery != null
        ? Settings.ipRecovery : BattleUnitBarVisualSettings.DefaultIpRecovery;

    private void Snap(int value)
    {
        target = value;
        displayed = value;
        active = false;
        label.text = value.ToString("N0");
        label.color = originalColor;
    }

    private void Render(float time)
    {
        var settings = Settings;
        var colors = settings != null && settings.ipRecoveryColors != null
            ? settings.ipRecoveryColors : BattleUnitBarVisualSettings.DefaultIpRecoveryColors;
        Color color = colors.Evaluate(time);
        color.a *= originalColor.a;
        label.color = color;
        label.text = ((int)Math.Round(displayed, MidpointRounding.AwayFromZero)).ToString("N0");
    }
}
