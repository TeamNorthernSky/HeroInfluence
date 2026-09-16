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

    [Serializable]
    private sealed class StepTextBinding
    {
        public string key;            // ShowUi("key")와 매칭. 예: "step1"
        public GameObject textObject; // 해당 step일 때만 켜지는 텍스트 오브젝트
    }

    [SerializeField] private GameObject panel;                 // 선택: 있으면 Show 시 켜기만 함(버튼 보호 위해 Hide는 끄지 않음)
    [SerializeField] private TMP_Text messageLabel;            // 선택: 동적 문구용. step text를 쓰면 비워도 됨
    [SerializeField] private List<ActionButtonBinding> actionButtons = new List<ActionButtonBinding>();
    [SerializeField] private List<StepTextBinding> stepTexts = new List<StepTextBinding>();

    private readonly Dictionary<Button, UnityAction> listeners = new Dictionary<Button, UnityAction>();

    public event Action<string> ActionPerformed;

    private void Awake()
    {
        // 시작 시 모든 step text를 숨겨 둔다(해당 step에서 Show로 켜진다).
        HideAllStepTexts();
    }

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    /// <summary>표시 가능한 상태인지(step text 또는 messageLabel 중 하나라도 있으면 준비됨).</summary>
    public bool IsReady => (stepTexts != null && stepTexts.Count > 0) || messageLabel != null;

    /// <summary>actionId가 등록된 버튼인지(누를 대상 존재 여부).</summary>
    public bool HasActionBinding(string actionId)
    {
        string n = Normalize(actionId);
        if (string.IsNullOrEmpty(n))
        {
            return false;
        }

        for (int i = 0; i < actionButtons.Count; i++)
        {
            ActionButtonBinding b = actionButtons[i];
            if (b != null && string.Equals(Normalize(b.actionId), n, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>key에 해당하는 step text를 띄운다(성공 여부 반환). message는 messageLabel이 있을 때만 사용.</summary>
    public bool TryShow(string key, string message, string highlightedActionId = null)
    {
        if (!IsReady)
        {
            Debug.LogWarning("[TutorialUI] stepTexts/messageLabel 미할당 — UI를 표시할 수 없습니다.", this);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(highlightedActionId) && !HasActionBinding(highlightedActionId))
        {
            Debug.LogWarning(
                $"[TutorialUI] 미등록 highlightActionId '{highlightedActionId}' — 누를 버튼이 없어 진행 불능 위험.",
                this);
        }

        Show(key, message, highlightedActionId);
        return true;
    }

    /// <summary>key의 step text만 켜고 나머지 step text는 끈다. 버튼은 건드리지 않는다.</summary>
    public void Show(string key, string message, string highlightedActionId = null)
    {
        string wantedKey = Normalize(key);

        for (int i = 0; i < stepTexts.Count; i++)
        {
            StepTextBinding b = stepTexts[i];
            if (b?.textObject != null)
            {
                b.textObject.SetActive(
                    !string.IsNullOrEmpty(wantedKey) &&
                    string.Equals(Normalize(b.key), wantedKey, StringComparison.Ordinal));
            }
        }

        // 선택: 동적 문구(messageLabel이 배선돼 있을 때만)
        if (messageLabel != null)
        {
            messageLabel.text = message ?? string.Empty;
        }

        // 버튼 강조 토글
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

        // panel은 있으면 켜기만 한다(Hide에서 끄지 않으므로 버튼이 통째로 사라지지 않는다).
        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    /// <summary>현재 step text와 강조만 숨긴다. panel/버튼은 그대로 둔다(버튼이 통째로 사라지는 문제 방지).</summary>
    public void Hide()
    {
        HideAllStepTexts();

        for (int i = 0; i < actionButtons.Count; i++)
        {
            if (actionButtons[i]?.highlight != null)
            {
                actionButtons[i].highlight.SetActive(false);
            }
        }
    }

    private void HideAllStepTexts()
    {
        if (stepTexts == null)
        {
            return;
        }

        for (int i = 0; i < stepTexts.Count; i++)
        {
            if (stepTexts[i]?.textObject != null)
            {
                stepTexts[i].textObject.SetActive(false);
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
