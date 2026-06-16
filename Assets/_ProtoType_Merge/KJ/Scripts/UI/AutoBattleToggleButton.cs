using UnityEngine;

[RequireComponent(typeof(ToggleButton))]
public class AutoBattleToggleButton : MonoBehaviour
{
    [SerializeField] private AutoBattleController autoBattleController;

    private ToggleButton toggleButton;

    private void Awake()
    {
        toggleButton = GetComponent<ToggleButton>();

        if (autoBattleController == null)
            autoBattleController = FindFirstObjectByType<AutoBattleController>();
    }

    private void OnEnable()
    {
        toggleButton.OnValueChanged += OnToggled;
    }

    private void OnDisable()
    {
        toggleButton.OnValueChanged -= OnToggled;
    }

    private void OnToggled(bool isOn)
    {
        if (autoBattleController == null) return;
        autoBattleController.IsAutoBattle = isOn;
    }
}
