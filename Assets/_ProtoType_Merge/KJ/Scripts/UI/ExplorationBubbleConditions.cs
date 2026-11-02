using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Observes existing exploration state without changing movement, gates, or saved progress.</summary>
public sealed class ExplorationBubbleConditions
{
    private sealed class PartyState
    {
        public Vector3 Position;
        public float IdleSince;
        public bool IdleQueued;
        public bool IdleShown;
        public bool ArrivalPending;
        public bool HasTravelled;
        public readonly HashSet<string> Visits = new HashSet<string>();
    }

    private static readonly FieldInfo SelectionField = typeof(ClickSelectionController)
        .GetField("partySelectionController", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly Dictionary<string, bool> gates = new Dictionary<string, bool>();
    private readonly Dictionary<string, bool> gateSnapshot = new Dictionary<string, bool>();
    private readonly Dictionary<PartyGridMover, PartyState> parties = new Dictionary<PartyGridMover, PartyState>();
    private readonly HashSet<string> visits = new HashSet<string>();
    private readonly List<string> staleGates = new List<string>();
    private readonly List<PartyGridMover> staleParties = new List<PartyGridMover>();
    private int sceneHandle;
    private float nextPoll;

    public void Reset()
    {
        gates.Clear();
        gateSnapshot.Clear();
        parties.Clear();
        nextPoll = 0f;
        sceneHandle = 0;
    }

    public void Tick(Action<PartyGridMover, ExplorationHeroDialogue.Situation> enqueue)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (sceneHandle != scene.handle) { Reset(); sceneHandle = scene.handle; }
        if (ClickSelectionController.Active == null || ClickSelectionController.Active.gameObject.scene != scene ||
            Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + .25f;

        var selection = SelectionField?.GetValue(ClickSelectionController.Active) as PartySelectionController;
        PartyGridMover selected = selection != null ? selection.ActiveMover : null;
        bool restoring = DHGameStateRestoreService.IsRestoring;
        bool blocked = IsBlocked();
        gateSnapshot.Clear();
        foreach (var gate in UnityEngine.Object.FindObjectsByType<GateRuntimeController>(FindObjectsSortMode.None))
        {
            if (gate.gameObject.scene != scene) continue;
            string key = string.IsNullOrEmpty(gate.GateId) ? gate.GetInstanceID().ToString() : gate.GateId;
            gateSnapshot.TryGetValue(key, out bool anyOpen);
            gateSnapshot[key] = anyOpen || gate.IsOpen;
        }
        foreach (var gate in gateSnapshot)
        {
            if (ObserveGate(gate.Key, gate.Value, restoring) && selected != null)
                enqueue(selected, ExplorationHeroDialogue.Situation.GateOpened);
        }

        foreach (var party in UnityEngine.Object.FindObjectsByType<PartyGridMover>(FindObjectsSortMode.None))
        {
            if (party.gameObject.scene != scene) continue;
            CollectVisits(party, visits);
            if (!parties.TryGetValue(party, out var state))
            {
                state = new PartyState { Position = party.transform.position, IdleSince = Time.unscaledTime };
                state.Visits.UnionWith(visits);
                parties.Add(party, state);
                continue; // Initial load/restore is not an arrival.
            }

            bool moved = party.IsMoving || (party.transform.position - state.Position).sqrMagnitude > .0001f;
            if (moved)
            {
                state.Position = party.transform.position;
                state.IdleSince = Time.unscaledTime;
                state.IdleQueued = false;
                state.IdleShown = false;
                if (!restoring) state.HasTravelled = true;
            }
            if (restoring || blocked || party != selected) state.IdleSince = Time.unscaledTime;

            bool arrived = false;
            foreach (string source in visits)
                if (!state.Visits.Contains(source)) { arrived = true; break; }
            state.Visits.Clear();
            state.Visits.UnionWith(visits);
            if (restoring) { state.ArrivalPending = false; state.HasTravelled = false; continue; }
            if (arrived && state.HasTravelled) state.ArrivalPending = true;
            if (visits.Count == 0) state.ArrivalPending = false;
            if (state.ArrivalPending && !party.IsMoving && !DefeatedPartyReturnController.IsPartyWaiting(party))
            {
                enqueue(party, ExplorationHeroDialogue.Situation.BaseArrival);
                state.ArrivalPending = false;
                state.HasTravelled = false;
            }

            if (!blocked && party == selected && !moved && !state.IdleQueued && !state.IdleShown &&
                Time.unscaledTime - state.IdleSince >= 5f && !DefeatedPartyReturnController.IsPartyWaiting(party))
            {
                state.IdleQueued = true;
                enqueue(party, ExplorationHeroDialogue.Situation.Idle);
            }
        }
        RemoveDestroyedObjects();
    }

    private bool ObserveGate(string key, bool isOpen, bool restoring)
    {
        bool opened = gates.TryGetValue(key, out bool wasOpen) && !wasOpen && isOpen;
        gates[key] = isOpen;
        return opened && !restoring;
    }

    public bool IsCurrent(PartyGridMover party, ExplorationHeroDialogue.Situation situation)
    {
        if (party == null || !party.isActiveAndEnabled || party.gameObject.scene != SceneManager.GetActiveScene()) return false;
        if (situation == ExplorationHeroDialogue.Situation.Idle)
        {
            var selection = SelectionField?.GetValue(ClickSelectionController.Active) as PartySelectionController;
            return selection?.ActiveMover == party && !party.IsMoving &&
                parties.TryGetValue(party, out var state) && state.IdleQueued &&
                Time.unscaledTime - state.IdleSince >= 5f &&
                (party.transform.position - state.Position).sqrMagnitude <= .0001f;
        }
        if (situation == ExplorationHeroDialogue.Situation.BaseArrival)
        {
            CollectVisits(party, visits);
            return visits.Count > 0 && !party.IsMoving;
        }
        return true;
    }

    public void NoticeFinished(PartyGridMover party, ExplorationHeroDialogue.Situation situation, bool shown)
    {
        if (situation != ExplorationHeroDialogue.Situation.Idle || party == null ||
            !parties.TryGetValue(party, out var state)) return;
        state.IdleQueued = false;
        if (shown) state.IdleShown = true;
    }

    private static void CollectVisits(PartyGridMover party, HashSet<string> result)
    {
        result.Clear();
        var identity = party.GetComponent<PartyIdentity>();
        var state = HQVisitState.Instance;
        if (identity == null || state == null) return;
        foreach (var source in state.VisitingBySource)
            if (source.Value.Contains(identity.PartyId)) result.Add(source.Key);
    }

    public static bool IsBlocked()
    {
        if (ClickSelectionController.Active == null ||
            ClickSelectionController.Active.gameObject.scene != SceneManager.GetActiveScene() ||
            DHGameEndState.IsEnding || DHGameStateRestoreService.IsRestoring ||
            ModalManager.HasAny || ChatModalController.IsOpen || WorldInputGate.IsTurnResolving ||
            GateCameraFocusController.IsBusy || JC.Environment.JcDayNightController.IsTransitioning ||
            (ChatManager.Instance != null && ChatManager.Instance.IsRunning) ||
            (DHWorldEventRuntimeManager.Instance != null && DHWorldEventRuntimeManager.Instance.IsRunning) ||
            (DHWorldEventConditionRuntimeManager.Instance != null && DHWorldEventConditionRuntimeManager.Instance.IsCheckPending) ||
            (SceneFadeController.Instance != null && SceneFadeController.Instance.IsTransitioning)) return true;
        var turn = UnityEngine.Object.FindFirstObjectByType<TurnManager>();
        var combat = UnityEngine.Object.FindFirstObjectByType<CombatPromptService>();
        return Time.timeScale <= 0f || (turn != null && !turn.IsPlayerTurn) || (combat != null && combat.IsOpen);
    }

    private void RemoveDestroyedObjects()
    {
        staleGates.Clear();
        foreach (var gate in gates.Keys) if (!gateSnapshot.ContainsKey(gate)) staleGates.Add(gate);
        foreach (var gate in staleGates) gates.Remove(gate);
        staleParties.Clear();
        foreach (var party in parties.Keys) if (party == null) staleParties.Add(party);
        foreach (var party in staleParties) parties.Remove(party);
    }
}
