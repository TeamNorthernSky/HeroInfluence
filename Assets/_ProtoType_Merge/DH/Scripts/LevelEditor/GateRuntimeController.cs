using System.Collections.Generic;
using UnityEngine;

public class GateRuntimeController : MonoBehaviour
{
    [SerializeField] private string gateId;
    [SerializeField] private string firstZoneId;
    [SerializeField] private string secondZoneId;
    [SerializeField, Min(1)] private int openDurationTurns = 3;
    [SerializeField] private List<GameObject> blockerObjects = new List<GameObject>();
    [SerializeField] private bool isOpen;

    private bool registered;

    public string GateId => NormalizeId(gateId);
    public string FirstZoneId => NormalizeId(firstZoneId);
    public string SecondZoneId => NormalizeId(secondZoneId);
    public int OpenDurationTurns => Mathf.Max(1, openDurationTurns);
    public bool IsOpen => isOpen;

    private void OnEnable()
    {
        TryRegister();
    }

    private void OnDisable()
    {
        if (!registered || GateThreatController.Instance == null)
            return;

        GateThreatController.Instance.UnregisterGate(this);
        registered = false;
    }

    public void Initialize(
        string nextGateId,
        string nextFirstZoneId,
        string nextSecondZoneId,
        IEnumerable<GameObject> nextBlockers,
        int nextOpenDurationTurns)
    {
        gateId = NormalizeId(nextGateId);
        firstZoneId = NormalizeId(nextFirstZoneId);
        secondZoneId = NormalizeId(nextSecondZoneId);
        openDurationTurns = Mathf.Max(1, nextOpenDurationTurns);

        blockerObjects.Clear();
        if (nextBlockers != null)
        {
            foreach (GameObject blocker in nextBlockers)
            {
                if (blocker != null && !blockerObjects.Contains(blocker))
                    blockerObjects.Add(blocker);
            }
        }

        ApplyProgressOrInitialState();
        TryRegister();
    }

    public bool ContainsZone(string zoneId)
    {
        string normalizedZoneId = NormalizeId(zoneId);
        return !string.IsNullOrWhiteSpace(normalizedZoneId) &&
            (string.Equals(FirstZoneId, normalizedZoneId, System.StringComparison.Ordinal) ||
             string.Equals(SecondZoneId, normalizedZoneId, System.StringComparison.Ordinal));
    }

    public void OpenGate(int day)
    {
        SetOpen(true, day, true);
    }

    public void CloseGate(int day)
    {
        SetOpen(false, day, true);
    }

    public void SetOpen(bool nextOpen, int day, bool saveProgress)
    {
        isOpen = nextOpen;
        for (int i = 0; i < blockerObjects.Count; i++)
        {
            GameObject blocker = blockerObjects[i];
            if (blocker != null)
                blocker.SetActive(!isOpen);
        }

        if (!saveProgress)
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        repository?.SetGateState(GateId, isOpen, Mathf.Max(1, day));
    }

    private void ApplyProgressOrInitialState()
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository != null && repository.TryGetGateState(GateId, out GateProgressState state) && state != null)
        {
            SetOpen(state.Open, state.OpenedDay, false);
            return;
        }

        SetOpen(false, ResolveCurrentDay(), false);
    }

    private void TryRegister()
    {
        if (registered || string.IsNullOrWhiteSpace(GateId))
            return;

        GateThreatController controller = GateThreatController.EnsureSceneInstance();
        if (controller == null)
            return;

        controller.RegisterGate(this);
        registered = true;
    }

    private static int ResolveCurrentDay()
    {
        TurnManager turnManager = FindFirstObjectByType<TurnManager>();
        if (turnManager != null)
            return turnManager.GetDay();

        return GameManager.Instance != null ? Mathf.Max(1, GameManager.Instance.CurrentDay) : 1;
    }

    private static string NormalizeId(string value)
    {
        return MapProgressKey.NormalizeSegment(value);
    }
}
