using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>행동 중인 턴 아이콘 옆에 스킬명을 외친 뒤 시전을 재개합니다.</summary>
public sealed class BattleSkillSpeechBubble : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text label;
    [SerializeField] private RectTransform bubble;
    [SerializeField] private TurnSlotUI[] slots;
    [Min(0f)] [SerializeField] private float duration = .5f;

    private void Awake() => Hide();
    private void OnDisable() => Hide();

    public IEnumerator ShowBeforeCast(BattleCharactor actor, SkillData skill)
    {
        if (actor == null || skill == null || group == null || label == null || bubble == null)
            yield break;
        TurnSlotUI activeSlot = null;
        if (slots != null)
            foreach (var slot in slots)
                if (slot != null && slot.isActiveAndEnabled && slot.DisplayedUnit == actor)
                { activeSlot = slot; break; }
        if (activeSlot == null) yield break;

        var icon = (RectTransform)activeSlot.transform;
        var parent = (RectTransform)bubble.parent;
        Vector3 center = parent.InverseTransformPoint(icon.TransformPoint(icon.rect.center));
        bubble.anchoredPosition = new Vector2(center.x - icon.rect.width * icon.lossyScale.x / parent.lossyScale.x * .5f - 8f, center.y);
        label.text = (skill.skillName ?? string.Empty).TrimEnd('+', ' ', '\t', '\r', '\n') + "!";
        float elapsed = 0f;
        float total = Mathf.Max(0f, duration);
        try
        {
            while (elapsed < total)
            {
                float enter = Mathf.Clamp01(elapsed / .1f);
                float scale = enter < .7f ? Mathf.Lerp(.82f, 1.05f, enter / .7f)
                    : Mathf.Lerp(1.05f, 1f, (enter - .7f) / .3f);
                bubble.localScale = Vector3.one * scale;
                group.alpha = Mathf.Clamp01(Mathf.Min(elapsed / .04f, (total - elapsed) / .06f));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
        }
        finally { Hide(); }
    }

    private void Hide()
    {
        if (group != null) group.alpha = 0f;
        if (bubble != null) bubble.localScale = Vector3.one;
    }
}
