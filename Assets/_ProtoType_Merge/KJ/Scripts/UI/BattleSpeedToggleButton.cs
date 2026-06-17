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
        if (toggleButton == null)
            toggleButton = GetComponent<ToggleButton>();

        bool isFast = Mathf.Approximately(BattleRuntimeSettings.BattleSpeed, fastSpeed);
        toggleButton.SetState(isFast, false);
        toggleButton.OnValueChanged += OnToggled;
    }

    private void OnDisable()
    {
        toggleButton.OnValueChanged -= OnToggled;
    }

    private void OnToggled(bool isOn)
    {
        float speed = isOn ? fastSpeed : normalSpeed;
        BattleRuntimeSettings.SetBattleSpeed(speed);

        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        battleManager?.ChangeBattleSpeed(speed);
    }
}
