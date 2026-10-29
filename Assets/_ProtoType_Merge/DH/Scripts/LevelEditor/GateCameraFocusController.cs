using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GateCameraFocusController : MonoBehaviour
{
    [Header("Gate Camera")]
    [SerializeField] private QuarterViewCameraFollower cameraFollower;
    [SerializeField, Min(0f)] private float moveDuration = 0.35f;
    [SerializeField, Min(0f)] private float holdDuration = 0.5f;
    [SerializeField, Min(0f)] private float returnDuration = 0.35f;

    private sealed class Change
    {
        public string Key;
        public readonly List<GateRuntimeController> Gates = new List<GateRuntimeController>();
        public bool Revealed;

        public bool IsAnimating => Gates.Exists(gate => gate != null && gate.IsVisualAnimating);

        public void Reveal(bool animate = false)
        {
            if (Revealed) return;
            Revealed = true;
            for (int i = 0; i < Gates.Count; i++)
                if (Gates[i] != null) Gates[i].ApplyPendingVisualState(animate);
        }

        public void FinishImmediately()
        {
            Revealed = true;
            for (int i = 0; i < Gates.Count; i++)
                if (Gates[i] != null) Gates[i].ApplyPendingVisualState();
        }
    }

    public static GateCameraFocusController Instance { get; private set; }
    public static bool IsBusy => Instance != null && (Instance.current != null || Instance.pending.Count > 0);

    private readonly Queue<Change> pending = new Queue<Change>();
    private readonly Dictionary<string, Change> queuedByKey = new Dictionary<string, Change>();
    private Change current;
    private Coroutine presentation;
    private bool ownsCameraFocus;
    private PartyRegistry partyRegistry;
    private LevelZoneLayoutLoader layoutLoader;
    private GridManager gridManager;

    public static bool TryQueue(GateRuntimeController gate)
    {
        if (!Application.isPlaying || DHGameEndState.IsEnding || DHGameStateRestoreService.IsRestoring ||
            gate == null || !gate.gameObject.scene.isLoaded)
            return false;

        if (Instance == null)
        {
            Instance = FindFirstObjectByType<GateCameraFocusController>();
            if (Instance == null)
            {
                GameObject root = new GameObject("[DH_GateCameraFocus]");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gate.gameObject.scene);
                Instance = root.AddComponent<GateCameraFocusController>();
            }
        }

        if (!Instance.isActiveAndEnabled) return false;
        Instance.QueueChange(gate);
        return true;
    }

    public static IEnumerator WaitForIdle()
    {
        while (IsBusy && !DHGameEndState.IsEnding)
            yield return null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDisable()
    {
        CancelPresentation();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (DHGameEndState.IsEnding) { CancelPresentation(); return; }
        if (presentation == null && pending.Count > 0)
            presentation = StartCoroutine(PresentChanges());
    }

    private void QueueChange(GateRuntimeController gate)
    {
        string key = string.IsNullOrWhiteSpace(gate.GateId) ? gate.GetInstanceID().ToString() : gate.GateId;
        Change change;
        if (current != null && current.Key == key && !current.Revealed)
            change = current;
        else if (!queuedByKey.TryGetValue(key, out change))
        {
            change = new Change { Key = key };
            queuedByKey.Add(key, change);
            pending.Enqueue(change);
        }
        if (!change.Gates.Contains(gate)) change.Gates.Add(gate);
    }

    private IEnumerator PresentChanges()
    {
        // Both representations of one gate may change during the same call stack.
        yield return null;
        try
        {
            while (pending.Count > 0 && !DHGameEndState.IsEnding)
            {
                ResolveCamera();
                while (MustWaitForOtherPresentation() && !DHGameEndState.IsEnding)
                    yield return null;
                if (DHGameEndState.IsEnding) yield break;

                current = pending.Dequeue();
                queuedByKey.Remove(current.Key);
                GateRuntimeController focusGate = SelectFocusGate(current);
                if (cameraFollower != null && focusGate != null)
                {
                    ownsCameraFocus = true;
                    yield return cameraFollower.PlayTemporaryFocus(focusGate.GetFocusWorldPosition(),
                        moveDuration, holdDuration, returnDuration, () => current.Reveal(true),
                        () => !isActiveAndEnabled || DHGameEndState.IsEnding ||
                              SceneFadeController.HideCursorDuringTransition,
                        () => current != null && current.IsAnimating);
                    ownsCameraFocus = false;
                }
                current.FinishImmediately();
                current = null;
            }
        }
        finally
        {
            ReleaseCamera();
            FlushChanges();
            presentation = null;
        }
    }

    private bool MustWaitForOtherPresentation()
    {
        if (cameraFollower == null) return false;
        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        return SceneFadeController.HideCursorDuringTransition || ModalManager.HasAny ||
            ChatModalController.IsOpen || (ChatManager.Instance != null && ChatManager.Instance.IsRunning) ||
            (party != null && party.IsMoving) ||
            (cameraFollower != null && (cameraFollower.IsExternalInputLocked ||
                                       cameraFollower.IsEventFocusMoving || cameraFollower.IsTemporaryFocusActive));
    }

    private GateRuntimeController SelectFocusGate(Change change)
    {
        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        LevelZoneLayoutLoader layout = layoutLoader;
        GridManager grid = gridManager;
        string partyZone = party != null && grid != null ? ResolveZone(layout, party.GetCurrentGrid()) : string.Empty;
        Vector3 origin = party != null ? party.transform.position :
            cameraFollower != null ? cameraFollower.GetFocusWorldPosition() : Vector3.zero;
        GateRuntimeController selected = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < change.Gates.Count; i++)
        {
            GateRuntimeController gate = change.Gates[i];
            if (gate == null || !gate.isActiveAndEnabled) continue;
            Vector3 center = gate.GetFocusWorldPosition();
            if (!string.IsNullOrWhiteSpace(partyZone) &&
                ResolveZone(layout, grid.WorldToGrid(center)) != partyZone) continue;
            float distance = (center - origin).sqrMagnitude;
            if (distance >= bestDistance) continue;
            selected = gate;
            bestDistance = distance;
        }
        return selected;
    }

    private static string ResolveZone(LevelZoneLayoutLoader layout, Vector2Int cell)
    {
        if (layout == null) return string.Empty;
        foreach (LoadedLevelZoneData zone in layout.LoadedZones)
            if (new RectInt(zone.Anchor, zone.Size).Contains(cell))
                return zone.ZoneId;
        return string.Empty;
    }

    private void ResolveCamera()
    {
        if (partyRegistry == null) partyRegistry = FindFirstObjectByType<PartyRegistry>();
        if (layoutLoader == null) layoutLoader = FindFirstObjectByType<LevelZoneLayoutLoader>();
        if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();
        if (cameraFollower != null) return;
        Camera camera = Camera.main;
        cameraFollower = camera != null ? camera.GetComponent<QuarterViewCameraFollower>() : null;
        if (cameraFollower == null) cameraFollower = FindFirstObjectByType<QuarterViewCameraFollower>();
    }

    private void CancelPresentation()
    {
        ReleaseCamera();
        if (presentation != null) StopCoroutine(presentation);
        presentation = null;
        FlushChanges();
    }

    private void ReleaseCamera()
    {
        if (ownsCameraFocus && cameraFollower != null) cameraFollower.CancelTemporaryFocus();
        ownsCameraFocus = false;
    }

    private void FlushChanges()
    {
        current?.FinishImmediately();
        current = null;
        while (pending.Count > 0) pending.Dequeue().FinishImmediately();
        queuedByKey.Clear();
    }
}
