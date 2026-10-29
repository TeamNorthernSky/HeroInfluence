using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>행동 중인 턴 아이콘 옆에 스킬명을 외친 뒤 시전을 재개합니다.</summary>
public sealed class BattleSkillSpeechBubble : MonoBehaviour
{
    // 반격은 스킬명 대신 고정 문구를 외칩니다.
    private const string CounterText = "반격!";

    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text label;
    [SerializeField] private RectTransform bubble;
    [SerializeField] private TurnSlotUI[] slots;
    [Min(0f)] [SerializeField] private float duration = .5f;
    private readonly Vector3[] placementCorners = new Vector3[4];

    private void Awake() => Hide();
    private void OnDisable() => Hide();

    public IEnumerator ShowBeforeCast(BattleCharactor actor, SkillData skill, bool counter = false)
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
        PositionBesideSlot(icon);
        label.text = counter ? CounterText
            : (skill.skillName ?? string.Empty).TrimEnd('+', ' ', '\t', '\r', '\n') + "!";
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
                PositionBesideSlot(icon);
                group.alpha = Mathf.Clamp01(Mathf.Min(elapsed / .04f, (total - elapsed) / .06f));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
        }
        finally { Hide(); }
    }

    // 앵커/피벗이 다른 세로 HUD에서도 실제 슬롯 왼쪽에 붙이고, 표시 중 이동과 확대를 따라갑니다.
    private void PositionBesideSlot(RectTransform icon)
    {
        if (icon == null || bubble == null || !(bubble.parent is RectTransform parent)) return;
        icon.GetWorldCorners(placementCorners);
        Vector3 edge = parent.InverseTransformPoint((placementCorners[0] + placementCorners[1]) * .5f);
        // 현재 턴의 돌출 프레임과 왼쪽 포인터까지 피해 말풍선을 배치합니다.
        var order = icon.GetComponentInParent<TurnOrderUI>();
        if (order != null && order.transform is RectTransform orderRect)
        {
            orderRect.GetWorldCorners(placementCorners);
            edge.x = Mathf.Min(edge.x, parent.InverseTransformPoint(placementCorners[0]).x);
        }
        Vector3 local = bubble.localPosition;
        local.x = edge.x - 8f - bubble.rect.xMax * bubble.localScale.x;
        local.y = edge.y - bubble.rect.center.y * bubble.localScale.y;
        bubble.localPosition = local;

        bubble.GetWorldCorners(placementCorners);
        Vector3 min = parent.InverseTransformPoint(placementCorners[0]);
        Vector3 max = parent.InverseTransformPoint(placementCorners[2]);
        Rect bounds = parent.rect;
        local.x += Mathf.Max(0f, bounds.xMin + 8f - min.x) - Mathf.Max(0f, max.x - bounds.xMax + 8f);
        local.y += Mathf.Max(0f, bounds.yMin + 8f - min.y) - Mathf.Max(0f, max.y - bounds.yMax + 8f);
        bubble.localPosition = local;
    }

    private void Hide()
    {
        if (group != null) group.alpha = 0f;
        if (bubble != null) bubble.localScale = Vector3.one;
    }
}
