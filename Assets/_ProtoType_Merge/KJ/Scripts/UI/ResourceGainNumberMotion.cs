using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Display-only resource count changes with signed notices below the top bar.</summary>
public sealed class ResourceGainNumberMotion
{
    private static readonly Dictionary<TMP_Text, ResourceGainNumberMotion> owners = new Dictionary<TMP_Text, ResourceGainNumberMotion>();
    private RewardFlight rewardFlight;

    /// <summary>Temporarily lets arriving reward icons drive this label without delaying the actual economy.</summary>
    public sealed class RewardFlight : IDisposable
    {
        private ResourceGainNumberMotion owner;
        private readonly double initial;
        private readonly Vector3 originalScale;
        private float fraction, age;
        internal RewardFlight(ResourceGainNumberMotion owner)
        {
            this.owner = owner;
            initial = owner.displayed;
            originalScale = owner.label.rectTransform.localScale;
        }
        public void SetProgress(float value)
        {
            if (owner == null) return;
            fraction = Mathf.Max(fraction, Mathf.Clamp01(value));
            owner.displayed = initial + (owner.target - initial) * fraction;
            owner.Render(0f);
            if (fraction >= 1f) Dispose();
        }
        internal void Advance(float delta)
        {
            age += delta;
            if (owner.label != null)
                owner.label.rectTransform.localScale = originalScale *
                    (fraction > 0f ? 1f + Mathf.Abs(Mathf.Sin(age * 24f)) * .1f : 1f);
        }
        public void Dispose()
        {
            if (owner == null) return;
            var motion = owner;
            owner = null;
            motion.rewardFlight = null;
            if (motion.label == null) return;
            motion.label.rectTransform.localScale = originalScale;
            motion.Snap(motion.target);
        }
    }

    public static RewardFlight BeginRewardFlight(TMP_Text text)
    {
        if (text == null || !owners.TryGetValue(text, out var owner) || !owner.initialized) return null;
        owner.rewardFlight?.Dispose();
        owner.active = false;
        owner.rewardFlight = new RewardFlight(owner);
        return owner.rewardFlight;
    }
    private readonly Func<int, string> format;
    private readonly bool showNotices;
    private readonly bool animateColor;
    private readonly Func<Sprite> noticeIcon;

