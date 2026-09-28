using UnityEngine;
using UnityEngine.UI;

/// <summary>Clears the shared hover effect when a button becomes unavailable under the pointer.</summary>
[RequireComponent(typeof(Button), typeof(ButtonEffectActiveToggle))]
[DisallowMultipleComponent]
public sealed class ButtonHoverStateReset : MonoBehaviour
{
    private Button button;
    private ButtonEffectActiveToggle hoverEffect;

    private void Awake()
    {
        button = GetComponent<Button>();
        hoverEffect = GetComponent<ButtonEffectActiveToggle>();
    }

    private void LateUpdate()
    {
        if (!button.IsActive() || !button.IsInteractable())
            hoverEffect.OnHoverExit();
    }

    private void OnDisable()
    {
        if (hoverEffect != null)
            hoverEffect.OnHoverExit();
    }
}
