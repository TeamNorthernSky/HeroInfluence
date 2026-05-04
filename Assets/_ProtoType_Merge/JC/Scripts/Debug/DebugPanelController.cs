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

    public void Initialize()
    {
        var panelRect = GetComponent<RectTransform>();
        initialSize = panelRect.sizeDelta;
        initialPosition = panelRect.anchoredPosition;
        debugCanvasObj = GetComponentInParent<Canvas>(true).gameObject;

        SetupDragAndResize(panelRect);

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
    }

    public void Toggle()
    {
        debugCanvasObj.SetActive(!debugCanvasObj.activeSelf);
        if (debugCanvasObj.activeSelf)
        {
            RefreshLogDisplay();
        }
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
