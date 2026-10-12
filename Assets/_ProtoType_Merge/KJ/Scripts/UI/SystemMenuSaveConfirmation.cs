using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Routes the system menu save button through its confirmation popup.</summary>
[DisallowMultipleComponent]
public sealed class SystemMenuSaveConfirmation : MonoBehaviour
{
    [SerializeField] private SystemMenuController menu;
    [SerializeField] private Button saveButton;
    [SerializeField] private GameObject popup;
    [SerializeField] private UnityEvent saveConfirmed = new UnityEvent();

    private bool awaitingConfirmation;

    // SystemMenuController installs its original save listener in Awake.
    private void Start()
    {
        if (menu == null || saveButton == null || popup == null)
        {
            Debug.LogError("[SystemMenuSaveConfirmation] Missing menu, save button or popup.", this);
            return;
        }

        saveButton.onClick.RemoveListener(menu.OnClickSave);
        saveButton.onClick.AddListener(OpenConfirmation);
    }

    public void OpenConfirmation()
    {
        if (popup == null || saveButton == null || !saveButton.isActiveAndEnabled ||
            !saveButton.interactable) return;
        awaitingConfirmation = true;
        popup.SetActive(true);
    }

    public void ConfirmSave()
    {
        if (!awaitingConfirmation || popup == null || !popup.activeInHierarchy ||
            ModalManager.Top != popup) return;

        awaitingConfirmation = false;
        popup.SetActive(false);
        saveConfirmed.Invoke();
    }

    public void Cancel()
    {
        awaitingConfirmation = false;
        if (popup != null) popup.SetActive(false);
    }

    private void OnDestroy()
    {
        if (saveButton != null) saveButton.onClick.RemoveListener(OpenConfirmation);
    }
}
