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

    private void Awake()
    {
        if (battleResultText != null)
        {
            battleResultText.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (flowManager != null)
        {
            flowManager.OnTurnStarted += HandleTurnStarted;
            flowManager.OnBattleEnded += HandleBattleEnded;
        }

        if (battleManager != null)
        {
            battleManager.OnActionExecuted += HandleActionExecuted;
        }

        if (inputHandler != null)
        {
            inputHandler.OnActionSelected += HandleActionSelected;
        }
    }

    private void OnDisable()
    {
        if (flowManager != null)
        {
            flowManager.OnTurnStarted -= HandleTurnStarted;
            flowManager.OnBattleEnded -= HandleBattleEnded;
        }

        if (battleManager != null)
        {
            battleManager.OnActionExecuted -= HandleActionExecuted;
        }

        if (inputHandler != null)
        {
            inputHandler.OnActionSelected -= HandleActionSelected;
        }
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

    private void HandleBattleEnded(BattleResult result)
    {
        if (battleResultText == null)
        {
            return;
        }

        battleResultText.gameObject.SetActive(true);
        if (result == BattleResult.Victory)
        {
            battleResultText.text = "전투 결과 : <color=yellow>승리!</color>";
        }
        else
        {
            battleResultText.text = "전투 결과 : <color=red>패배...</color>";
        }
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
