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

    private bool registeredThreat;
    private bool registeredTeleport;

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
        if (registeredThreat && GateThreatController.Instance != null)
            GateThreatController.Instance.UnregisterGate(this);

        if (registeredTeleport && GateTeleportController.Instance != null)
            GateTeleportController.Instance.UnregisterGate(this);

        registeredThreat = false;
        registeredTeleport = false;
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

    public void RefreshFromProgress()
    {
        ApplyProgressOrInitialState();
    }
    public bool ContainsZone(string zoneId)
    {
        string normalizedZoneId = NormalizeId(zoneId);
        return !string.IsNullOrWhiteSpace(normalizedZoneId) &&
            (string.Equals(FirstZoneId, normalizedZoneId, System.StringComparison.Ordinal) ||
             string.Equals(SecondZoneId, normalizedZoneId, System.StringComparison.Ordinal));
    }

    public void CollectTeleportCells(List<Vector2Int> results)
    {
        if (results == null)
            return;

        GridManager gridManager = ResolveGridManager();
        if (gridManager == null)
            return;

        for (int i = 0; i < blockerObjects.Count; i++)
        {
            GameObject blocker = blockerObjects[i];
            if (blocker == null)
                continue;

            Vector2Int cell = gridManager.WorldToGrid(blocker.transform.position);
            if (!results.Contains(cell))
                results.Add(cell);
        }

        results.Sort(CompareGridCells);
    }

    public bool TryGetTeleportCellIndex(Vector2Int grid, out int index)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        CollectTeleportCells(cells);

        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] != grid)
                continue;

            index = i;
            return true;
        }

        index = -1;
        return false;
    }

    public bool TryGetTeleportCellByIndex(int index, out Vector2Int cell)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        CollectTeleportCells(cells);
        if (cells.Count == 0)
        {
            cell = default;
            return false;
        }

        int clampedIndex = Mathf.Clamp(index, 0, cells.Count - 1);
        cell = cells[clampedIndex];
        return true;
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
        if (string.IsNullOrWhiteSpace(GateId))
            return;

        if (!registeredThreat)
        {
            GateThreatController controller = GateThreatController.EnsureSceneInstance();
            if (controller != null)
            {
                controller.RegisterGate(this);
                registeredThreat = true;
            }
        }

        if (!registeredTeleport)
        {
            GateTeleportController controller = GateTeleportController.EnsureSceneInstance();
            if (controller != null)
            {
                controller.RegisterGate(this);
                registeredTeleport = true;
            }
        }
    }

    private static int CompareGridCells(Vector2Int first, Vector2Int second)
    {
        int xCompare = first.x.CompareTo(second.x);
        return xCompare != 0 ? xCompare : first.y.CompareTo(second.y);
    }

    private static GridManager ResolveGridManager()
    {
        return Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
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
