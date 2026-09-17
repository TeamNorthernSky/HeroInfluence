using UnityEngine;
using UnityEngine.UI;

public class BattleUIManager : MonoBehaviour
{
    [SerializeField] private BattleFlowManager flowManager;

    [Header("Result Panel")]
    [SerializeField] private BattleResultPanel victoryResultPrefab;
    [SerializeField] private BattleResultPanel defeatResultPrefab;
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
    [SerializeField] private TurnArrow turnArrow;
    [SerializeField] private Vector2 turnArrowScreenOffset = new Vector2(0f, 120f);

    public Material ClearMaterial;
    public Material TargetMaterial;

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

    private void Update()
    {
        UpdateTurnArrowPosition();
    }

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
