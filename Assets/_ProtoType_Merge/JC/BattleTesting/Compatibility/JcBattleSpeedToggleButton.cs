// [JC 테스트 씬 전용 / 기준 0705af74]
// 원본: Assets/_ProtoType_Merge/KJ/Scripts/UI/BattleSpeedToggleButton.cs
// 원본 객체 BattleSpeedToggleButton → JcBattleSpeedToggleButton. 동일 이름 함수는 원본 함수와 1:1 대응합니다.
// 차이: JC 전투 흐름/입력을 참조하며 원본 씬·컴포넌트·데이터를 수정하지 않습니다.
using UnityEngine;

[RequireComponent(typeof(ToggleButton))]
public class JcBattleSpeedToggleButton : MonoBehaviour
{
    [Tooltip("JC 전투 실행기입니다. 배속 변경을 테스트 유닛과 연출에 적용합니다.")]
    [SerializeField] private JcBattleManager battleManager;
    [Tooltip("빠른 전투 속도 배율입니다. 2는 정상 속도의 두 배이며 0 이하는 사용하지 않습니다.")]
    [SerializeField] private float fastSpeed = 2f;
    [Tooltip("일반 전투 속도 배율입니다. 1이 정상 속도입니다.")]
    [SerializeField] private float normalSpeed = 1f;

    private ToggleButton toggleButton;

    private void Awake()
    {
        toggleButton = GetComponent<ToggleButton>();

        if (battleManager == null)
            battleManager = FindFirstObjectByType<JcBattleManager>();
    }

    private void OnEnable()
    {
        if (toggleButton == null)
            toggleButton = GetComponent<ToggleButton>();

        bool isFast = Mathf.Approximately(JcBattleRuntimeSettings.BattleSpeed, fastSpeed);
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
        JcBattleRuntimeSettings.SetBattleSpeed(speed);

        if (battleManager == null)
            battleManager = FindFirstObjectByType<JcBattleManager>();

        battleManager?.ChangeBattleSpeed(speed);
    }
}
