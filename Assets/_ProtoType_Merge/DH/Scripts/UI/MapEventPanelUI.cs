using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [JC 260515 머지후처리] ResourceManager 직접 의존 폐기 → Game.Economy(=GameManager.Economy) 사용
// [JC 260515 추가] IsAnyActive 정적 추적 — PartyInfoTrigger 등 외부에서 MapEventPanel 활성 여부 가드용
public class MapEventPanelUI : MonoBehaviour
{
    private static int activeCount = 0;
    public static bool IsAnyActive => activeCount > 0;

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text eventPromptText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private MapEventObject currentMapEvent;
    private bool isPanelActive = false;

    private void Awake()
    {
        if (yesButton != null)
            yesButton.onClick.AddListener(OnOkButtonClicked);

        if (noButton != null)
            noButton.onClick.AddListener(OnNoButtonClicked);

        HidePanel();
    }

    private void OnEnable()
    {
        MapEventObject.EventInteracted += HandleEventInteracted;
    }

    private void OnDestroy()
    {
        if (yesButton != null)
            yesButton.onClick.RemoveListener(OnOkButtonClicked);

        if (noButton != null)
            noButton.onClick.RemoveListener(OnNoButtonClicked);
    }

    private void HandleEventInteracted(MapEventObject mapEvent)
    {
        if (mapEvent == null)
            return;

        currentMapEvent = mapEvent;

        if (eventPromptText != null)
            eventPromptText.text = $"Will you try this {mapEvent.EventKey}?";

        if (costText != null)
            costText.text = $"- {mapEvent.RequireAmount} {mapEvent.RequireResource}";

        if (yesButton != null)
        {
            yesButton.interactable = Game.Economy != null
                && Game.Economy.Has(mapEvent.RequireResource, mapEvent.RequireAmount);
        }

        ShowPanel();
    }

    private void OnOkButtonClicked()
    {
        if (currentMapEvent == null) return;

        bool success = currentMapEvent.TryExecuteEvent();
        if (success)
        {
            HidePanel();
        }
    }

    private void OnNoButtonClicked()
    {
        HidePanel();
    }

    private void ShowPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);
        if (!isPanelActive)
        {
            isPanelActive = true;
            activeCount++;
        }
    }

    private void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        if (isPanelActive)
        {
            isPanelActive = false;
            activeCount = Mathf.Max(0, activeCount - 1);
        }

        currentMapEvent = null;
    }

    private void OnDisable()
    {
        MapEventObject.EventInteracted -= HandleEventInteracted;
        // 컴포넌트가 disable되어도 activeCount 누락 방지
        if (isPanelActive)
        {
            isPanelActive = false;
            activeCount = Mathf.Max(0, activeCount - 1);
        }
    }
}
