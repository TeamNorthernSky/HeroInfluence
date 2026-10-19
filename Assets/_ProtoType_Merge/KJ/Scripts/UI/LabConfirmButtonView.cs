using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class LabConfirmButtonView : MonoBehaviour
{
    private Button button;
    private GameObject disabledOverlay;

    private void Awake()
    {
        button = GetComponent<Button>();
        var overlay = transform.Find("DisabledOverlay");
        disabledOverlay = overlay != null ? overlay.gameObject : null;
        if (overlay != null)
            foreach (var graphic in overlay.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
    }

    private void OnEnable() { Refresh(); }
    private void LateUpdate() { Refresh(); }

    private void Refresh()
    {
        if (button != null && disabledOverlay != null)
            disabledOverlay.SetActive(!button.IsInteractable());
    }
}
