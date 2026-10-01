using System;
using TMPro;
using UnityEngine;

/// <summary>실제 홍보 상태를 변경하지 않고 공통 화면에 단계별 예시를 표시한다.</summary>
public sealed class TutorialPublicityExplanationView : MonoBehaviour
{
    [Serializable]
    public sealed class Step
    {
        public int panelIndex;
        public string cost, available, count, totalCost, caption, influence, state;
        public GameObject highlight;
        public bool showFill;
        public Vector2 fillAnchorMin, fillAnchorMax, handleAnchorMin, handleAnchorMax;
    }

    [SerializeField] private GameObject sharedPanel;
    [SerializeField] private TMP_Text costText, availableText, countText, totalCostText;
    [SerializeField] private TMP_Text captionText, influenceText, stateText;
    [SerializeField] private RectTransform fill, handle;
    [SerializeField] private UnityEngine.UI.Slider progressSlider;
    [SerializeField] private Step[] steps = Array.Empty<Step>();

    public void ShowStep(int panelIndex)
    {
        Hide();
        foreach (var step in steps)
        {
            if (step == null || step.panelIndex != panelIndex) continue;
            if (sharedPanel == null) return;
            SetText(costText, step.cost);
            SetText(availableText, step.available);
            SetText(countText, step.count);
            SetText(totalCostText, step.totalCost);
            SetText(captionText, step.caption);
            SetText(influenceText, step.influence);
            SetText(stateText, step.state);
            if (fill != null)
            {
                fill.gameObject.SetActive(step.showFill);
                fill.anchorMin = step.fillAnchorMin;
                fill.anchorMax = step.fillAnchorMax;
            }
            if (handle != null)
            {
                handle.anchorMin = step.handleAnchorMin;
                handle.anchorMax = step.handleAnchorMax;
            }
            if (progressSlider != null)
            {
                int.TryParse(step.available, out int available);
                int.TryParse(step.count, out int count);
                progressSlider.minValue = 0;
                progressSlider.maxValue = Mathf.Max(1, available);
                progressSlider.SetValueWithoutNotify(count);
            }
            if (step.highlight != null) step.highlight.SetActive(true);
            sharedPanel.SetActive(true);
            return;
        }
    }

    public void Hide()
    {
        foreach (var step in steps)
            if (step != null && step.highlight != null) step.highlight.SetActive(false);
        if (sharedPanel != null) sharedPanel.SetActive(false);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
