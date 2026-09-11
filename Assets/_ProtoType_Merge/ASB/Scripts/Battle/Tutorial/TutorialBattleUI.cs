using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TutorialBattleUI : MonoBehaviour
{
    [Serializable]
    private sealed class ActionButtonBinding
    {
        public string actionId;
        public Button button;
        public GameObject highlight;
    }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text messageLabel;
    [SerializeField] private List<ActionButtonBinding> actionButtons = new List<ActionButtonBinding>();

    private readonly Dictionary<Button, UnityAction> listeners = new Dictionary<Button, UnityAction>();

    public event Action<string> ActionPerformed;

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    public void Show(string message, string highlightedActionId = null)
    {
        if (messageLabel != null)
        {
            messageLabel.text = message ?? string.Empty;
        }

        string normalized = Normalize(highlightedActionId);
        for (int i = 0; i < actionButtons.Count; i++)
        {
            ActionButtonBinding binding = actionButtons[i];
            if (binding?.highlight != null)
            {
                binding.highlight.SetActive(
                    !string.IsNullOrEmpty(normalized) &&
                    string.Equals(Normalize(binding.actionId), normalized, StringComparison.Ordinal));
            }
        }

        panel?.SetActive(true);
    }

    public void Hide()
    {
        panel?.SetActive(false);
        for (int i = 0; i < actionButtons.Count; i++)
        {
            if (actionButtons[i]?.highlight != null)
            {
                actionButtons[i].highlight.SetActive(false);
            }
        }
    }

    public void NotifyAction(string actionId)
    {
        string normalized = Normalize(actionId);
        if (!string.IsNullOrEmpty(normalized))
        {
            ActionPerformed?.Invoke(normalized);
        }
    }

    private void BindButtons()
    {
        UnbindButtons();
        for (int i = 0; i < actionButtons.Count; i++)
        {
            ActionButtonBinding binding = actionButtons[i];
            if (binding?.button == null || string.IsNullOrWhiteSpace(binding.actionId))
            {
                continue;
            }

            string capturedId = Normalize(binding.actionId);
            UnityAction listener = () => NotifyAction(capturedId);
            binding.button.onClick.AddListener(listener);
            listeners[binding.button] = listener;
        }
    }

    private void UnbindButtons()
    {
        foreach (KeyValuePair<Button, UnityAction> entry in listeners)
        {
            if (entry.Key != null)
            {
                entry.Key.onClick.RemoveListener(entry.Value);
            }
        }
        listeners.Clear();
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
