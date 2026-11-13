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

        toggleButton.EnablePressedAppearance();
        toggleButton.SetState(BattleRuntimeSettings.IsAutoBattle, false);
        toggleButton.OnValueChanged += OnToggled;
    }

    // JC | 2026-11-13 | 기존 ToggleButton의 Awake가 끝난 뒤 안내용 입력을 연결합니다. 기존 이벤트/GUID는 유지합니다.
    private void Start() => JC.Tutorial.JcTutorialBattleControlLock.Attach(gameObject);

    private void OnDisable()
    {
        toggleButton.OnValueChanged -= OnToggled;
    }

    private void OnToggled(bool isOn)
    {
        // JC | 2026-11-13 | 직접 이벤트 호출도 공용 자동 설정을 바꾸지 못하게 합니다.
        if (JC.Tutorial.JcTutorialBattleControlLock.IsRestricted(gameObject.scene))
        { toggleButton.SetState(false, false); return; }
        BattleRuntimeSettings.SetAutoBattle(isOn);

        if (autoBattleController == null)
            autoBattleController = FindFirstObjectByType<AutoBattleController>();

        if (autoBattleController != null)
            autoBattleController.IsAutoBattle = isOn;
    }
}
