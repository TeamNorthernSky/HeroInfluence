using System.Collections.Generic;
using UnityEngine;

public class GateLifecycleController : MonoBehaviour
{
    [SerializeField, Min(1)] private int openDurationTurns = 3;
    [SerializeField] private TurnManager turnManager;

    private readonly List<GateRuntimeController> gates = new List<GateRuntimeController>();

    public static GateLifecycleController Instance { get; private set; }
    public int OpenDurationTurns => Mathf.Max(1, openDurationTurns);

    private void Awake()
    {
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        ResolveReferences();
        SubscribeTurnManager();
        RegisterSceneGates();
        EvaluateOpenGates(ResolveCurrentDay());
    }

    private void OnDisable()
    {
        if (turnManager != null)
            turnManager.DayAdvanced -= HandleDayAdvanced;

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (turnManager == null)
        {
            ResolveReferences();
            SubscribeTurnManager();
        }
    }

    public static GateLifecycleController EnsureSceneInstance()
    {
        if (Instance != null)
            return Instance;

        GateLifecycleController existing = FindFirstObjectByType<GateLifecycleController>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        if (!Application.isPlaying)
            return null;

        GameObject created = new GameObject(nameof(GateLifecycleController));
        Instance = created.AddComponent<GateLifecycleController>();
        return Instance;
    }

    public void RegisterGate(GateRuntimeController gate)
    {
        if (gate == null || gates.Contains(gate))
            return;

        gates.Add(gate);
        EvaluateGate(gate, ResolveCurrentDay());
    }

    public void UnregisterGate(GateRuntimeController gate)
    {
        if (gate == null)
            return;

        gates.Remove(gate);
    }

    [ContextMenu("Evaluate Open Gates")]
    public void EvaluateOpenGates()
    {
        EvaluateOpenGates(ResolveCurrentDay());
    }

    private void HandleDayAdvanced(int day)
    {
        EvaluateOpenGates(day);
    }

    private void EvaluateOpenGates(int day)
    {
        for (int i = gates.Count - 1; i >= 0; i--)
        {
            GateRuntimeController gate = gates[i];
            if (gate == null)
            {
                gates.RemoveAt(i);
                continue;
            }

            EvaluateGate(gate, day);
        }
    }

    private void EvaluateGate(GateRuntimeController gate, int day)
    {
        if (gate == null || !gate.IsOpen)
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null || !repository.TryGetGateState(gate.GateId, out GateProgressState state) || state == null || !state.Open)
            return;

        int elapsedTurns = Mathf.Max(0, day - state.OpenedDay);
        if (elapsedTurns < OpenDurationTurns)
            return;

        gate.CloseGate(day);
    }

    private void RegisterSceneGates()
    {
        GateRuntimeController[] sceneGates = FindObjectsByType<GateRuntimeController>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneGates.Length; i++)
            RegisterGate(sceneGates[i]);
    }

    private void ResolveReferences()
    {
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();
    }

    private void SubscribeTurnManager()
    {
        if (turnManager == null)
            return;

        turnManager.DayAdvanced -= HandleDayAdvanced;
        turnManager.DayAdvanced += HandleDayAdvanced;
    }

    private int ResolveCurrentDay()
    {
        if (turnManager != null)
            return turnManager.GetDay();

        return GameManager.Instance != null ? Mathf.Max(1, GameManager.Instance.CurrentDay) : 1;
    }
}
