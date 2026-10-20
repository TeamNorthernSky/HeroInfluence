using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public sealed class AutoTurnEndSettingsToggle : MonoBehaviour
{
    private Toggle toggle;
    private TurnManager turnManager;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(SetAutoTurnEnd);
    }

    private void OnEnable() { Refresh(); }
    private void Update() { Refresh(); }

    private void Refresh()
    {
        if (toggle == null) return;
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
        toggle.interactable = turnManager != null;
        toggle.SetIsOnWithoutNotify(turnManager != null && turnManager.AutoTurnEnd);
    }

    private void SetAutoTurnEnd(bool enabled)
    {
        //if (turnManager != null) turnManager.AutoTurnEnd = enabled;
    }

    private void OnDestroy()
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(SetAutoTurnEnd);
    }
}
