using UnityEngine;

[RequireComponent(typeof(ToggleButton))]
public class BattleSpeedToggleButton : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private float fastSpeed = 2f;
    [SerializeField] private float normalSpeed = 1f;

    private ToggleButton toggleButton;

    private void Awake()
    {
        toggleButton = GetComponent<ToggleButton>();

        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();
    }

    private void OnEnable()
    {
        toggleButton.OnValueChanged += OnToggled;
    }

    private void OnDisable()
    {
        toggleButton.OnValueChanged -= OnToggled;
        battleManager?.ChangeBattleSpeed(normalSpeed);
    }

    private void OnToggled(bool isOn)
    {
        if (battleManager == null) return;
        battleManager.ChangeBattleSpeed(isOn ? fastSpeed : normalSpeed);
    }
}
