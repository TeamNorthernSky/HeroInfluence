using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Keeps the disabled heal artwork while allowing its existing reason message.</summary>
[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public sealed class InfirmaryDisabledHealClick : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    private bool pressedWhileDisabled;
    private int pressedPointerId;

    public void OnPointerDown(PointerEventData eventData)
    {
        var button = GetComponent<Button>();
        pressedWhileDisabled = eventData.button == PointerEventData.InputButton.Left
            && button.IsActive() && !button.interactable;
        pressedPointerId = eventData.pointerId;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        bool showReason = pressedWhileDisabled && pressedPointerId == eventData.pointerId;
        pressedWhileDisabled = false;
        if (!showReason || eventData.button != PointerEventData.InputButton.Left) return;
        var button = GetComponent<Button>();
        if (!button.IsActive() || button.interactable) return;
        // A successful heal can disable the Button before this click handler runs.
        // Only a press that started disabled should invoke the unavailable reason.
        button.onClick.Invoke();
    }

    private void OnDisable() => pressedWhileDisabled = false;
}
