using UnityEngine;

public class BattleUIManager : MonoBehaviour
{
    [SerializeField] private BattleFlowManager flowManager;

    [Header("Result Panel")]
    [SerializeField] private BattleResultPanel victoryResultPrefab;
    [SerializeField] private BattleResultPanel defeatResultPrefab;
    [SerializeField] private Transform resultPanelParent;

    [Header("Turn Arrow")]
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
        BattleResultPanel prefab = result == BattleResult.Victory ? victoryResultPrefab : defeatResultPrefab;
        if (prefab == null || resultPanelParent == null) return null;

        resultPanelParent.gameObject.SetActive(true);

        BattleResultPanel instance = Instantiate(prefab, resultPanelParent, false);
        instance.Show(result, plan);
        return instance;
    }

    private void Update()
    {
        UpdateTurnArrowPosition();
    }

    private void UpdateTurnArrowPosition()
    {
        if (flowManager == null || turnArrow == null)
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
