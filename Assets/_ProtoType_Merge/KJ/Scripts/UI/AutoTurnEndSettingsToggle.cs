using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public sealed class AutoTurnEndSettingsToggle : MonoBehaviour
{
    private Toggle toggle;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(SetAutoTurnEnd);
        Refresh();
    }

    private void OnEnable() { Refresh(); }

    private void Refresh()
    {
        if (toggle == null) return;
        toggle.interactable = true;
        toggle.SetIsOnWithoutNotify(GameSettings.AutoTurnEnd);
    }

    private void SetAutoTurnEnd(bool enabled)
    {
        GameSettings.ApplyAutoTurnEnd(enabled);
    }

    private void OnDestroy()
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(SetAutoTurnEnd);
    }
}
