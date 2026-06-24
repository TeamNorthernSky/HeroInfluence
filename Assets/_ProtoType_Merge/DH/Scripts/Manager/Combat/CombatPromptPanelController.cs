using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatPromptPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button startBattleButton;
    [SerializeField] private Button fleeButton;
    [SerializeField] private Button skipBattleButton;
    [SerializeField] private TextMeshProUGUI advantageText;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    private Action startBattleHandler;
    private Action fleeHandler;
    private Action skipBattleHandler;

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

        if (skipBattleButton != null)
            skipBattleButton.onClick.AddListener(HandleSkipBattleClicked);
    }

    private void OnDisable()
    {
        if (startBattleButton != null)
            startBattleButton.onClick.RemoveListener(HandleStartBattleClicked);

        if (fleeButton != null)
            fleeButton.onClick.RemoveListener(HandleFleeClicked);

        if (skipBattleButton != null)
            skipBattleButton.onClick.RemoveListener(HandleSkipBattleClicked);
    }

    public void Open(Action onStartBattle, Action onFlee, Action onSkipBattle, string advantageLabel)
    {
        startBattleHandler = onStartBattle;
        fleeHandler = onFlee;
        skipBattleHandler = onSkipBattle;
        gameObject.SetActive(true);
        PrepareInteractableState();
        SetAdvantageLabel(advantageLabel);
    }

    public void Close()
    {
        startBattleHandler = null;
        fleeHandler = null;
        skipBattleHandler = null;
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

    private void HandleSkipBattleClicked()
    {
        Action handler = skipBattleHandler;
        skipBattleHandler = null;
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
        PrepareButton(skipBattleButton);
    }

    private void SetAdvantageLabel(string label)
    {
        if (advantageText == null)
            return;

        advantageText.text = string.IsNullOrWhiteSpace(label) ? string.Empty : label;
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

        if (skipBattleButton == null)
        {
            buttons ??= GetComponentsInChildren<Button>(true);
            if (buttons.Length > 2)
                skipBattleButton = buttons[2];
        }

        if (advantageText == null)
        {
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI text = texts[i];
                if (text != null && text.name.IndexOf("Advantage", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    advantageText = text;
                    break;
                }
            }
        }

        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null)
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
}
