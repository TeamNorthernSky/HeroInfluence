using System.Collections.Generic;
using UnityEngine;

/// <summary>Shows exploration party notices for rejected moves and field item interactions.</summary>
[DisallowMultipleComponent]
public sealed class ExplorationPartyBubbleManager : MonoBehaviour
{
    public static ExplorationPartyBubbleManager Instance { get; private set; }

    private readonly List<ExplorationUnitBubble> candidates = new List<ExplorationUnitBubble>(4);
    private readonly List<ExplorationUnitBubble> visibleCandidates = new List<ExplorationUnitBubble>(4);
    private readonly ExplorationPartyPanelBubble panelBubble = new ExplorationPartyPanelBubble();
    private ExplorationActionPointBubble prefab;
    private ExplorationUnitBubble visibleBubble;
    private float nextRefresh;
    private bool missingPrefabReported;
    private bool showingInWorld;
    private float expiresAt;
    private string currentMessage;
    private int movementBalloonTemplateId;
    private readonly Queue<(PartyGridMover party, ResourceType type)> pendingResources =
        new Queue<(PartyGridMover party, ResourceType type)>();

    public int LastSpeakerUnitIndex { get; private set; }
    public bool IsShowingInPartyPanel => panelBubble.IsShowing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var host = new GameObject("ExplorationPartyBubbleManager");
        host.AddComponent<ExplorationPartyBubbleManager>();
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        if (Instance == this)
        {
            MoveCommandPreviewController.AnyDestinationUnreachable += OnDestinationUnreachable;
            PartyInteractionController.FieldResourceCollected += NotifyResourceInteraction;
        }
    }

    private void OnDisable()
    {
        MoveCommandPreviewController.AnyDestinationUnreachable -= OnDestinationUnreachable;
        PartyInteractionController.FieldResourceCollected -= NotifyResourceInteraction;
        ClearResourceNotices();
        HideCurrent();
    }

    private void OnDestroy()
    {
        panelBubble.Dispose();
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (visibleBubble == null)
        {
            panelBubble.Hide();
            return;
        }
        var unit = visibleBubble.GetComponent<PartyUnitState>();
        if (Time.unscaledTime >= expiresAt || unit == null || unit.IsIncapacitated ||
            !unit.gameObject.activeInHierarchy || unit.UnitIndex != LastSpeakerUnitIndex)
        {
            HideCurrent();
            return;
        }
        UpdatePresentation(false);
    }

    private void Update()
    {
        if (ClickSelectionController.Active == null)
        {
            HideCurrent();
            ClearResourceNotices();
            return;
        }
        if (visibleBubble == null && pendingResources.Count > 0)
        {
            var notice = pendingResources.Dequeue();
            ShowForParty(notice.party, null, notice.type);
        }
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .5f;
        foreach (var party in FindObjectsByType<PartyGridMover>(FindObjectsSortMode.None))
            PrepareParty(party, false);
    }

    private bool LoadPrefab()
    {
        if (prefab != null) return true;
        prefab = Resources.Load<ExplorationActionPointBubble>("ExplorationActionPointBubble");
        if (prefab == null && !missingPrefabReported)
        {
            missingPrefabReported = true;
            Debug.LogWarning("[PartyBubble] Missing Resources/ExplorationActionPointBubble prefab.", this);
        }
        return prefab != null;
    }

    private void PrepareParty(PartyGridMover party, bool collectCandidates)
    {
        if (party == null || !LoadPrefab()) return;
        foreach (var unit in party.GetComponentsInChildren<PartyUnitState>(true))
        {
            var view = unit.GetComponent<ExplorationUnitBubble>();
            if (view == null) view = unit.gameObject.AddComponent<ExplorationUnitBubble>();
            if (!view.Prepare(prefab)) continue;
            if (collectCandidates && unit.UnitIndex > 0 && unit.gameObject.activeInHierarchy && !unit.IsIncapacitated)
                candidates.Add(view);
        }
    }

    private void OnDestinationUnreachable(PartyGridMover party)
    {
        if (ShowForParty(party)) AudioManager.Instance?.PlaySfx("Button_Click_Fail");
    }

    public void NotifyResourceInteraction(PartyGridMover party, ResourceType type)
    {
        if (!isActiveAndEnabled || ClickSelectionController.Active == null ||
            party == null || !party.gameObject.activeInHierarchy) return;
        pendingResources.Enqueue((party, type));
    }

    private void ClearResourceNotices()
    {
        pendingResources.Clear();
    }

    public static string ResourceMessage(ResourceType type)
    {
        var source = Resources.Load<ExplorationActionPointBubble>("ExplorationActionPointBubble");
        return source != null ? source.GetResourceMessage(type) : "재화를 얻었어!";
    }

    public bool ShowForParty(PartyGridMover party, string message = null)
        => ShowForParty(party, message, null);

    private bool ShowForParty(PartyGridMover party, string message, ResourceType? resourceType)
    {
        HideCurrent();
        currentMessage = message;
        candidates.Clear();
        visibleCandidates.Clear();
        if (party == null || !party.gameObject.activeInHierarchy ||
            DefeatedPartyReturnController.IsPartyWaiting(party) || Camera.main == null) return false;
        PrepareParty(party, true);
        if (candidates.Count == 0) return false;
        foreach (var candidate in candidates)
            if (candidate.IsVisibleTo(Camera.main)) visibleCandidates.Add(candidate);
        var selection = visibleCandidates.Count > 0 ? visibleCandidates : candidates;
        visibleBubble = selection[Random.Range(0, selection.Count)];
        LastSpeakerUnitIndex = visibleBubble.UnitIndex;
        var speaker = visibleBubble.GetComponent<PartyUnitState>();
        movementBalloonTemplateId = ExplorationHeroDialogue.GetTemplateId(speaker);
        if (string.IsNullOrWhiteSpace(currentMessage))
        {
            currentMessage = resourceType.HasValue
                ? ExplorationHeroDialogue.GetResourceMessage(speaker, resourceType.Value)
                : ExplorationHeroDialogue.GetMovementMessage(speaker);
            if (string.IsNullOrWhiteSpace(currentMessage))
                currentMessage = resourceType.HasValue ? ResourceMessage(resourceType.Value) : visibleBubble.Bubble.movementMessage;
        }
        candidates.Clear();
        visibleCandidates.Clear();
        bool shown = UpdatePresentation(true);
        expiresAt = Time.unscaledTime + (showingInWorld ? visibleBubble.Bubble.TotalDuration : panelBubble.TotalDuration);
        if (!shown) HideCurrent();
        return shown;
    }

    private bool UpdatePresentation(bool force)
    {
        var camera = Camera.main;
        bool inWorld = visibleBubble.IsVisibleTo(camera);
        if (!force && inWorld == showingInWorld)
        {
            if (inWorld) return visibleBubble.Bubble != null && visibleBubble.Bubble.gameObject.activeSelf;
            if (panelBubble.HasBinding(LastSpeakerUnitIndex) && panelBubble.IsShowing) return true;
        }
        visibleBubble.Hide();
        panelBubble.Hide();
        showingInWorld = inWorld;
        return inWorld ? visibleBubble.Show(camera, currentMessage, movementBalloonTemplateId)
            : LoadPrefab() && panelBubble.ShowForUnit(LastSpeakerUnitIndex, prefab, currentMessage);
    }

    private void HideCurrent()
    {
        if (visibleBubble != null) visibleBubble.Hide();
        panelBubble.Hide();
        visibleBubble = null;
        showingInWorld = false;
    }
}
