using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Keeps the disabled heal artwork while allowing its existing reason message.</summary>
[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public sealed class InfirmaryDisabledHealClick : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        var button = GetComponent<Button>();
        if (!button.IsActive() || button.interactable) return;
        // OnHealAll checks the current recovery conditions before doing anything.
        button.onClick.Invoke();
    }
}
