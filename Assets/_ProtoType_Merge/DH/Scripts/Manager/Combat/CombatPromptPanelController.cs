using System;
using UnityEngine;
using UnityEngine.UI;

public class CombatPromptPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button startBattleButton;
    [SerializeField] private Button fleeButton;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    private Action startBattleHandler;
    private Action fleeHandler;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (startBattleButton != null)
            startBattleButton.onClick.AddListener(HandleStartBattleClicked);

        if (fleeButton != null)
            fleeButton.onClick.AddListener(HandleFleeClicked);
    }

    private void OnDisable()
    {
        if (startBattleButton != null)
            startBattleButton.onClick.RemoveListener(HandleStartBattleClicked);

        if (fleeButton != null)
            fleeButton.onClick.RemoveListener(HandleFleeClicked);
    }

    public void Open(Action onStartBattle, Action onFlee)
    {
        startBattleHandler = onStartBattle;
        fleeHandler = onFlee;
        gameObject.SetActive(true);
        PrepareInteractableState();
    }

    public void Close()
    {
        startBattleHandler = null;
        fleeHandler = null;
        gameObject.SetActive(false);
    }

    private void HandleStartBattleClicked()
    {
        Action handler = startBattleHandler;
        startBattleHandler = null;
        handler?.Invoke();
    }

    private void HandleFleeClicked()
    {
        Action handler = fleeHandler;
        fleeHandler = null;
        handler?.Invoke();
    }

    private void PrepareInteractableState()
    {
        ResolveReferences();

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
            panelCanvasGroup.ignoreParentGroups = true;
        }

        PrepareButton(startBattleButton);
        PrepareButton(fleeButton);
    }

    private static void PrepareButton(Button button)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(true);
        button.enabled = true;
        button.interactable = true;
    }

    private void ResolveReferences()
    {
        Button[] buttons = null;
        if (startBattleButton == null)
        {
            buttons = GetComponentsInChildren<Button>(true);
            if (buttons.Length > 0)
                startBattleButton = buttons[0];
        }

        if (fleeButton == null)
        {
            buttons ??= GetComponentsInChildren<Button>(true);
            if (buttons.Length > 1)
                fleeButton = buttons[1];
        }

        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null)
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
}
