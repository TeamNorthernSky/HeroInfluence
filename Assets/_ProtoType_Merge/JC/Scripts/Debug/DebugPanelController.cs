using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DebugPanelController : MonoBehaviour
{
    [Header("탭 버튼")]
    [SerializeField] private Button logTabButton;

    [Header("탭 패널")]
    [SerializeField] private GameObject logTab;

    [Header("로그")]
    [SerializeField] private ScrollRect logScrollRect;
    [SerializeField] private TextMeshProUGUI logContent;
    [SerializeField] private Button logFilterAll;
    [SerializeField] private Button logFilterLog;
    [SerializeField] private Button logFilterWarning;
    [SerializeField] private Button logFilterError;
    [SerializeField] private Button logClear;

    private List<LogEntry> logEntries = new List<LogEntry>();
    private DebugLogType currentFilter = DebugLogType.Log;
    private bool showAll = true;
    private int maxLogEntries = 200;

    private struct LogEntry
    {
        public string timestamp;
        public string message;
        public DebugLogType type;
    }

    private Vector2 initialSize;
    private Vector2 initialPosition;
    private GameObject debugCanvasObj;

    // --- 입력 모드 ---
    private GameObject inputBar;
    private TMP_InputField commandInput;
    private TextMeshProUGUI commandHistory;
    private bool inputModeActive;
    private float timeScaleBackup = 1f;
    private readonly List<string> historyLines = new List<string>();
    private const int MaxHistoryLines = 6;

    public bool IsInputModeActive => inputModeActive;
    public bool IsCanvasActive => debugCanvasObj != null && debugCanvasObj.activeSelf;

    public void Initialize()
    {
        var panelRect = GetComponent<RectTransform>();
        initialSize = panelRect.sizeDelta;
        initialPosition = panelRect.anchoredPosition;
        debugCanvasObj = GetComponentInParent<Canvas>(true).gameObject;

        SetupDragAndResize(panelRect);
        BuildInputBar();

        debugCanvasObj.SetActive(false);
        BindLogButtons();
        BindTabButtons();
        Application.logMessageReceived += HandleUnityLog;
    }

    private void SetupDragAndResize(RectTransform panelRect)
    {
        // TitleBar를 드래그 핸들로 사용
        var titleBar = transform.Find("TitleBar");
        if (titleBar != null)
        {
            var drag = titleBar.gameObject.AddComponent<PanelDragHandler>();
            drag.SetTarget(panelRect);
        }

        // 패널 배경 드래그 (상호작용 요소 위에서는 차단)
        var panelDrag = gameObject.AddComponent<PanelDragHandler>();
        panelDrag.SetTarget(panelRect, null, true);

        // 리사이즈 핸들 생성 (우하단)
        var resizeObj = new GameObject("ResizeHandle");
        resizeObj.transform.SetParent(transform, false);

        var resizeRect = resizeObj.AddComponent<RectTransform>();
        resizeRect.anchorMin = new Vector2(1f, 0f);
        resizeRect.anchorMax = new Vector2(1f, 0f);
        resizeRect.pivot = new Vector2(1f, 0f);
        resizeRect.anchoredPosition = Vector2.zero;
        resizeRect.sizeDelta = new Vector2(20f, 20f);

        var resizeImage = resizeObj.AddComponent<UnityEngine.UI.Image>();
        resizeImage.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);

        var resizeTmpObj = new GameObject("ResizeIcon");
        resizeTmpObj.transform.SetParent(resizeObj.transform, false);

        var resizeTmpRect = resizeTmpObj.AddComponent<RectTransform>();
        resizeTmpRect.anchorMin = Vector2.zero;
        resizeTmpRect.anchorMax = Vector2.one;
        resizeTmpRect.offsetMin = Vector2.zero;
        resizeTmpRect.offsetMax = Vector2.zero;

        var resizeTmp = resizeTmpObj.AddComponent<TextMeshProUGUI>();
        resizeTmp.text = "//";
        resizeTmp.fontSize = 10;
        resizeTmp.alignment = TextAlignmentOptions.Center;
        resizeTmp.color = Color.gray;

        var resizer = resizeObj.AddComponent<PanelResizeHandler>();
        resizer.SetTarget(panelRect, new Vector2(200f, 150f), new Vector2(1200f, 800f));
    }

    public void ResetLayout()
    {
        var panelRect = GetComponent<RectTransform>();
        panelRect.sizeDelta = initialSize;
        panelRect.anchoredPosition = initialPosition;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleUnityLog;
        // 안전장치: 입력 모드에서 패널이 파괴되면 timeScale 복원
        if (inputModeActive)
        {
            Time.timeScale = timeScaleBackup;
            inputModeActive = false;
        }
    }

    public void Toggle()
    {
        debugCanvasObj.SetActive(!debugCanvasObj.activeSelf);
        if (debugCanvasObj.activeSelf)
        {
            RefreshLogDisplay();
        }
        else if (inputModeActive)
        {
            // 패널 비활성 시 입력 모드도 강제 종료
            HideInputBar();
        }
    }

    // --- 입력 모드 (Pause + 커맨드 입력) ---

    public void ShowInputBar()
    {
        if (inputBar == null) BuildInputBar();
        if (inputModeActive) return;
        inputModeActive = true;
        timeScaleBackup = Time.timeScale;
        Time.timeScale = 0f;
        inputBar.SetActive(true);
        commandInput.text = string.Empty;
        commandInput.ActivateInputField();
        commandInput.Select();
    }

    public void HideInputBar()
    {
        if (!inputModeActive) return;
        inputModeActive = false;
        Time.timeScale = timeScaleBackup;
        if (inputBar != null) inputBar.SetActive(false);
        if (commandInput != null) commandInput.DeactivateInputField();
    }

    private void BuildInputBar()
    {
        if (inputBar != null) return;

        var barObj = new GameObject("InputBar");
        barObj.transform.SetParent(transform, false);
        var barRect = barObj.AddComponent<RectTransform>();
        // 패널 하단 외부에 부착(패널이 움직이면 따라 이동)
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(0f, 84f);

        var bg = barObj.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);
        bg.raycastTarget = true;

        // History 영역 (상단 영역 ~56px)
        var historyObj = new GameObject("History");
        historyObj.transform.SetParent(barObj.transform, false);
        var historyRect = historyObj.AddComponent<RectTransform>();
        historyRect.anchorMin = new Vector2(0f, 0f);
        historyRect.anchorMax = new Vector2(1f, 1f);
        historyRect.offsetMin = new Vector2(8f, 32f);
        historyRect.offsetMax = new Vector2(-8f, -4f);
        var historyTmp = historyObj.AddComponent<TextMeshProUGUI>();
        historyTmp.fontSize = 12;
        historyTmp.alignment = TextAlignmentOptions.BottomLeft;
        historyTmp.color = Color.white;
        historyTmp.enableWordWrapping = false;
        historyTmp.overflowMode = TextOverflowModes.Truncate;
        commandHistory = historyTmp;

        // InputField 영역 (하단 28px)
        var inputObj = new GameObject("CommandInput");
        inputObj.transform.SetParent(barObj.transform, false);
        var inputRect = inputObj.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0f);
        inputRect.anchorMax = new Vector2(1f, 0f);
        inputRect.pivot = new Vector2(0.5f, 0f);
        inputRect.anchoredPosition = new Vector2(0f, 2f);
        inputRect.sizeDelta = new Vector2(-16f, 26f);

        var inputBg = inputObj.AddComponent<Image>();
        inputBg.color = new Color(0.12f, 0.12f, 0.12f, 1f);

        var input = inputObj.AddComponent<TMP_InputField>();
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.shouldHideMobileInput = false;

        // Text Area & Placeholder & Text
        var textAreaObj = new GameObject("Text Area");
        textAreaObj.transform.SetParent(inputObj.transform, false);
        var textAreaRect = textAreaObj.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(8f, 2f);
        textAreaRect.offsetMax = new Vector2(-8f, -2f);
        textAreaObj.AddComponent<RectMask2D>();

        var placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(textAreaObj.transform, false);
        var phRect = placeholderObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;
        var phTmp = placeholderObj.AddComponent<TextMeshProUGUI>();
        phTmp.text = "command...";
        phTmp.fontSize = 14;
        phTmp.alignment = TextAlignmentOptions.MidlineLeft;
        phTmp.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        phTmp.enableWordWrapping = false;

        var textObj = new GameObject("Text");
        textObj.transform.SetParent(textAreaObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var textTmp = textObj.AddComponent<TextMeshProUGUI>();
        textTmp.fontSize = 14;
        textTmp.color = Color.white;
        textTmp.alignment = TextAlignmentOptions.MidlineLeft;
        textTmp.enableWordWrapping = false;

        input.textViewport = textAreaRect;
        input.textComponent = textTmp;
        input.placeholder = phTmp;
        input.onSubmit.AddListener(OnCommandSubmit);

        commandInput = input;
        inputBar = barObj;
        inputBar.SetActive(false);
    }

    private void OnCommandSubmit(string line)
    {
        if (!inputModeActive) return;

        if (!string.IsNullOrWhiteSpace(line))
        {
            CheatCommandRegistry.TryExecute(line, out string result);
            PushHistory($"> {line}");
            if (!string.IsNullOrEmpty(result)) PushHistory(result);
        }

        commandInput.text = string.Empty;
        commandInput.ActivateInputField();
        commandInput.Select();
    }

    private void PushHistory(string line)
    {
        historyLines.Add(line);
        while (historyLines.Count > MaxHistoryLines) historyLines.RemoveAt(0);
        if (commandHistory != null)
            commandHistory.text = string.Join("\n", historyLines);
    }

    // --- 탭 전환 ---

    private void BindTabButtons()
    {
        logTabButton.onClick.AddListener(() => SwitchTab(logTab));
        SwitchTab(logTab);
    }

    private void SwitchTab(GameObject activeTab)
    {
        logTab.SetActive(activeTab == logTab);
    }

    // --- 로그 ---

    private void BindLogButtons()
    {
        logFilterAll.onClick.AddListener(() => { showAll = true; RefreshLogDisplay(); });
        logFilterLog.onClick.AddListener(() => { showAll = false; currentFilter = DebugLogType.Log; RefreshLogDisplay(); });
        logFilterWarning.onClick.AddListener(() => { showAll = false; currentFilter = DebugLogType.Warning; RefreshLogDisplay(); });
        logFilterError.onClick.AddListener(() => { showAll = false; currentFilter = DebugLogType.Error; RefreshLogDisplay(); });
        logClear.onClick.AddListener(() => { logEntries.Clear(); RefreshLogDisplay(); });
    }

    private void HandleUnityLog(string condition, string stackTrace, LogType type)
    {
        if (this == null) return;

        DebugLogType debugType;
        switch (type)
        {
            case LogType.Warning:
                debugType = DebugLogType.Warning;
                break;
            case LogType.Error:
            case LogType.Exception:
            case LogType.Assert:
                debugType = DebugLogType.Error;
                break;
            default:
                debugType = DebugLogType.Log;
                break;
        }

        AddLog(condition, debugType);
    }

    public void AddLog(string message, DebugLogType type)
    {
        var entry = new LogEntry
        {
            timestamp = DateTime.Now.ToString("HH:mm:ss"),
            message = message,
            type = type,
        };

        logEntries.Add(entry);

        if (logEntries.Count > maxLogEntries)
        {
            logEntries.RemoveAt(0);
        }

        if (debugCanvasObj != null && debugCanvasObj.activeSelf)
        {
            RefreshLogDisplay();
        }
    }

    private void RefreshLogDisplay()
    {
        var sb = new System.Text.StringBuilder();

        foreach (var entry in logEntries)
        {
            if (!showAll && entry.type != currentFilter)
                continue;

            string colorTag;
            string typeTag;
            switch (entry.type)
            {
                case DebugLogType.Warning:
                    colorTag = "#FFFF00";
                    typeTag = "WAR";
                    break;
                case DebugLogType.Error:
                    colorTag = "#FF4444";
                    typeTag = "ERR";
                    break;
                default:
                    colorTag = "#000000";
                    typeTag = "LOG";
                    break;
            }

            sb.AppendLine($"<color={colorTag}>{entry.timestamp} [{typeTag}] {entry.message}</color>");
        }

        logContent.text = sb.ToString();
        logContent.ForceMeshUpdate();

        LayoutRebuilder.ForceRebuildLayoutImmediate(logContent.rectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(logScrollRect.content);

        Canvas.ForceUpdateCanvases();
        logScrollRect.verticalNormalizedPosition = 0f;
    }
}
