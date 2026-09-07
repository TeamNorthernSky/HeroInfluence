using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DHWorldEventRuntimeManager : MonoBehaviour
{
    private const string RootName = "[DH_WorldEventRuntime]";

    public static DHWorldEventRuntimeManager Instance { get; private set; }

    [SerializeField] private bool completeImmediatelyUntilUiIsReady;
    [SerializeField] private bool logEvents = true;
    [SerializeField] private string choiceCancelText = "취소";

    private bool isRunning;
    private WorldEventObject runningSource;
    private PartyGridMover runningParty;
    private DHWorldEventTemplate runningTemplate;
    private Action runningClosedCallback;
    private DHWorldEventPresentationRequest currentRequest;

    public bool IsRunning => isRunning;
    public DHWorldEventPresentationRequest CurrentRequest => currentRequest;

    public event Action<DHWorldEventPresentationRequest> PresentationChanged;
    public event Action<DHWorldEventPresentationRequest> PresentationClosed;

    public static DHWorldEventRuntimeManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject root = GameObject.Find(RootName);
        if (root == null)
            root = new GameObject(RootName);

        return root.AddComponent<DHWorldEventRuntimeManager>();
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

    public bool TryStartEvent(WorldEventObject source, PartyGridMover party, Action closedCallback = null)
    {
        if (isRunning || source == null)
            return false;

        DHWorldEventCatalog catalog = DHWorldEventCatalog.Instance;
        if (catalog == null || !catalog.TryGetEvent(source.WorldEventId, out DHWorldEventTemplate template) || template == null)
        {
            Debug.LogWarning($"[DHWorldEventRuntime] World event data was not found: {source.WorldEventId}", source);
            return false;
        }

        if (template.EventType == DHWorldEventType.Consume && template.SourceType == DHWorldEventSourceType.Npc)
            return StartConsumeNpcEvent(source, party, template, closedCallback);

        if (template.EventType == DHWorldEventType.Reward && template.SourceType == DHWorldEventSourceType.Npc)
            return StartRewardNpcEvent(source, party, template, closedCallback);

        if (template.EventType == DHWorldEventType.Choice && template.SourceType == DHWorldEventSourceType.Npc)
            return StartChoiceNpcEvent(source, party, template, closedCallback);

        if (!completeImmediatelyUntilUiIsReady)
            return false;

        isRunning = true;
        runningSource = source;
        runningParty = party;
        runningTemplate = template;
        runningClosedCallback = closedCallback;
        source.Complete();
        EndCurrentEvent(true);
        return true;
    }

    public bool SelectChoice(string choiceId)
    {
        if (!CanHandleInput(DHWorldEventPresentationStep.Description) ||
            runningTemplate.EventType != DHWorldEventType.Choice ||
            runningTemplate.SourceType != DHWorldEventSourceType.Npc)
        {
            return false;
        }

        if (!TryFindCurrentChoice(choiceId, out DHWorldEventChoicePresentationOption option))
            return false;

        if (option.IsCancel)
        {
            EndCurrentEvent(false);
            return true;
        }

        if (!option.IsEnabled)
        {
            currentRequest = BuildChoiceRequest(option.DisabledReason);
            PresentationChanged?.Invoke(currentRequest);
            return false;
        }

        if (!DHWorldEventChoiceResolver.TryResolveResultGroupId(option, out string resultGroupId, out bool succeeded))
        {
            currentRequest = BuildChoiceRequest("Choice result group was not found.");
            PresentationChanged?.Invoke(currentRequest);
            return false;
        }

        DHWorldEventCatalog catalog = DHWorldEventCatalog.Instance;
        if (catalog == null || !catalog.TryGetChoiceResults(resultGroupId, out IReadOnlyList<DHWorldEventResultTemplate> results))
        {
            currentRequest = BuildChoiceRequest($"Choice result data was not found: {resultGroupId}");
            PresentationChanged?.Invoke(currentRequest);
            return false;
        }

        if (!DHWorldEventResultApplier.TryApplyChoiceResults(results, runningParty, out string reason))
        {
            currentRequest = BuildChoiceRequest(reason);
            PresentationChanged?.Invoke(currentRequest);
            return false;
        }

        if (logEvents)
        {
            Debug.Log(
                $"[DHWorldEventRuntime] Choice event '{runningTemplate.WorldEventId}' selected '{option.ChoiceId}', success={succeeded}, resultGroup={resultGroupId}.",
                runningSource);
        }

        runningSource.Complete();
        EndCurrentEvent(true);
        return true;
    }

    public bool SelectProceed()
    {
        if (!CanHandleInput(DHWorldEventPresentationStep.Description))
            return false;

        if (!DHWorldEventResultApplier.CanApply(runningTemplate, runningParty, out string reason))
        {
            currentRequest = BuildRequest(DHWorldEventPresentationStep.Description, runningTemplate.Description, false, reason);
            PresentationChanged?.Invoke(currentRequest);
            return false;
        }

        if (runningTemplate.EventType == DHWorldEventType.Reward && runningTemplate.SourceType == DHWorldEventSourceType.Npc)
            return ConfirmRewardProceed();

        currentRequest = BuildRequest(DHWorldEventPresentationStep.Accept, runningTemplate.AcceptText, true, string.Empty);
        PresentationChanged?.Invoke(currentRequest);
        return true;
    }

    public bool SelectDecline()
    {
        if (!CanHandleInput(DHWorldEventPresentationStep.Description))
            return false;

        currentRequest = BuildRequest(DHWorldEventPresentationStep.Cancel, runningTemplate.CancelText, true, string.Empty);
        PresentationChanged?.Invoke(currentRequest);
        return true;
    }

    private bool StartRewardNpcEvent(
        WorldEventObject source,
        PartyGridMover party,
        DHWorldEventTemplate template,
        Action closedCallback)
    {
        isRunning = true;
        runningSource = source;
        runningParty = party;
        runningTemplate = template;
        runningClosedCallback = closedCallback;

        currentRequest = BuildRequest(DHWorldEventPresentationStep.Description, template.Description, true, string.Empty);

        if (logEvents)
        {
            Debug.Log(
                $"[DHWorldEventRuntime] Start reward NPC event '{template.WorldEventId}' zone={template.ZoneNo}.",
                source);
        }

        PresentationChanged?.Invoke(currentRequest);
        return true;
    }

    private bool ConfirmRewardProceed()
    {
        if (!DHWorldEventResultApplier.TryApplyRewards(runningTemplate, runningParty, out string reason))
        {
            currentRequest = BuildRequest(DHWorldEventPresentationStep.Description, runningTemplate.Description, false, reason);
            PresentationChanged?.Invoke(currentRequest);
            return false;
        }

        runningSource.Complete();
        EndCurrentEvent(true);
        return true;
    }

    public bool ConfirmAccept()
    {
        if (!CanHandleInput(DHWorldEventPresentationStep.Accept))
            return false;

        if (!DHWorldEventResultApplier.TryApply(runningTemplate, runningParty, out string reason))
        {
            currentRequest = BuildRequest(DHWorldEventPresentationStep.Description, runningTemplate.Description, false, reason);
            PresentationChanged?.Invoke(currentRequest);
            return false;
        }

        runningSource.Complete();
        EndCurrentEvent(true);
        return true;
    }

    public bool ConfirmCancel()
    {
        if (!CanHandleInput(DHWorldEventPresentationStep.Cancel))
            return false;

        EndCurrentEvent(false);
        return true;
    }

    public bool ConfirmCurrentMessage()
    {
        if (currentRequest == null)
            return false;

        return currentRequest.Step switch
        {
            DHWorldEventPresentationStep.Accept => ConfirmAccept(),
            DHWorldEventPresentationStep.Cancel => ConfirmCancel(),
            _ => false
        };
    }

    public void CancelActiveEvent()
    {
        if (!isRunning)
            return;

        EndCurrentEvent(false);
    }

    private bool StartConsumeNpcEvent(
        WorldEventObject source,
        PartyGridMover party,
        DHWorldEventTemplate template,
        Action closedCallback)
    {
        isRunning = true;
        runningSource = source;
        runningParty = party;
        runningTemplate = template;
        runningClosedCallback = closedCallback;

        bool canProceed = DHWorldEventResultApplier.CanApply(template, party, out string disabledReason);
        currentRequest = BuildRequest(DHWorldEventPresentationStep.Description, template.Description, canProceed, disabledReason);

        if (logEvents)
        {
            Debug.Log(
                $"[DHWorldEventRuntime] Start consume NPC event '{template.WorldEventId}' zone={template.ZoneNo}, canProceed={canProceed}.",
                source);
        }

        PresentationChanged?.Invoke(currentRequest);
        return true;
    }

    private bool StartChoiceNpcEvent(
        WorldEventObject source,
        PartyGridMover party,
        DHWorldEventTemplate template,
        Action closedCallback)
    {
        isRunning = true;
        runningSource = source;
        runningParty = party;
        runningTemplate = template;
        runningClosedCallback = closedCallback;

        currentRequest = BuildChoiceRequest(string.Empty);

        if (logEvents)
        {
            Debug.Log(
                $"[DHWorldEventRuntime] Start choice NPC event '{template.WorldEventId}' zone={template.ZoneNo}, choices={template.Choices.Count}.",
                source);
        }

        PresentationChanged?.Invoke(currentRequest);
        return true;
    }

    private DHWorldEventPresentationRequest BuildRequest(
        DHWorldEventPresentationStep step,
        string messageText,
        bool canProceed,
        string disabledReason)
    {
        return new DHWorldEventPresentationRequest(runningTemplate, step, messageText, canProceed, disabledReason);
    }

    private DHWorldEventPresentationRequest BuildChoiceRequest(string disabledReason)
    {
        IReadOnlyList<DHWorldEventChoicePresentationOption> options =
            DHWorldEventChoiceResolver.BuildPresentationOptions(runningTemplate, runningParty, choiceCancelText);

        return new DHWorldEventPresentationRequest(
            runningTemplate,
            DHWorldEventPresentationStep.Description,
            runningTemplate != null ? runningTemplate.Description : string.Empty,
            true,
            disabledReason,
            options);
    }

    private bool TryFindCurrentChoice(string choiceId, out DHWorldEventChoicePresentationOption option)
    {
        option = null;
        if (currentRequest == null || string.IsNullOrWhiteSpace(choiceId))
            return false;

        IReadOnlyList<DHWorldEventChoicePresentationOption> options = currentRequest.ChoiceOptions;
        string normalized = choiceId.Trim();
        for (int i = 0; i < options.Count; i++)
        {
            DHWorldEventChoicePresentationOption candidate = options[i];
            if (candidate != null && string.Equals(candidate.ChoiceId, normalized, StringComparison.Ordinal))
            {
                option = candidate;
                return true;
            }
        }

        return false;
    }

    private bool CanHandleInput(DHWorldEventPresentationStep expectedStep)
    {
        return isRunning &&
               runningSource != null &&
               runningTemplate != null &&
               currentRequest != null &&
               currentRequest.Step == expectedStep;
    }

    private void EndCurrentEvent(bool completed)
    {
        DHWorldEventPresentationRequest closedRequest = currentRequest;
        Action callback = runningClosedCallback;

        isRunning = false;
        runningSource = null;
        runningParty = null;
        runningTemplate = null;
        runningClosedCallback = null;
        currentRequest = null;

        if (logEvents && closedRequest != null)
            Debug.Log($"[DHWorldEventRuntime] Close world event '{closedRequest.WorldEventId}', completed={completed}.", this);

        PresentationClosed?.Invoke(closedRequest);
        callback?.Invoke();
    }
}
