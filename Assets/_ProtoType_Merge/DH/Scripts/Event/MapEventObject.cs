using System;
using UnityEngine;

public class MapEventObject : MonoBehaviour
{
    public static event Action<MapEventObject, PartyGridMover> EventInteracted;

    [SerializeField] private MapEventType eventType = MapEventType.TrainingHp;
    [SerializeField] private ResourceType requireResource = ResourceType.Money;
    [SerializeField] private int requireAmount = 100;
    [SerializeField] private int effectAmount = 3;

    private MapEventRegistry eventRegistry;

    public MapEventType EventType => eventType;
    public string EventKey => MapEventTypeUtility.ToEventKey(eventType);
    public ResourceType RequireResource => requireResource;
    public int RequireAmount => requireAmount;
    public int EffectAmount => effectAmount;

    private void OnEnable()
    {
        ResolveRegistry();
        eventRegistry?.Register(this);
    }

    private void OnDisable()
    {
        eventRegistry?.Unregister(this);
    }

    public void ApplyInitialData(MapEventType nextEventType, int nextRequireAmount, int nextEffectAmount)
    {
        eventType = nextEventType;
        requireResource = ResourceType.Money;
        requireAmount = Mathf.Max(1, nextRequireAmount);
        effectAmount = Mathf.Max(0, nextEffectAmount);
    }

    public void Interact(PartyGridMover party)
    {
        // Map events still use the old confirmation panel flow instead of the chat/event-effect system.
        EventInteracted?.Invoke(this, party);
    }

    public bool TryExecuteEvent(PartyGridMover party)
    {
        if (Game.Economy == null || party == null)
            return false;

        if (!Game.Economy.Has(requireResource, requireAmount))
            return false;

        if (!CanApplyEventEffect(party))
            return false;

        if (!Game.Economy.Spend(requireResource, requireAmount))
            return false;

        if (!ApplyEventEffect(party))
            return false;

        // Map events are one-shot per cell and are restored by MapProgressRepository completion keys.
        MarkEventCompleted();
        Destroy(gameObject);
        return true;
    }

    private void MarkEventCompleted()
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return;

        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        repository.MarkEventCompleted(MapProgressKey.ForEvent(GetCurrentGrid(gridManager), EventKey));
    }

    public Vector2Int GetCurrentGrid(GridManager gridManager)
    {
        return gridManager != null
            ? gridManager.WorldToGrid(transform.position)
            : Vector2Int.zero;
    }

    public bool OccupiesGrid(Vector2Int grid, GridManager gridManager)
    {
        return GetCurrentGrid(gridManager) == grid;
    }

    private void ResolveRegistry()
    {
        if (eventRegistry == null)
            eventRegistry = FindFirstObjectByType<MapEventRegistry>();
    }

    private bool CanApplyEventEffect(PartyGridMover party)
    {
        if (!TryGetPartyUnitIndices(party, out int[] unitIndices))
            return false;

        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null)
            return false;

        for (int i = 0; i < unitIndices.Length; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex > 0 && repository.ContainsUnit(unitIndex))
                return true;
        }

        return false;
    }

    private bool ApplyEventEffect(PartyGridMover party)
    {
        if (!TryGetPartyUnitIndices(party, out int[] unitIndices))
            return false;

        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null)
            return false;

        bool appliedAny = false;
        for (int i = 0; i < unitIndices.Length; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex <= 0)
                continue;

            bool applied = eventType switch
            {
                MapEventType.TrainingHp => repository.AddEventBonusStats(unitIndex, Mathf.Max(0, effectAmount), 0f),
                MapEventType.TrainingAtk => repository.AddEventBonusStats(unitIndex, 0f, Mathf.Max(0, effectAmount)),
                MapEventType.Heal => repository.HealUnitToIngameMaxHp(unitIndex, out _),
                _ => false
            };

            appliedAny |= applied;
        }

        if (appliedAny)
            PartyRepositorySync.ApplyUnitsToScene(unitIndices);

        return appliedAny;
    }

    private static bool TryGetPartyUnitIndices(PartyGridMover party, out int[] unitIndices)
    {
        unitIndices = Array.Empty<int>();
        if (party == null)
            return false;

        PartyComposition composition = party.GetComponent<PartyComposition>();
        if (composition == null)
            return false;

        unitIndices = composition.UnitIndices;
        return unitIndices != null && unitIndices.Length > 0;
    }

}
