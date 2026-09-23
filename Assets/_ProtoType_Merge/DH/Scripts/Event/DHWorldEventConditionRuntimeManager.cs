using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DHWorldEventConditionRuntimeManager : MonoBehaviour
{
    private const string RootName = "[DH_WorldEventConditionRuntime]";

    public static DHWorldEventConditionRuntimeManager Instance { get; private set; }

    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private bool conditionEventsEnabled = true;
    [SerializeField] private bool checkOnEnable = true;
    [SerializeField] private bool logChecks;

    private bool checkQueued;
    private bool subscribedToEconomy;

    public static DHWorldEventConditionRuntimeManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        DHWorldEventConditionRuntimeManager existing = FindFirstObjectByType<DHWorldEventConditionRuntimeManager>();
        if (existing != null)
            return existing;

        GameObject root = GameObject.Find(RootName);
        if (root == null)
            root = new GameObject(RootName);

        return root.AddComponent<DHWorldEventConditionRuntimeManager>();
    }

    public bool ConditionEventsEnabled
    {
        get => conditionEventsEnabled;
        set
        {
            conditionEventsEnabled = value;
            if (conditionEventsEnabled)
                RequestCheck();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();

        if (checkOnEnable)
            RequestCheck();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RequestCheck()
    {
        if (!Application.isPlaying || !conditionEventsEnabled || checkQueued)
            return;

        EnsureEconomySubscription();
        checkQueued = true;
        StartCoroutine(CheckAfterFrame());
    }

    private IEnumerator CheckAfterFrame()
    {
        yield return null;
        checkQueued = false;
        TryStartFirstSatisfiedEvent();
    }

    private bool TryStartFirstSatisfiedEvent()
    {
        if (!conditionEventsEnabled)
            return false;

        DHWorldEventRuntimeManager runtimeManager = DHWorldEventRuntimeManager.EnsureInstance();
        if (runtimeManager == null || runtimeManager.IsRunning)
            return false;

        DHWorldEventCatalog catalog = DHWorldEventCatalog.Instance;
        if (catalog == null)
            return false;

        PartyGridMover party = ResolvePlayerParty();
        if (party == null)
            return false;

        int currentTurn = turnManager != null ? turnManager.GetDay() : 0;
        IReadOnlyList<DHWorldEventTemplate> events = catalog.GetAllEvents();
        for (int i = 0; i < events.Count; i++)
        {
            DHWorldEventTemplate template = events[i];
            if (template == null || template.SourceType != DHWorldEventSourceType.Condition)
                continue;

            string progressKey = DHWorldEventRuntimeManager.BuildProgressKey(template.WorldEventId);
            if (MapProgressRepository.Instance != null && MapProgressRepository.Instance.IsEventCompleted(progressKey))
                continue;

            if (!DHWorldEventTriggerConditionEvaluator.IsSatisfied(template, party, currentTurn, out string reason))
            {
                if (logChecks && !string.IsNullOrWhiteSpace(reason))
                    Debug.Log($"[DHWorldEventConditionRuntime] Skip '{template.WorldEventId}': {reason}", this);
                continue;
            }

            return runtimeManager.TryStartConditionEvent(template, party, RequestCheck);
        }

        return false;
    }

    private void Subscribe()
    {
        if (turnManager != null)
        {
            turnManager.DayAdvanced -= HandleDayAdvanced;
            turnManager.DayAdvanced += HandleDayAdvanced;
        }

        if (Game.Economy != null)
        {
            Game.Economy.OnResourceChanged -= HandleResourceChanged;
            Game.Economy.OnResourceChanged += HandleResourceChanged;
            subscribedToEconomy = true;
        }

        DHWorldEventRuntimeManager runtimeManager = DHWorldEventRuntimeManager.EnsureInstance();
        runtimeManager.PresentationClosed -= HandlePresentationClosed;
        runtimeManager.PresentationClosed += HandlePresentationClosed;
    }

    private void EnsureEconomySubscription()
    {
        if (subscribedToEconomy || Game.Economy == null)
            return;

        Game.Economy.OnResourceChanged -= HandleResourceChanged;
        Game.Economy.OnResourceChanged += HandleResourceChanged;
        subscribedToEconomy = true;
    }

    private void Unsubscribe()
    {
        if (turnManager != null)
            turnManager.DayAdvanced -= HandleDayAdvanced;

        if (subscribedToEconomy && Game.Economy != null)
            Game.Economy.OnResourceChanged -= HandleResourceChanged;

        subscribedToEconomy = false;

        if (DHWorldEventRuntimeManager.Instance != null)
            DHWorldEventRuntimeManager.Instance.PresentationClosed -= HandlePresentationClosed;
    }

    private void HandleDayAdvanced(int _)
    {
        RequestCheck();
    }

    private void HandleResourceChanged(ResourceType _, int __)
    {
        RequestCheck();
    }

    private void HandlePresentationClosed(DHWorldEventPresentationRequest _)
    {
        RequestCheck();
    }

    private PartyGridMover ResolvePlayerParty()
    {
        ResolveReferences();
        return partyRegistry != null ? partyRegistry.PlayerParty : null;
    }

    private void ResolveReferences()
    {
        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();
    }
}
