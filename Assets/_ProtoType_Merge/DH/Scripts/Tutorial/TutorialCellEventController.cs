using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public enum TutorialCellEventType
{
    None = 0,
    CompleteTutorial = 1
}

public class TutorialCellEventController : MonoBehaviour
{
    [Serializable]
    public class TutorialCellEventEntry
    {
        [SerializeField] private string eventKey;
        [SerializeField, Min(1)] private int order = 1;
        [SerializeField] private Vector2Int triggerCell;
        [SerializeField] private TutorialCellEventType eventType = TutorialCellEventType.CompleteTutorial;
        [SerializeField] private bool triggerOnce = true;
        [SerializeField] private UnityEvent onTriggered;

        public string EventKey => string.IsNullOrWhiteSpace(eventKey) ? string.Empty : eventKey.Trim();
        public int Order => Mathf.Max(1, order);
        public Vector2Int TriggerCell => triggerCell;
        public TutorialCellEventType EventType => eventType;
        public bool TriggerOnce => triggerOnce;
        public UnityEvent OnTriggered => onTriggered;
    }

    [Header("State")]
    [SerializeField] private bool eventEnabled = true;
    [SerializeField] private bool requireMovementOrder = true;

    [Header("Completion")]
    [SerializeField] private bool loadMainExplorationOnComplete = true;
    [SerializeField] private string mainExplorationSceneName = "DHScene_3";

    [Header("References")]
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private TutorialMovementConstraint movementConstraint;
    [SerializeField] private bool useTutorialProgressRepository = true;

    [Header("Cell Events")]
    [SerializeField] private List<TutorialCellEventEntry> cellEvents = new List<TutorialCellEventEntry>();

    private readonly HashSet<int> triggeredEventIndices = new HashSet<int>();
    private PartyGridMover subscribedParty;
    private bool tutorialCompleted;

    public static event Action TutorialCompleted;

    public bool TutorialCompletedState => tutorialCompleted;

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeParty();
    }

    private void OnDisable()
    {
        UnsubscribeParty();
    }

    private void Update()
    {
        if (subscribedParty == null || !subscribedParty.gameObject.activeInHierarchy)
            SubscribeParty();
    }

    public void ResetTriggeredEvents()
    {
        triggeredEventIndices.Clear();
        tutorialCompleted = false;
    }

    private void HandlePartyGridEntered(Vector2Int grid)
    {
        if (!eventEnabled || tutorialCompleted)
            return;

        int currentOrder = movementConstraint != null ? movementConstraint.CurrentOrder : 0;
        for (int i = 0; i < cellEvents.Count; i++)
        {
            TutorialCellEventEntry entry = cellEvents[i];
            if (entry == null)
                continue;

            string eventKey = ResolveEventKey(i, entry);
            if (useTutorialProgressRepository &&
                TutorialProgressRepository.EnsureInstance()?.IsCellEventCompleted(eventKey) == true)
            {
                continue;
            }

            if (entry.TriggerOnce && triggeredEventIndices.Contains(i))
                continue;

            if (entry.TriggerCell != grid)
                continue;

            if (requireMovementOrder && currentOrder > 0 && entry.Order != currentOrder)
                continue;

            TriggerEntry(i, entry);
            return;
        }
    }

    private void TriggerEntry(int index, TutorialCellEventEntry entry)
    {
        if (entry.TriggerOnce)
            triggeredEventIndices.Add(index);

        if (useTutorialProgressRepository)
            TutorialProgressRepository.EnsureInstance()?.MarkCellEventCompleted(ResolveEventKey(index, entry));

        entry.OnTriggered?.Invoke();
        ExecuteEvent(entry.EventType);
    }

    private void ExecuteEvent(TutorialCellEventType eventType)
    {
        switch (eventType)
        {
            case TutorialCellEventType.None:
                break;
            case TutorialCellEventType.CompleteTutorial:
                CompleteTutorial();
                break;
            default:
                Debug.LogWarning($"[TutorialCellEventController] Unsupported tutorial event type: {eventType}", this);
                break;
        }
    }

    private void CompleteTutorial()
    {
        if (tutorialCompleted)
            return;

        tutorialCompleted = true;
        if (useTutorialProgressRepository)
            TutorialProgressRepository.EnsureInstance()?.MarkTutorialCompleted();

        TutorialCompleted?.Invoke();

        if (!loadMainExplorationOnComplete)
            return;

        if (GameSceneManager.Instance != null && string.IsNullOrWhiteSpace(mainExplorationSceneName))
        {
            GameSceneManager.Instance.LoadExploration();
            return;
        }

        if (!string.IsNullOrWhiteSpace(mainExplorationSceneName))
            SceneManager.LoadScene(mainExplorationSceneName.Trim());
    }

    private void ResolveReferences()
    {
        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();

        if (movementConstraint == null)
            movementConstraint = FindFirstObjectByType<TutorialMovementConstraint>();
    }

    private void SubscribeParty()
    {
        ResolveReferences();

        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (subscribedParty == party)
            return;

        UnsubscribeParty();
        subscribedParty = party;
        if (subscribedParty != null)
            subscribedParty.GridEntered += HandlePartyGridEntered;
    }

    private string ResolveEventKey(int index, TutorialCellEventEntry entry)
    {
        if (entry != null && !string.IsNullOrWhiteSpace(entry.EventKey))
            return entry.EventKey;

        if (entry == null)
            return $"cell_event_{index}";

        return $"{entry.EventType}_{entry.Order}_{entry.TriggerCell.x}_{entry.TriggerCell.y}";
    }

    private void UnsubscribeParty()
    {
        if (subscribedParty != null)
            subscribedParty.GridEntered -= HandlePartyGridEntered;

        subscribedParty = null;
    }
}
