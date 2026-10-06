// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleUIManager.cs
// 원본 객체: BattleUIManager -> JcBattleUIManager
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using UnityEngine;
using UnityEngine.UI;

public class JcBattleUIManager : MonoBehaviour
{
    [Tooltip("JC 전투의 현재 턴과 종료 상태를 읽는 흐름입니다.")]
    [SerializeField] private JcBattleFlowManager flowManager;

    [Header("Result Panel")]
    [Tooltip("기존 결과창 호환용 프리팹입니다. JC 세션의 승패는 전용 초기화 메시지박스로 처리합니다.")]
    [SerializeField] private BattleResultPanel victoryResultPrefab;
    [Tooltip("기존 결과창 호환용 프리팹입니다. JC 세션은 보상이나 영속 데이터를 저장하지 않습니다.")]
    [SerializeField] private BattleResultPanel defeatResultPrefab;
    [Tooltip("기존 결과창 호환용 부모입니다. JC 승패 메시지박스는 이 참조를 사용하지 않습니다.")]
    [SerializeField] private Transform resultPanelParent;
    [Tooltip("전투씬에 미리 배치한 결과창입니다. 연결하면 이 인스턴스를 사용하고, 비워 두면 기존 승패 프리팹을 생성합니다.")]
    [SerializeField] private BattleResultPanel sceneResultPanel;

    [Header("Result HUD State")]
    [Tooltip("결과창 표시 시 숨길 하단 패널과 버튼의 호버·선택 효과입니다. 결과창과 확인 버튼은 포함하지 않습니다. 비우면 숨기는 대상이 없습니다.")]
    [SerializeField] private GameObject[] hideOnResult;
    [Tooltip("결과창 표시 시 클릭을 막을 버튼입니다. 오브젝트는 유지하고 각 Button의 비활성 색상을 사용합니다. 비우면 버튼 상태를 변경하지 않습니다.")]
    [SerializeField] private Button[] disableOnResult;

    [Header("Turn Arrow")]
    [Tooltip("기존 턴 화살표를 표시합니다. 노란 셀로 현재 턴을 표시하는 개편 전투씬에서는 끕니다. 다른 씬은 기존 설정을 유지합니다.")]
    [SerializeField] private bool showTurnArrow = true;
    [Tooltip("기존 현재 턴 화살표입니다. 셀로 현재 턴을 표시하는 JC 씬에서는 화살표 옵션을 끕니다.")]
    [SerializeField] private TurnArrow turnArrow;
    [Tooltip("현재 턴 화살표의 화면 위치 보정입니다. 화면 좌표 단위로 X는 오른쪽, Y는 위쪽입니다.")]
    [SerializeField] private Vector2 turnArrowScreenOffset = new Vector2(0f, 120f);

    [Tooltip("타깃을 선택하지 않은 셀의 기본 재질입니다. 셀 표시는 JC 그리드 제어가 담당합니다.")]
    public Material ClearMaterial;
    [Tooltip("선택 가능한 대상 셀의 재질입니다. 기존 UI 호환 참조입니다.")]
    public Material TargetMaterial;

    // 원본 함수 대응: BattleUIManager.Awake (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleUIManager.cs)

    private void Awake()
    {
        if (turnArrow == null)
        {
            GameObject turnArrowObject = GameObject.Find("TurnArrow");
            if (turnArrowObject != null)
            {
                turnArrow = turnArrowObject.GetComponent<TurnArrow>();
            }
        }

        turnArrow?.SetEnabled(false);
        turnArrowScreenOffset = new Vector2(0f, 150f);

        if (resultPanelParent != null)
        {
            resultPanelParent.gameObject.SetActive(false);
        }
    }

    // 원본 함수 대응: BattleUIManager.ShowBattleResultUI (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleUIManager.cs)

    public BattleResultPanel ShowBattleResultUI(BattleResult result, BattleRewardPlan plan = null)
    {
        if (sceneResultPanel != null)
        {
            ApplyResultHudState();
            if (resultPanelParent != null) resultPanelParent.gameObject.SetActive(true);
            sceneResultPanel.gameObject.SetActive(true);
            sceneResultPanel.Show(result, plan);
            return sceneResultPanel;
        }
        BattleResultPanel prefab = result == BattleResult.Victory ? victoryResultPrefab : defeatResultPrefab;
        if (prefab == null || resultPanelParent == null) return null;

        ApplyResultHudState();
        resultPanelParent.gameObject.SetActive(true);

        BattleResultPanel instance = Instantiate(prefab, resultPanelParent, false);
        instance.Show(result, plan);
        return instance;
    }

    // 원본 함수 대응: BattleUIManager.ApplyResultHudState (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleUIManager.cs)

    private void ApplyResultHudState()
    {
        if (hideOnResult != null)
        {
            foreach (GameObject target in hideOnResult)
                if (target != null) target.SetActive(false);
        }

        if (disableOnResult != null)
        {
            foreach (Button button in disableOnResult)
                if (button != null) button.interactable = false;
        }
    }

    // 원본 함수 대응: BattleUIManager.Update (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleUIManager.cs)

    private void Update()
    {
        UpdateTurnArrowPosition();
    }

    // 원본 함수 대응: BattleUIManager.UpdateTurnArrowPosition (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleUIManager.cs)

    private void UpdateTurnArrowPosition()
    {
        if (!showTurnArrow || flowManager == null || turnArrow == null)
        {
            turnArrow?.Follow(null);
            return;
        }

        BattleCharactor currentBattleCharacter = flowManager.CurrentUnit;
        if (currentBattleCharacter == null || currentBattleCharacter.IsDead)
        {
            turnArrow.Follow(null);
            return;
        }

        turnArrow.Follow(currentBattleCharacter.transform, turnArrowScreenOffset);
    }
}
