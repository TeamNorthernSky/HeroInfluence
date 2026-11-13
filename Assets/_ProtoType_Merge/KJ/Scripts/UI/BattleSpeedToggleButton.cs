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
        toggleButton.EnablePressedAppearance();
        toggleButton.SetState(isFast, false);
        toggleButton.OnValueChanged += OnToggled;
    }

    // JC | 2026-11-13 | 기존 ToggleButton의 Awake가 끝난 뒤 안내용 입력을 연결합니다. 기능 클릭은 잠금 컴포넌트가 차단합니다.
    private void Start() => JC.Tutorial.JcTutorialBattleControlLock.Attach(gameObject);

    private void OnDisable()
    {
        toggleButton.OnValueChanged -= OnToggled;
    }

    private void OnToggled(bool isOn)
    {
        // JC | 2026-11-13 | 직접 이벤트 호출도 공용 배속 설정을 바꾸지 못하게 합니다.
        if (JC.Tutorial.JcTutorialBattleControlLock.IsRestricted(gameObject.scene))
        { toggleButton.SetState(false, false); return; }
        float speed = isOn ? fastSpeed : normalSpeed;
        BattleRuntimeSettings.SetBattleSpeed(speed);

        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        battleManager?.ChangeBattleSpeed(speed);
    }
}
