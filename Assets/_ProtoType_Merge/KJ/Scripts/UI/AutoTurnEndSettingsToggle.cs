using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public sealed class AutoTurnEndSettingsToggle : MonoBehaviour
{
    [SerializeField] private Image checkboxImage;
    [SerializeField] private Sprite checkedSprite;
    [SerializeField] private Sprite uncheckedSprite;
    private Toggle toggle;
    private SettingsModalController settings;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
        settings = GetComponentInParent<SettingsModalController>(true);
        toggle.onValueChanged.AddListener(SetAutoTurnEnd);
        Refresh();
    }

    private void OnEnable() { Refresh(); }

    public void Refresh()
    {
        if (toggle == null) return;
        toggle.interactable = true;
        toggle.SetIsOnWithoutNotify(settings != null ? settings.PendingAutoTurnEnd : GameSettings.AutoTurnEnd);
        UpdateSprite(toggle.isOn);
    }

    private void SetAutoTurnEnd(bool enabled)
    {
        if (settings != null) settings.SetPendingAutoTurnEnd(enabled);
        else GameSettings.ApplyAutoTurnEnd(enabled);
        UpdateSprite(enabled);
    }

    private void UpdateSprite(bool enabled)
    {
        if (checkboxImage == null || checkedSprite == null || uncheckedSprite == null) return;
        if (toggle.graphic != null && toggle.graphic != checkboxImage)
            toggle.graphic.gameObject.SetActive(false);
        toggle.graphic = null;
        toggle.transition = Selectable.Transition.None;
        checkboxImage.color = Color.white;
        checkboxImage.sprite = enabled ? checkedSprite : uncheckedSprite;
    }

    private void OnDestroy()
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(SetAutoTurnEnd);
    }
}
