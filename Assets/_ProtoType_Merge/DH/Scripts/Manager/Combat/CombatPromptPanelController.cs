using System;
using UnityEngine;
using UnityEngine.UI;

public class CombatPromptPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button startBattleButton;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    private Action startBattleHandler;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (startBattleButton != null)
            startBattleButton.onClick.AddListener(HandleStartBattleClicked);
    }

    private void OnDisable()
    {
        if (startBattleButton != null)
            startBattleButton.onClick.RemoveListener(HandleStartBattleClicked);
    }

    public void Open(Action onStartBattle)
    {
        startBattleHandler = onStartBattle;
        gameObject.SetActive(true);
        PrepareInteractableState();
    }

    public void Close()
    {
        startBattleHandler = null;
        gameObject.SetActive(false);
    }

    private void HandleStartBattleClicked()
    {
        Action handler = startBattleHandler;
        startBattleHandler = null;
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

        if (startBattleButton == null)
            return;

        startBattleButton.gameObject.SetActive(true);
        startBattleButton.enabled = true;
        startBattleButton.interactable = true;
    }

    private void ResolveReferences()
    {
        if (startBattleButton == null)
            startBattleButton = GetComponentInChildren<Button>(true);

        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null)
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
}