    public ResourceGainNumberMotion(Func<int, string> format = null, bool showNotices = true, bool animateColor = true,
        Func<Sprite> noticeIcon = null)
    {
        this.format = format ?? (value => value.ToString("N0"));
        this.showNotices = showNotices;
        this.animateColor = animateColor;
        this.noticeIcon = noticeIcon;
    }
    private TMP_Text label;
    private Color originalColor;
    private int target;
    private double displayed, start;
    private float elapsed, progress;
    private float countDelay;
    private bool useMoneySpeed;
    private const double MoneyUnitsPerSecond = 1000d;
    private bool initialized, active;
    private const float CountDuration = .9f;
    private const float NoticeDuration = 1.6f;
    private const float NoticeEntranceDuration = .25f;
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
            owners[label] = this;
            Snap(value);
            return;
        }
        if (value == target) return;
        if (rewardFlight != null) { target = value; return; }
        if (!label.isActiveAndEnabled)
        {
            Snap(value);
            return;
        }
        long change = (long)value - target;
        countDelay = showNotices && ShowChange(change) && change > 0 ? NoticeDuration : 0f;
        start = displayed;
        target = value;
        var hud = label.GetComponentInParent<ResourceHUDController>();
        useMoneySpeed = hud != null && hud.GetResourceAnchor(ResourceType.Money) == label.rectTransform;
        elapsed = progress = 0;
        active = true;
        Render(0);
    }

    public void Advance(float delta)
    {
        AdvanceNotices(Mathf.Max(0f, delta));
        if (rewardFlight != null) { rewardFlight.Advance(Mathf.Max(0f, delta)); return; }
        if (!active || label == null) return;
        if (!label.isActiveAndEnabled) { Snap(target); return; }
        float remaining = Mathf.Max(0, delta);
        if (countDelay > 0f)
        {
            float wait = Mathf.Min(countDelay, remaining);
            countDelay -= wait;
            remaining -= wait;
            if (countDelay > 0f) return;
        }
        elapsed += remaining;
        if (useMoneySpeed)
        {
            double step = MoneyUnitsPerSecond * remaining;
            double difference = target - displayed;
            if (Math.Abs(difference) <= step) Snap(target);
            else
            {
                displayed += Math.Sign(difference) * step;
                Render(0f);
            }
            return;
        }
        var motion = Motion;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(CountDuration, motion.duration));
        progress = Mathf.Max(progress, motion.Evaluate(t));
        displayed = start + ((double)target - start) * progress;
        if (t >= 1) Snap(target);
        else Render(t);
    }

    public void Reset()
    {
        rewardFlight?.Dispose();
        if (!ReferenceEquals(label, null) && owners.TryGetValue(label, out var owner) && ReferenceEquals(owner, this))
            owners.Remove(label);
        foreach (var notice in notices)
            RemoveNotice(notice);
        notices.Clear();
        if (label != null && initialized) Snap(target);
        active = initialized = false;
        label = null;
    }

    private bool ShowChange(long amount)
    {
        var canvas = label.canvas != null ? label.canvas.rootCanvas : null;
        if (canvas == null || amount == 0) return false;
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
        Sprite resourceIcon = noticeIcon != null ? noticeIcon() : null;
        var hud = label.GetComponentInParent<ResourceHUDController>();
        if (hud != null)
            foreach (ResourceType resource in new[] { ResourceType.Money, ResourceType.Chip, ResourceType.Crystal, ResourceType.Supply })
                if (hud.GetResourceAnchor(resource) == label.rectTransform)
                {
                    resourceIcon = Sprites.UI.Resource(resource);
                    break;
                }
        if (resourceIcon != null)
        {
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.layer = go.layer;
            var icon = iconObject.GetComponent<Image>();
            icon.rectTransform.SetParent(rect, false);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, .5f);
            icon.rectTransform.pivot = new Vector2(0f, .5f);
            icon.rectTransform.anchoredPosition = new Vector2(4f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(28f, 28f);
            icon.sprite = resourceIcon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }
        var textGo = new GameObject("Amount", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.layer = go.layer;
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(rect, false);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        if (resourceIcon != null) text.rectTransform.offsetMin = new Vector2(34f, 0f);
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
        return true;
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
            float entrance = Mathf.Clamp01(notice.age / NoticeEntranceDuration);
            float eased = 1f - Mathf.Pow(1f - entrance, 3f);
            if (label.GetComponentInParent<ResourceHUDController>() != null)
            {
                Vector3 bottom = parent.InverseTransformPoint(anchor.TransformPoint(new Vector3(anchor.rect.center.x, anchor.rect.yMin, 0f)));
                notice.rect.localPosition = new Vector3(bottom.x,
                    bottom.y - notice.rect.rect.height * .5f - 4f - i * 40f, 0f);
            }
            else
            {
                Vector3 right = parent.InverseTransformPoint(anchor.TransformPoint(new Vector3(anchor.rect.xMax, anchor.rect.center.y, 0f)));
                notice.rect.localPosition = new Vector3(right.x + 64f + 24f * eased, right.y - i * 40f, 0f);
            }
            notice.rect.localScale = Vector3.one;
            notice.group.alpha = entrance * (1f - Mathf.Clamp01((notice.age - .8f) / .8f));
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
        countDelay = 0f;
        label.text = format(value);
        label.color = originalColor;
    }

    private void Render(float time)
    {
        Color color = new Color(0f, 0f, 0f, originalColor.a);
        label.color = animateColor ? color : originalColor;
        label.text = format((int)Math.Round(displayed, MidpointRounding.AwayFromZero));
    }
}
