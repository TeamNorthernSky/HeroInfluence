using TMPro;
using UnityEngine;

/// <summary>WorldEventObject의 ID로 기본 대사를 조회하고, 상호작용 중에는 수락/거절 대사를 표시한다.</summary>
[DisallowMultipleComponent]
public sealed class WorldEventNpcSpeechView : MonoBehaviour
{
    [SerializeField] private WorldEventObject source;
    [SerializeField] private TMP_Text message;

    [SerializeField] private WorldEventNpcPanelSelector answerPanel;

    private string lastId;
    public char eventTypeID;
    private DHWorldEventCatalog lastCatalog;
    private DHWorldEventPresentationRequest lastRequest;
    private bool initialized;
    private WorldEventNpcPanelSelector lastAnswerPanel;

    private void OnEnable() { initialized = false; Refresh(); }
    private void LateUpdate() { Refresh(); }

    public void Refresh()
    {
        if (source == null) source = GetComponent<WorldEventObject>();
        if (source == null) return;
        var catalog = DHWorldEventCatalog.Instance;
        var request = DHWorldEventRuntimeManager.Instance != null ? DHWorldEventRuntimeManager.Instance.CurrentRequest : null;
        string id = source.WorldEventId;
        eventTypeID = string.IsNullOrEmpty(id) ? '\0' : char.ToUpperInvariant(id[0]);
        if (request != null && request.WorldEventId != id) request = null;
        // 월드 프리팹은 화면 Canvas의 씬 인스턴스를 직접 직렬화할 수 없으므로 런타임에 찾는다.
        if (request != null && answerPanel == null)
            answerPanel = FindFirstObjectByType<WorldEventNpcPanelSelector>(FindObjectsInactive.Include);
        if (initialized && lastId == id && lastCatalog == catalog && ReferenceEquals(lastRequest, request) &&
            lastAnswerPanel == answerPanel && (request == null || answerPanel != null)) return;
        if (answerPanel != null && (lastId != id || request == null))
            answerPanel.HideForEventId(lastId);
        initialized = catalog != null;
        lastId = id;
        lastAnswerPanel = answerPanel;
        lastCatalog = catalog;
        lastRequest = request;
        string text = string.Empty;
        if (catalog != null && catalog.TryGetEvent(id, out var template) &&
            template.SourceType == DHWorldEventSourceType.Npc &&
            WorldEventNpcPanelSelector.MatchesEventType(eventTypeID, template.EventType))
        {
            text = request != null ? request.MessageText : template.Description;
            if (request != null && answerPanel != null)
                answerPanel.ShowForEventType(eventTypeID, template);
        }
        else if (answerPanel != null) answerPanel.HideForEventId(id);
        if (message != null) message.text = text;
    }
}
