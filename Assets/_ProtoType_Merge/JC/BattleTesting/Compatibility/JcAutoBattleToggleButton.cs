// [JC 테스트 씬 전용 / 기준 0705af74]
// 원본: Assets/_ProtoType_Merge/KJ/Scripts/UI/AutoBattleToggleButton.cs
// 원본 객체 AutoBattleToggleButton → JcAutoBattleToggleButton. 동일 이름 함수는 원본 함수와 1:1 대응합니다.
// 차이: JC 전투 흐름/입력을 참조하며 원본 씬·컴포넌트·데이터를 수정하지 않습니다.
using UnityEngine;

[RequireComponent(typeof(ToggleButton))]
public class JcAutoBattleToggleButton : MonoBehaviour
{
    [Tooltip("JC 자동전투 제어입니다. 버튼을 켜면 아군 턴의 행동을 자동 선택합니다.")]
    [SerializeField] private JcAutoBattleController autoBattleController;

    private ToggleButton toggleButton;

    private void Awake()
    {
        toggleButton = GetComponent<ToggleButton>();

        if (autoBattleController == null)
            autoBattleController = FindFirstObjectByType<JcAutoBattleController>();
    }

    private void OnEnable()
    {
        if (toggleButton == null)
            toggleButton = GetComponent<ToggleButton>();

        toggleButton.SetState(JcBattleRuntimeSettings.IsAutoBattle, false);
        toggleButton.OnValueChanged += OnToggled;
    }

    private void OnDisable()
    {
        toggleButton.OnValueChanged -= OnToggled;
    }

    private void OnToggled(bool isOn)
    {
        JcBattleRuntimeSettings.SetAutoBattle(isOn);

        if (autoBattleController == null)
            autoBattleController = FindFirstObjectByType<JcAutoBattleController>();

        if (autoBattleController != null)
            autoBattleController.IsAutoBattle = isOn;
    }
}
