using TMPro;
using UnityEngine;

/// <summary>상호작용 전에는 물음표를, 상호작용 중에는 이벤트 대사 말풍선을 표시한다.</summary>
[DisallowMultipleComponent]
public sealed class WorldEventNpcSpeechView : MonoBehaviour
{
    [SerializeField] private WorldEventObject source;
    [SerializeField] private TMP_Text message;
    [SerializeField] private GameObject questionBubble;
    [SerializeField] private GameObject dialogueBubble;

    [SerializeField] private WorldEventNpcPanelSelector answerPanel;

    private string lastId;
    public DHWorldEventType eventTypeID = DHWorldEventType.None;
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
        bool interacting = false;
        eventTypeID = DHWorldEventType.None;
        if (catalog != null && catalog.TryGetEvent(id, out var template) && template != null)
        {
            eventTypeID = template.EventType;
            if (template.SourceType == DHWorldEventSourceType.Npc)
            {
                interacting = request != null;
                text = interacting ? request.MessageText : string.Empty;
                if (request != null && answerPanel != null)
                    answerPanel.ShowForEventType(eventTypeID, template);
            }
            else if (answerPanel != null) answerPanel.HideForEventId(id);
        }
        else if (answerPanel != null) answerPanel.HideForEventId(id);
        if (message != null) message.text = text;
        if (questionBubble != null) questionBubble.SetActive(!interacting);
        if (dialogueBubble != null) dialogueBubble.SetActive(interacting);
    }
}
