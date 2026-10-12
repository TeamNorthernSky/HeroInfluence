using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>System menu save result and confirmation for leaving the game.</summary>
[DisallowMultipleComponent]
public sealed class SystemMenuActionConfirmations : MonoBehaviour
{
    [SerializeField] private SystemMenuController menu;
    [SerializeField] private Button titleButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private GameObject titlePopup;
    [SerializeField] private GameObject quitPopup;
    [SerializeField] private GameObject saveCompletedPopup;
    [SerializeField] private UnityEvent titleConfirmed = new UnityEvent();
    [SerializeField] private UnityEvent quitConfirmed = new UnityEvent();

    private GameObject pendingPopup;

    // The menu installs direct action listeners in Awake; replace them after that.
    private void Start()
    {
        if (menu == null || titleButton == null || quitButton == null ||
            titlePopup == null || quitPopup == null || saveCompletedPopup == null)
        {
            Debug.LogError("[SystemMenuActionConfirmations] Missing menu, buttons or popups.", this);
            return;
        }

        titleButton.onClick.RemoveListener(menu.OnClickToTitle);
        quitButton.onClick.RemoveListener(menu.OnClickQuit);
        titleButton.onClick.AddListener(OpenTitleConfirmation);
        quitButton.onClick.AddListener(OpenQuitConfirmation);
    }

    public void OpenTitleConfirmation() => Open(titleButton, titlePopup);
    public void OpenQuitConfirmation() => Open(quitButton, quitPopup);

    private void Open(Button button, GameObject popup)
    {
        if (button == null || popup == null || !button.isActiveAndEnabled ||
            !button.interactable) return;
        CancelAction();
        pendingPopup = popup;
        popup.SetActive(true);
    }

    public void ConfirmTitle() => Confirm(titlePopup, titleConfirmed);
    public void ConfirmQuit() => Confirm(quitPopup, quitConfirmed);

    private void Confirm(GameObject popup, UnityEvent action)
    {
        if (popup == null || pendingPopup != popup || !popup.activeInHierarchy ||
            ModalManager.Top != popup) return;
        pendingPopup = null;
        popup.SetActive(false);
        action.Invoke();
    }

    public void CancelAction()
    {
        if (pendingPopup != null) pendingPopup.SetActive(false);
        pendingPopup = null;
    }

    // Called by the existing save confirmation, once the player chooses Yes.
    public void SaveAndNotify()
    {
        bool saved = GameSaveService.SaveToSlot(SaveSlotRepository.CurrentSlot);
        Debug.Log(saved
            ? $"[SystemMenu] 저장 완료 — 슬롯 {SaveSlotRepository.CurrentSlot}."
            : "[SystemMenu] 저장 실패/건너뜀 — 턴 시작 스냅샷 없음 또는 IO 오류.");
        ShowSaveResult(saved);
    }

    private void ShowSaveResult(bool saved)
    {
        if (saveCompletedPopup != null) saveCompletedPopup.SetActive(saved);
    }

    public void CloseSaveCompleted()
    {
        if (saveCompletedPopup != null) saveCompletedPopup.SetActive(false);
    }

    private void OnDestroy()
    {
        if (titleButton != null) titleButton.onClick.RemoveListener(OpenTitleConfirmation);
        if (quitButton != null) quitButton.onClick.RemoveListener(OpenQuitConfirmation);
    }
}
