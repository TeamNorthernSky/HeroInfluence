using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TemporaryTutorialTurnEndButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private TutorialTurnManager turnManager;
    [SerializeField] private TMP_Text label;

    [Header("Display")]
    [SerializeField] private string labelText = "턴 종료";

    private void Awake()
    {
        ResolveReferences();
        ApplyLabel();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyLabel();

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }

    private void OnValidate()
    {
        ResolveReferences();
        ApplyLabel();
    }

    public void HandleClicked()
    {
        ResolveReferences();
        if (turnManager == null)
        {
            Debug.LogWarning("[TemporaryTutorialTurnEndButton] TutorialTurnManager is missing.", this);
            return;
        }

        turnManager.EndTutorialTurn();
    }

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (turnManager == null)
            turnManager = FindFirstObjectByType<TutorialTurnManager>();

        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);
    }

    private void ApplyLabel()
    {
        if (label != null)
            label.text = labelText;
    }
}
