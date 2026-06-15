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

    [Header("Turn Arrow")]
    [SerializeField] private TurnArrow turnArrow;
    [SerializeField] private Vector3 turnArrowWorldOffset = new Vector3(0f, 100f, 0f);
    [SerializeField] private Camera worldCamera;

    private void Awake()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

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

    private void HandleTurnStarted(int roundIndex, BattleCharactor unit)
    {
        if (totalTurnText != null)
        {
            totalTurnText.text = $"Round : {roundIndex}";
        }

        if (currentTurnText != null)
        {
            string colorHex = (unit != null && unit.IsPlayer) ? "#00FF00" : "#FF0000";
            string unitName = unit != null ? unit.UnitName : "Unknown";
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
    public void ShowBattleResultUI(BattleResult result)
    {
        if (battleResultText == null)
            return;

        battleResultText.gameObject.SetActive(true);
        if (result == BattleResult.Victory)
        {
            battleResultText.text = "전투 결과 : <color=yellow>승리!</color>";
        }
        else
        {
            // [JC 260513] Victory 외(Defeat/Escape/Cancelled)는 패배 표시.
            // Escape 전용 UI가 필요해지면 분기 추가.
            battleResultText.text = "전투 결과 : <color=red>패배...</color>";
        }
    }

    private void Update()
    {
        UpdateTurnArrowPosition();
    }

    private void UpdateTurnArrowPosition()
    {
        if (flowManager == null || turnArrow == null || worldCamera == null)
        {
            turnArrow?.SetEnabled(false);
            return;
        }

        BattleCharactor currentBattleCharacter = flowManager.CurrentUnit;
        if (currentBattleCharacter == null || currentBattleCharacter.IsDead)
        {
            turnArrow.SetEnabled(false);
            return;
        }

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(
            currentBattleCharacter.transform.position )+ turnArrowWorldOffset;
        if (screenPosition.z <= 0f)
        {
            turnArrow.SetEnabled(false);
            return;
        }

        turnArrow.SetScreenPosition(screenPosition);
        turnArrow.SetEnabled(true);
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

        totalTurnText = CreateOrReplaceText(canvas.transform, "TotalTurnText", new Vector2(30f, 110f));
        currentTurnText = CreateOrReplaceText(canvas.transform, "CurrentTurnText", new Vector2(30f, 70f));
        currentSkillText = CreateOrReplaceText(canvas.transform, "CurrentSkillText", new Vector2(30f, 30f));
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
