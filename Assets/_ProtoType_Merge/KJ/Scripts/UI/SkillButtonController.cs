using UnityEngine;

/// <summary>
/// Button_Skill / Button_Skill_2의 hover + toggle 상태를 통합 관리합니다.
/// tooltip 표시 조건:
///   tooltip1 = (hover1 || toggle1) && !toggle2
///   tooltip2 = (hover2 || toggle2) && !toggle1
/// </summary>
public class SkillButtonController : MonoBehaviour
{
    [SerializeField] private ToggleButton toggle1;
    [SerializeField] private ToggleButton toggle2;
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private BattleFlowManager battleFlowManager;

    private void Awake()
    {
        if (inputHandler == null)
            inputHandler = FindFirstObjectByType<InputHandler>();
        if (battleFlowManager == null)
            battleFlowManager = FindFirstObjectByType<BattleFlowManager>();
    }

    private void Start()
    {
        toggle2.SetState(false, false);
        toggle1.SetState(true);
    }

    private System.Action<bool> onToggle1;
    private System.Action<bool> onToggle2;

    private void OnEnable()
    {
        onToggle1 = isOn => OnToggleChanged(isOn, toggle2, PendingActionType.ClassSkill);
        onToggle2 = isOn => OnToggleChanged(isOn, toggle1, PendingActionType.WeaponSkill);
        toggle1.OnValueChanged += onToggle1;
        toggle2.OnValueChanged += onToggle2;

        if (battleFlowManager != null)
            battleFlowManager.OnTurnStarted += OnTurnStarted;
    }

    private void OnDisable()
    {
        toggle1.OnValueChanged -= onToggle1;
        toggle2.OnValueChanged -= onToggle2;

        if (battleFlowManager != null)
            battleFlowManager.OnTurnStarted -= OnTurnStarted;
    }

    private void OnTurnStarted(int round, BattleCharactor unit)
    {
        if (unit == null || !unit.IsPlayer) return;
        Debug.Log(unit.UnitName);

        toggle2.SetState(false, false);
        toggle1.SetState(false, false);
        toggle1.SetState(true);
    }

    private void OnToggleChanged(bool isOn, ToggleButton other, PendingActionType skillAction)
    {
        if (!isOn) return;

        other.SetState(false, false);
        inputHandler?.BeginPendingAction(skillAction);
    }
}
