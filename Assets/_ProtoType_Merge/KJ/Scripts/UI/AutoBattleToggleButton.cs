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
        if (toggleButton == null)
            toggleButton = GetComponent<ToggleButton>();

        toggleButton.SetState(BattleRuntimeSettings.IsAutoBattle, false);
        toggleButton.OnValueChanged += OnToggled;
    }

    private void OnDisable()
    {
        toggleButton.OnValueChanged -= OnToggled;
    }

    private void OnToggled(bool isOn)
    {
        BattleRuntimeSettings.SetAutoBattle(isOn);

        if (autoBattleController == null)
            autoBattleController = FindFirstObjectByType<AutoBattleController>();

        if (autoBattleController != null)
            autoBattleController.IsAutoBattle = isOn;
    }
}
