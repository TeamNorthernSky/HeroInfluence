using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// [KJ 260812] Routes Enter/Keypad Enter to the affirmative action of the active popup.
///
/// Popup-to-button mappings are explicit because button names and hierarchy layouts differ.
/// ModalManager determines the top modal; known nested and non-modal overlays are handled
/// separately so an obscured popup never receives the action.
/// </summary>
[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
public sealed class PopupEnterSubmitter : MonoBehaviour
{
    private const string HostName = "[PopupEnterSubmitter]";
    private const string CloneSuffix = "(Clone)";

    private static PopupEnterSubmitter instance;

    private EventSystem suppressedEventSystem;
    private bool previousSendNavigationEvents;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        var host = new GameObject(HostName);
        DontDestroyOnLoad(host);
        instance = host.AddComponent<PopupEnterSubmitter>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!IsPlainEnterPressed()) return;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;
        if (ShouldYieldToTextInput(eventSystem) || HasExpandedDropdown()) return;

        // With no active supported popup, preserve the EventSystem's normal Submit behavior.
        if (!TryResolveActivePopup(out Button confirmButton)) return;

        // StandaloneInputModule also treats Enter as Submit. Suppress navigation until
        // LateUpdate so only this explicitly selected affirmative action runs this frame.
        SuppressNativeSubmitForCurrentFrame(eventSystem);

        // A hidden or disabled affirmative action consumes Enter without falling through.
        if (!IsSubmittable(confirmButton)) return;

        var submitEvent = new BaseEventData(eventSystem);
        ExecuteEvents.Execute(confirmButton.gameObject, submitEvent, ExecuteEvents.submitHandler);
    }

    private void LateUpdate()
    {
        RestoreNativeSubmit();
    }

    private void OnDestroy()
    {
        RestoreNativeSubmit();
        if (instance == this) instance = null;
    }

    private static bool IsPlainEnterPressed()
    {
        //if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter))
        if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter) && !Input.GetKeyDown(KeyCode.Space))
            return false;

        // Leave modifier shortcuts (including Ctrl+Shift+Enter) to their existing handlers.
        bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        return !control && !alt;
    }

    private static bool ShouldYieldToTextInput(EventSystem eventSystem)
    {
        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected == null) return false;

        TMP_InputField tmpInput = selected.GetComponentInParent<TMP_InputField>();
        if (tmpInput != null && tmpInput.isFocused) return true;

        InputField legacyInput = selected.GetComponentInParent<InputField>();
        return legacyInput != null && legacyInput.isFocused;
    }

    private static bool HasExpandedDropdown()
    {
        TMP_Dropdown[] tmpDropdowns = FindObjectsByType<TMP_Dropdown>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < tmpDropdowns.Length; i++)
        {
            if (tmpDropdowns[i] != null && tmpDropdowns[i].IsExpanded)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true when an active popup owns Enter. The button may intentionally be null;
    /// this consumes Enter and prevents fallback to a popup underneath it.
    /// </summary>
    private static bool TryResolveActivePopup(out Button confirmButton)
    {
        confirmButton = null;

        GameObject topModal = ModalManager.Top;
        if (topModal != null && topModal.activeInHierarchy)
        {
            confirmButton = ResolveModalConfirmButton(topModal);
            return true;
        }

        // These visible overlays are popup-like but are not registered with ModalManager.
        if (TryResolveComponentPopup<SkillSelectionPanel>(out confirmButton, "ConfirmButton"))
            return true;

        if (TryResolveComponentPopup<BattleResultPanel>(out confirmButton, "Accept"))
            return true;

        if (TryResolveComponentPopup<CombatPromptPanelController>(out confirmButton, "BattleButton"))
            return true;

        return false;
    }

    private static Button ResolveModalConfirmButton(GameObject topModal)
    {
        string rootName = NormalizeInstanceName(topModal.name);

        // SkipConfirm is nested inside ChatModal and is not a separate Modal registration.
        if (string.Equals(rootName, "ChatModal", StringComparison.Ordinal))
        {
            Transform skipConfirm = FindActiveTransform(topModal.transform, "SkipConfirm");
            return skipConfirm != null ? FindNamedButton(skipConfirm, "YesButton") : null;
        }

        switch (rootName)
        {
            case "ExitPopup":
            case "TutorialPopup":
            case "Modal_Settings":
                return FindNamedButton(topModal.transform, "Accept Button");

            case "DeleteConfirmPopup":
                return FindNamedButton(topModal.transform, "Confirm Button");

            case "Modal_HQLobby_HQProgress":
            case "Modal_HQLobby_Exchange":
            case "Modal_Lab":
            case "Modal_Training":
            case "Modal_Publicity":
                return FindNamedButton(topModal.transform, "BTN_Confirm");

            case "Modal_HQLobby_UpgradeResult":
                return FindNamedButton(topModal.transform, "BTN_OK");

            case "Modal_Workshop":
                return FindNamedButton(topModal.transform, "BtnPrimary");

            case "Modal_Sortie":
                return FindNamedButton(topModal.transform, "BTN_Sortie_Confirm", "BTN_Confirm");

            case "Modal_Infirmary":
                return FindNamedButton(topModal.transform, "BTN_HealAll");

            case "MapEventPanel":
                return FindNamedButton(topModal.transform, "YesButton");

            case "OutpostPanel":
                return FindNamedButton(topModal.transform, "OKButton");

            case "Modal_HQLobby_EndTurnConfirm":
                return FindNamedButton(topModal.transform, "YesButton", "Yes", "BTN_Yes");

            case "PNL_PressNextTurn":
                // This popup is spawned beside the external next-turn button.
                return FindNamedButton(topModal.transform.parent, "BTN_Explor_NextTurn");

            default:
                // Unknown top modals own Enter but deliberately have no inferred action.
                return null;
        }
    }

    private static bool TryResolveComponentPopup<T>(out Button confirmButton, params string[] buttonNames)
        where T : Behaviour
    {
        confirmButton = null;
        T[] popups = FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        T fallback = null;

        for (int i = 0; i < popups.Length; i++)
        {
            T popup = popups[i];
            if (popup == null || !popup.isActiveAndEnabled || !popup.gameObject.activeInHierarchy)
                continue;

            fallback ??= popup;
            Button candidate = FindNamedButton(popup.transform, buttonNames);
            if (candidate != null && candidate.gameObject.activeInHierarchy)
            {
                confirmButton = candidate;
                return true;
            }
        }

        if (fallback == null) return false;

        // If the active popup exists but its named button is absent or hidden, it still owns
        // Enter. A null result is consumed later instead of falling through to another popup.
        confirmButton = FindNamedButton(fallback.transform, buttonNames);
        return true;
    }

    private static Button FindNamedButton(Transform root, params string[] buttonNames)
    {
        if (root == null || buttonNames == null) return null;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int nameIndex = 0; nameIndex < buttonNames.Length; nameIndex++)
        {
            string expected = buttonNames[nameIndex];
            for (int buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
            {
                Button button = buttons[buttonIndex];
                if (button != null && string.Equals(button.gameObject.name, expected, StringComparison.Ordinal))
                    return button;
            }
        }

        return null;
    }

    private static Transform FindActiveTransform(Transform root, string objectName)
    {
        if (root == null) return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null &&
                candidate.gameObject.activeInHierarchy &&
                string.Equals(candidate.gameObject.name, objectName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsSubmittable(Button button)
    {
        return button != null &&
               button.gameObject.activeInHierarchy &&
               button.IsActive() &&
               button.IsInteractable();
    }

    private void SuppressNativeSubmitForCurrentFrame(EventSystem eventSystem)
    {
        RestoreNativeSubmit();
        suppressedEventSystem = eventSystem;
        previousSendNavigationEvents = eventSystem.sendNavigationEvents;
        eventSystem.sendNavigationEvents = false;
    }

    private void RestoreNativeSubmit()
    {
        if (suppressedEventSystem != null)
            suppressedEventSystem.sendNavigationEvents = previousSendNavigationEvents;

        suppressedEventSystem = null;
    }

    private static string NormalizeInstanceName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName) ||
            !objectName.EndsWith(CloneSuffix, StringComparison.Ordinal))
        {
            return objectName;
        }

        return objectName.Substring(0, objectName.Length - CloneSuffix.Length).TrimEnd();
    }
}
