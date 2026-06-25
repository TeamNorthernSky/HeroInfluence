using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleUIManager : MonoBehaviour
{
    [SerializeField] private BattleFlowManager flowManager;
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private InputHandler inputHandler;

    [Space(10)]
    [SerializeField] private TextMeshProUGUI totalTurnText;
    [SerializeField] private TextMeshProUGUI currentTurnText;
    [SerializeField] private TextMeshProUGUI currentSkillText;
    [SerializeField] private TextMeshProUGUI battleResultText;


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

        if (battleResultText != null)
        {
            battleResultText.gameObject.SetActive(false);
        }

        turnArrow?.SetEnabled(false);

        turnArrowScreenOffset = new Vector2(0f, 150f);

        if (resultPanelParent != null)
        {
            resultPanelParent.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (flowManager != null)
            flowManager.OnTurnStarted += HandleTurnStarted;

        if (battleManager != null)
            battleManager.OnActionExecuted += HandleActionExecuted;

        if (inputHandler != null)
            inputHandler.OnActionSelected += HandleActionSelected;
    }

    private void OnDisable()
    {
        if (flowManager != null)
            flowManager.OnTurnStarted -= HandleTurnStarted;

        if (battleManager != null)
            battleManager.OnActionExecuted -= HandleActionExecuted;

        if (inputHandler != null)
            inputHandler.OnActionSelected -= HandleActionSelected;
    }

    private void HandleTurnStarted(int currentTurn, BattleCharactor unit)
    {
        if (totalTurnText != null)
        {
            totalTurnText.text = $"Turn : {currentTurn}";
        }

        if (currentTurnText != null)
        {
            string colorHex = (unit != null && unit.IsPlayer) ? "#00FF00" : "#FF0000";
            string unitName = unit != null ? unit.DisplayName : "Unknown"; // [JC 260621] 클래스명 아닌 히어로명
            currentTurnText.text = $"현재 턴 : <color={colorHex}>{unitName}</color>";
        }

        if (currentSkillText != null)
        {
            currentSkillText.text = "대기 중...";
        }
    }

    private void HandleActionExecuted(string actionName)
    {
        if (currentSkillText != null)
        {
            currentSkillText.text = $"사용 스킬 : {actionName}";
        }
    }

    private void HandleActionSelected(string actionName)
    {
        if (currentSkillText != null)
        {
            currentSkillText.text = $"선택 스킬 : <color=yellow>{actionName}</color>";
        }
    }

    /// <summary>PostBattleSequence에서 명시적으로 호출됩니다. OnBattleEnded 직접 구독 불필요.</summary>
    public BattleResultPanel ShowBattleResultUI(BattleResult result, BattleRewardPlan plan = null)
    {
        if (battleResultText != null)
        {
            battleResultText.gameObject.SetActive(true);
            battleResultText.text = result == BattleResult.Victory
                ? "전투 결과 : <color=yellow>승리!</color>"
                : "전투 결과 : <color=red>패배...</color>";
        }

        resultPanelParent.gameObject.SetActive(true);

        BattleResultPanel prefab = result == BattleResult.Victory ? victoryResultPrefab : defeatResultPrefab;
        if (prefab == null || resultPanelParent == null) return null;


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

#if UNITY_EDITOR
    [ContextMenu("전투 UI 생성 (좌측 하단)")]
    private void GenerateUIInEditor()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("BattleCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        totalTurnText    = CreateOrReplaceText(canvas.transform, "TotalTurnText"   , new Vector2(30f, 110f));
        currentTurnText  = CreateOrReplaceText(canvas.transform, "CurrentTurnText" , new Vector2(30f,  70f));
        currentSkillText = CreateOrReplaceText(canvas.transform, "CurrentSkillText", new Vector2(30f,  30f));
        battleResultText = CreateOrReplaceText(canvas.transform, "BattleResultText", new Vector2(30f, 150f));
        battleResultText.fontSize = 30f;
        battleResultText.text = "전투 결과 : -";
        battleResultText.gameObject.SetActive(false);

        UnityEditor.EditorUtility.SetDirty(this);
    }

    private static TextMeshProUGUI CreateOrReplaceText(Transform parent, string objectName, Vector2 anchoredPosition)
    {
        Transform existing = parent.Find(objectName);
        GameObject textGo = existing != null ? existing.gameObject : new GameObject(objectName);
        if (existing == null)
        {
            textGo.transform.SetParent(parent, false);
        }

        RectTransform rect = textGo.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = textGo.AddComponent<RectTransform>();
        }

        TextMeshProUGUI text = textGo.GetComponent<TextMeshProUGUI>();
        if (text == null)
        {
            text = textGo.AddComponent<TextMeshProUGUI>();
        }

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(500f, 30f);

        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Left;
        text.text = objectName;

        return text;
    }
#endif
}
