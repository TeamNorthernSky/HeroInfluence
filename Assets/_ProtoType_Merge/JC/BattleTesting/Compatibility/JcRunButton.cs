// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/KJ/Scripts/UI/RunButton.cs
// 원본 객체: RunButton -> JcRunButton
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using UnityEngine;
using UnityEngine.UI;

public class JcRunButton : MonoBehaviour
{
    [Tooltip("도주 요청을 보내는 기존 씬 버튼입니다. JC에서는 테스트 패배 종료로 처리합니다.")]
    [SerializeField] private Button button;
    [Tooltip("JC 전투의 현재 턴과 행동 권한을 읽는 흐름입니다.")]
    [SerializeField] private JcBattleFlowManager battleFlowManager;

    // 원본 함수 대응: RunButton.Reset (Assets/_ProtoType_Merge/KJ/Scripts/UI/RunButton.cs)

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    // 원본 함수 대응: RunButton.Awake (Assets/_ProtoType_Merge/KJ/Scripts/UI/RunButton.cs)

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (battleFlowManager == null)
        {
            battleFlowManager = FindFirstObjectByType<JcBattleFlowManager>();
        }
    }

    // 원본 함수 대응: RunButton.OnEnable (Assets/_ProtoType_Merge/KJ/Scripts/UI/RunButton.cs)

    private void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(OnRunButtonClicked);

        if (battleFlowManager != null)
        {
            battleFlowManager.OnTurnStarted += OnTurnStarted;
            battleFlowManager.OnBattleEnded += OnBattleEnded;
        }

        //button.interactable = false;
    }

    // 원본 함수 대응: RunButton.OnDisable (Assets/_ProtoType_Merge/KJ/Scripts/UI/RunButton.cs)

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnRunButtonClicked);

        if (battleFlowManager != null)
        {
            battleFlowManager.OnTurnStarted -= OnTurnStarted;
            battleFlowManager.OnBattleEnded -= OnBattleEnded;
        }
    }

    // 원본 함수 대응: RunButton.OnTurnStarted (Assets/_ProtoType_Merge/KJ/Scripts/UI/RunButton.cs)

    private void OnTurnStarted(int round, BattleCharactor unit)
    {
        if (button != null) button.interactable = unit != null && unit.IsPlayer;
    }

    // 원본 함수 대응: RunButton.OnBattleEnded (Assets/_ProtoType_Merge/KJ/Scripts/UI/RunButton.cs)

    private void OnBattleEnded(BattleResult result)
    {
        if (button != null) button.interactable = false;
    }

    // 원본 함수 대응: RunButton.OnRunButtonClicked (Assets/_ProtoType_Merge/KJ/Scripts/UI/RunButton.cs)

    private void OnRunButtonClicked()
    {
        if (battleFlowManager == null)
        {
            Debug.LogWarning("[JcRunButton] BattleFlowManager를 찾을 수 없습니다.");
            return;
        }

        battleFlowManager.RequestFlee();
    }
}
