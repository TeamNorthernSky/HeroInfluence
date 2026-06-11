using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

// [JC 260514 머지후처리] ResourceManager 직접 의존 폐기 → Game.Economy(=GameManager.Economy) 사용
// [JC 260514 추가] IsAnyActive 정적 추적 — PartyInfoTrigger 등 외부에서 MapEventPanel 활성 여부 가드용
public class MapEventPanelUI : MonoBehaviour
{
    private static int activeCount = 0;
    public static bool IsAnyActive => activeCount > 0;

    [SerializeField] private GameObject panelRoot;
    [FormerlySerializedAs("eventPromptText")]
    [SerializeField] private TMP_Text descriptionText;
    [FormerlySerializedAs("costText")]
    [SerializeField] private TMP_Text effectAmountText;
    [SerializeField] private TMP_Text costAmountText;
    [SerializeField] private Image eventImage;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("Display Presets")]
    [SerializeField] private MapEventDisplayEntry[] displayEntries = Array.Empty<MapEventDisplayEntry>();

    private MapEventObject currentMapEvent;
    private PartyGridMover currentParty;
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

    private void HandleEventInteracted(MapEventObject mapEvent, PartyGridMover party)
    {
        if (mapEvent == null || party == null)
            return;

        currentMapEvent = mapEvent;
        currentParty = party;

        MapEventDisplayEntry displayEntry = GetDisplayEntry(mapEvent.EventType);
        string resourceName = GetResourceDisplayName(mapEvent.RequireResource);
        string effectText = GetEffectDisplayText(mapEvent.EventType, mapEvent.EffectAmount);

        if (descriptionText != null)
            descriptionText.text = $"{resourceName}을 '{mapEvent.RequireAmount}' 지불하고\n모든 영웅의 '{effectText}'";

        if (effectAmountText != null)
            effectAmountText.text = mapEvent.EventType == MapEventType.Heal
                ? "Full"
                : Mathf.Max(0, mapEvent.EffectAmount).ToString();

        if (costAmountText != null)
            costAmountText.text = Mathf.Max(0, mapEvent.RequireAmount).ToString();

        ApplyImage(eventImage, displayEntry.EventSprite);

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

        bool success = currentMapEvent.TryExecuteEvent(currentParty);
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
        currentParty = null;
    }

    private void OnDisable()
    {
        MapEventObject.EventInteracted -= HandleEventInteracted;
        currentMapEvent = null;
        currentParty = null;
        // 컴포넌트가 disable되어도 activeCount 누락 방지
        if (isPanelActive)
        {
            isPanelActive = false;
            activeCount = Mathf.Max(0, activeCount - 1);
        }
    }

    private MapEventDisplayEntry GetDisplayEntry(MapEventType eventType)
    {
        if (displayEntries == null)
            return default;

        for (int i = 0; i < displayEntries.Length; i++)
        {
            MapEventDisplayEntry entry = displayEntries[i];
            if (entry.EventType == eventType)
                return entry;
        }

        return default;
    }

    private static string GetEffectDisplayText(MapEventType eventType, int effectAmount)
    {
        int amount = Mathf.Max(0, effectAmount);
        return eventType switch
        {
            MapEventType.TrainingAtk => $"공격력 + {amount}",
            MapEventType.TrainingHp => $"최대 체력 + {amount}",
            MapEventType.Heal => "현재 체력 회복",
            _ => eventType.ToString()
        };
    }

    private static string GetResourceDisplayName(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Money => "자금",
            ResourceType.Chip => "히어로 메달",
            ResourceType.Crystal => "아티펙트 수정",
            ResourceType.Supply => "건설 자재",
            _ => resourceType.ToString()
        };
    }

    private static void ApplyImage(Image targetImage, Sprite sprite)
    {
        if (targetImage == null)
            return;

        targetImage.sprite = sprite;
        targetImage.enabled = sprite != null;
    }
}

[Serializable]
public struct MapEventDisplayEntry
{
    [SerializeField] private MapEventType eventType;
    [SerializeField] private Sprite eventSprite;

    public MapEventType EventType => eventType;
    public Sprite EventSprite => eventSprite;
}
