using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TutorialUnitProgressState
{
    [SerializeField] private string unitTemplateKey;
    [SerializeField] private bool joined;
    [SerializeField] private int currentHp;
    [SerializeField] private int maxHp;
    [SerializeField] private int currentIp;
    [SerializeField] private int maxIp;
    [SerializeField] private int atk;

    public string UnitTemplateKey => NormalizeKey(unitTemplateKey);
    public bool Joined => joined;
    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public int CurrentIp => currentIp;
    public int MaxIp => maxIp;
    public int Atk => atk;

    public TutorialUnitProgressState()
    {
    }

    public TutorialUnitProgressState(string unitTemplateKey, bool joined)
    {
        this.unitTemplateKey = NormalizeKey(unitTemplateKey);
        this.joined = joined;
    }

    public void SetJoined(bool value)
    {
        joined = value;
    }

    public void SetStats(int nextCurrentHp, int nextMaxHp, int nextCurrentIp, int nextMaxIp, int nextAtk)
    {
        currentHp = nextCurrentHp;
        maxHp = nextMaxHp;
        currentIp = nextCurrentIp;
        maxIp = nextMaxIp;
        atk = nextAtk;
    }

    public void Normalize()
    {
        unitTemplateKey = NormalizeKey(unitTemplateKey);
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }
}

[Serializable]
public class TutorialPartyProgressState
{
    [SerializeField] private Vector2Int currentGrid;
    [SerializeField] private bool hasCurrentGrid;
    [SerializeField] private int remainingMovePoints;
    [SerializeField] private bool hasRemainingMovePoints;

    public Vector2Int CurrentGrid => currentGrid;
    public bool HasCurrentGrid => hasCurrentGrid;
    public int RemainingMovePoints => remainingMovePoints;
    public bool HasRemainingMovePoints => hasRemainingMovePoints;

    public void SetCurrentGrid(Vector2Int grid)
    {
        currentGrid = grid;
        hasCurrentGrid = true;
    }

    public void SetRemainingMovePoints(int value)
    {
        remainingMovePoints = Mathf.Max(0, value);
        hasRemainingMovePoints = true;
    }

    public void Clear()
    {
        currentGrid = Vector2Int.zero;
        hasCurrentGrid = false;
        remainingMovePoints = 0;
        hasRemainingMovePoints = false;
    }
}

public enum TutorialOutpostClaimState
{
    EnemyClaimed = 0,
    HeroClaimed = 1
}

public enum TutorialCombatSourceType
{
    None = 0,
    Outpost = 1
}

[Serializable]
public class TutorialOutpostProgressState
{
    [SerializeField] private string objectKey;
    [SerializeField] private TutorialOutpostClaimState claimState;

    public string ObjectKey => NormalizeKey(objectKey);
    public TutorialOutpostClaimState ClaimState => claimState;

    public TutorialOutpostProgressState()
    {
    }

    public TutorialOutpostProgressState(string objectKey, TutorialOutpostClaimState claimState)
    {
        this.objectKey = NormalizeKey(objectKey);
        this.claimState = claimState;
    }

    public void SetClaimState(TutorialOutpostClaimState nextClaimState)
    {
        claimState = nextClaimState;
    }

    public void Normalize()
    {
        objectKey = NormalizeKey(objectKey);
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }
}

[DisallowMultipleComponent]
public sealed class TutorialProgressRepository : MonoBehaviour
{
    private const string RootName = "[DH_TutorialProgressRepository]";

    public static TutorialProgressRepository Instance { get; private set; }

    [Header("Tutorial Progress")]
    [SerializeField] private bool tutorialCompleted;
    [SerializeField] private CombatResult lastCombatResult = CombatResult.None;
    [SerializeField] private int currentTurn = 1;
    [SerializeField] private int currentOrder = 1;
    [SerializeField] private TutorialPartyProgressState partyState = new TutorialPartyProgressState();
    [SerializeField] private int money;
    [SerializeField] private int chip;
    [SerializeField] private int crystal;
    [SerializeField] private int supply;
    [SerializeField] private List<TutorialUnitProgressState> unitStates = new List<TutorialUnitProgressState>();
    [SerializeField] private List<string> collectedItemKeys = new List<string>();
    [SerializeField] private List<string> completedCellEventKeys = new List<string>();
    [SerializeField] private List<string> seenMessageKeys = new List<string>();
    [SerializeField] private List<string> inactiveObjectKeys = new List<string>();
    [SerializeField] private List<TutorialOutpostProgressState> outpostStates = new List<TutorialOutpostProgressState>();
    [SerializeField] private TutorialCombatSourceType pendingCombatSourceType = TutorialCombatSourceType.None;
    [SerializeField] private string pendingCombatSourceKey;

    private readonly Dictionary<string, TutorialUnitProgressState> unitLookup =
        new Dictionary<string, TutorialUnitProgressState>(StringComparer.Ordinal);
    private readonly Dictionary<string, TutorialOutpostProgressState> outpostLookup =
        new Dictionary<string, TutorialOutpostProgressState>(StringComparer.Ordinal);
    private readonly HashSet<string> collectedItemLookup =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> completedCellEventLookup =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> seenMessageLookup =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> inactiveObjectLookup =
        new HashSet<string>(StringComparer.Ordinal);

    public bool TutorialCompleted => tutorialCompleted;
    public CombatResult LastCombatResult => lastCombatResult;
    public int CurrentTurn => Mathf.Max(1, currentTurn);
    public int CurrentOrder => currentOrder;
    public TutorialPartyProgressState PartyState => partyState;
    public IReadOnlyList<TutorialUnitProgressState> UnitStates => unitStates;
    public IReadOnlyList<string> CollectedItemKeys => collectedItemKeys;
    public IReadOnlyList<string> CompletedCellEventKeys => completedCellEventKeys;
    public IReadOnlyList<string> SeenMessageKeys => seenMessageKeys;
    public IReadOnlyList<string> InactiveObjectKeys => inactiveObjectKeys;
    public IReadOnlyList<TutorialOutpostProgressState> OutpostStates => outpostStates;
    public TutorialCombatSourceType PendingCombatSourceType => pendingCombatSourceType;
    public string PendingCombatSourceKey => NormalizeKey(pendingCombatSourceKey);
    public bool HasAnyJoinedUnit => GetJoinedUnitCount() > 0;

    public event Action ProgressChanged;
    public event Action<ResourceType, int> TutorialResourceChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static TutorialProgressRepository EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject root = new GameObject(RootName);
        DontDestroyOnLoad(root);
        return root.AddComponent<TutorialProgressRepository>();
    }

    public static void ClearProgress()
    {
        EnsureInstance().ResetProgress();
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
        RebuildLookups();
    }

    private void OnValidate()
    {
        currentTurn = Mathf.Max(1, currentTurn);
        currentOrder = Mathf.Max(1, currentOrder);
        RebuildLookups();
    }

    public void ResetProgress()
    {
        tutorialCompleted = false;
        lastCombatResult = CombatResult.None;
        currentTurn = 1;
        currentOrder = 1;
        partyState.Clear();
        ResetResources(notify: false);
        unitStates.Clear();
        collectedItemKeys.Clear();
        completedCellEventKeys.Clear();
        seenMessageKeys.Clear();
        inactiveObjectKeys.Clear();
        outpostStates.Clear();
        pendingCombatSourceType = TutorialCombatSourceType.None;
        pendingCombatSourceKey = string.Empty;
        RebuildLookups();
        NotifyChanged();
    }

    [ContextMenu("Clear Tutorial Progress")]
    public void ClearTutorialProgress()
    {
        ResetProgress();
    }

    public void MarkTutorialCompleted()
    {
        if (tutorialCompleted)
            return;

        tutorialCompleted = true;
        NotifyChanged();
    }

    public void SetLastCombatResult(CombatResult result)
    {
        if (lastCombatResult == result)
            return;

        lastCombatResult = result;
        NotifyChanged();
    }

    public void SetCurrentOrder(int order)
    {
        int normalized = Mathf.Max(1, order);
        if (currentOrder == normalized)
            return;

        currentOrder = normalized;
        NotifyChanged();
    }

    public void SetPartyGrid(Vector2Int grid)
    {
        partyState.SetCurrentGrid(grid);
        NotifyChanged();
    }

    public void SetRemainingMovePoints(int value)
    {
        partyState.SetRemainingMovePoints(value);
        NotifyChanged();
    }

    public void SetCurrentTurn(int turn)
    {
        int normalized = Mathf.Max(1, turn);
        if (currentTurn == normalized)
            return;

        currentTurn = normalized;
        NotifyChanged();
    }

    public int AdvanceTurn()
    {
        currentTurn = Mathf.Max(1, currentTurn + 1);
        NotifyChanged();
        return currentTurn;
    }

    public void ResetTurn()
    {
        SetCurrentTurn(1);
    }

    public int GetResource(ResourceType type)
    {
        return type switch
        {
            ResourceType.Money => money,
            ResourceType.Chip => chip,
            ResourceType.Crystal => crystal,
            ResourceType.Supply => supply,
            _ => 0
        };
    }

    public void SetResource(ResourceType type, int value)
    {
        int normalizedValue = Mathf.Max(0, value);
        if (GetResource(type) == normalizedValue)
            return;

        switch (type)
        {
            case ResourceType.Money:
                money = normalizedValue;
                break;
            case ResourceType.Chip:
                // ResourceType.Chip is displayed as Medal in tutorial/event UI.
                chip = normalizedValue;
                break;
            case ResourceType.Crystal:
                crystal = normalizedValue;
                break;
            case ResourceType.Supply:
                supply = normalizedValue;
                break;
            default:
                return;
        }

        NotifyResourceChanged(type, normalizedValue);
    }

    public void AddResource(ResourceType type, int amount)
    {
        if (amount == 0)
            return;

        SetResource(type, GetResource(type) + amount);
    }

    public bool HasResource(ResourceType type, int amount)
    {
        return amount <= 0 || GetResource(type) >= amount;
    }

    public bool SpendResource(ResourceType type, int amount)
    {
        if (amount <= 0 || !HasResource(type, amount))
            return false;

        SetResource(type, GetResource(type) - amount);
        return true;
    }

    public void ResetResources()
    {
        ResetResources(notify: true);
    }

    public TutorialUnitProgressState GetOrCreateUnitState(string unitTemplateKey)
    {
        string normalized = NormalizeKey(unitTemplateKey);
        if (string.IsNullOrEmpty(normalized))
            return null;

        if (unitLookup.TryGetValue(normalized, out TutorialUnitProgressState state) && state != null)
            return state;

        state = new TutorialUnitProgressState(normalized, false);
        unitStates.Add(state);
        unitLookup[normalized] = state;
        return state;
    }

    public bool TryGetUnitState(string unitTemplateKey, out TutorialUnitProgressState state)
    {
        return unitLookup.TryGetValue(NormalizeKey(unitTemplateKey), out state) && state != null;
    }

    public void SetUnitJoined(string unitTemplateKey, bool joined)
    {
        TutorialUnitProgressState state = GetOrCreateUnitState(unitTemplateKey);
        if (state == null || state.Joined == joined)
            return;

        state.SetJoined(joined);
        NotifyChanged();
    }

    public void SetUnitStats(
        string unitTemplateKey,
        int currentHp,
        int maxHp,
        int currentIp,
        int maxIp,
        int atk)
    {
        TutorialUnitProgressState state = GetOrCreateUnitState(unitTemplateKey);
        if (state == null)
            return;

        state.SetStats(
            Mathf.Max(0, currentHp),
            Mathf.Max(0, maxHp),
            Mathf.Max(0, currentIp),
            Mathf.Max(0, maxIp),
            Mathf.Max(0, atk));
        NotifyChanged();
    }

    public bool IsUnitJoined(string unitTemplateKey)
    {
        return TryGetUnitState(unitTemplateKey, out TutorialUnitProgressState state) && state.Joined;
    }

    public List<string> GetJoinedUnitTemplateKeys()
    {
        List<string> result = new List<string>();
        for (int i = 0; i < unitStates.Count; i++)
        {
            TutorialUnitProgressState state = unitStates[i];
            if (state == null || !state.Joined || string.IsNullOrWhiteSpace(state.UnitTemplateKey))
                continue;

            result.Add(state.UnitTemplateKey);
        }

        return result;
    }

    public void MarkCellEventCompleted(string eventKey)
    {
        AddUniqueKey(eventKey, completedCellEventKeys, completedCellEventLookup);
    }

    public void MarkItemCollected(string itemKey)
    {
        AddUniqueKey(itemKey, collectedItemKeys, collectedItemLookup);
    }

    public bool IsItemCollected(string itemKey)
    {
        return collectedItemLookup.Contains(NormalizeKey(itemKey));
    }

    public bool IsCellEventCompleted(string eventKey)
    {
        return completedCellEventLookup.Contains(NormalizeKey(eventKey));
    }

    public void MarkMessageSeen(string messageKey)
    {
        AddUniqueKey(messageKey, seenMessageKeys, seenMessageLookup);
    }

    public bool IsMessageSeen(string messageKey)
    {
        return seenMessageLookup.Contains(NormalizeKey(messageKey));
    }

    public void MarkObjectInactive(string objectKey)
    {
        AddUniqueKey(objectKey, inactiveObjectKeys, inactiveObjectLookup);
    }

    public bool IsObjectInactive(string objectKey)
    {
        return inactiveObjectLookup.Contains(NormalizeKey(objectKey));
    }

    public void SetOutpostState(string objectKey, TutorialOutpostClaimState claimState)
    {
        string normalized = NormalizeKey(objectKey);
        if (string.IsNullOrEmpty(normalized))
            return;

        bool created = false;
        if (!outpostLookup.TryGetValue(normalized, out TutorialOutpostProgressState state) || state == null)
        {
            state = new TutorialOutpostProgressState(normalized, claimState);
            outpostStates.Add(state);
            outpostLookup[normalized] = state;
            created = true;
        }

        if (state.ClaimState == claimState)
        {
            if (created)
                NotifyChanged();

            return;
        }

        state.SetClaimState(claimState);
        NotifyChanged();
    }

    public bool TryGetOutpostState(string objectKey, out TutorialOutpostClaimState claimState)
    {
        claimState = TutorialOutpostClaimState.EnemyClaimed;
        if (!outpostLookup.TryGetValue(NormalizeKey(objectKey), out TutorialOutpostProgressState state) || state == null)
            return false;

        claimState = state.ClaimState;
        return true;
    }

    public void SetPendingCombatSource(TutorialCombatSourceType sourceType, string sourceKey)
    {
        string normalizedKey = NormalizeKey(sourceKey);
        if (sourceType == TutorialCombatSourceType.None || string.IsNullOrEmpty(normalizedKey))
        {
            ClearPendingCombatSource();
            return;
        }

        if (pendingCombatSourceType == sourceType && PendingCombatSourceKey == normalizedKey)
            return;

        pendingCombatSourceType = sourceType;
        pendingCombatSourceKey = normalizedKey;
        NotifyChanged();
    }

    public void ClearPendingCombatSource()
    {
        bool changed = pendingCombatSourceType != TutorialCombatSourceType.None ||
            !string.IsNullOrWhiteSpace(pendingCombatSourceKey);

        pendingCombatSourceType = TutorialCombatSourceType.None;
        pendingCombatSourceKey = string.Empty;

        if (changed)
            NotifyChanged();
    }

    public void ApplyPendingCombatResult(CombatResult result)
    {
        TutorialCombatSourceType sourceType = pendingCombatSourceType;
        string sourceKey = PendingCombatSourceKey;
        ClearPendingCombatSource();

        if (sourceType != TutorialCombatSourceType.Outpost || string.IsNullOrEmpty(sourceKey))
            return;

        if (result == CombatResult.Victory)
            SetOutpostState(sourceKey, TutorialOutpostClaimState.HeroClaimed);
    }

    [ContextMenu("Rebuild Lookup")]
    public void RebuildLookups()
    {
        unitLookup.Clear();
        outpostLookup.Clear();
        RebuildKeyLookup(collectedItemKeys, collectedItemLookup);
        RebuildKeyLookup(completedCellEventKeys, completedCellEventLookup);
        RebuildKeyLookup(seenMessageKeys, seenMessageLookup);
        RebuildKeyLookup(inactiveObjectKeys, inactiveObjectLookup);
        pendingCombatSourceKey = NormalizeKey(pendingCombatSourceKey);

        for (int i = unitStates.Count - 1; i >= 0; i--)
        {
            TutorialUnitProgressState state = unitStates[i];
            if (state == null || string.IsNullOrWhiteSpace(state.UnitTemplateKey))
            {
                unitStates.RemoveAt(i);
                continue;
            }

            state.Normalize();
            unitLookup[state.UnitTemplateKey] = state;
        }

        for (int i = outpostStates.Count - 1; i >= 0; i--)
        {
            TutorialOutpostProgressState state = outpostStates[i];
            if (state == null || string.IsNullOrWhiteSpace(state.ObjectKey))
            {
                outpostStates.RemoveAt(i);
                continue;
            }

            state.Normalize();
            outpostLookup[state.ObjectKey] = state;
        }
    }

    private int GetJoinedUnitCount()
    {
        int count = 0;
        for (int i = 0; i < unitStates.Count; i++)
        {
            if (unitStates[i] != null && unitStates[i].Joined)
                count++;
        }

        return count;
    }

    private void AddUniqueKey(string key, List<string> list, HashSet<string> lookup)
    {
        string normalized = NormalizeKey(key);
        if (string.IsNullOrEmpty(normalized) || lookup.Contains(normalized))
            return;

        list.Add(normalized);
        lookup.Add(normalized);
        NotifyChanged();
    }

    private static void RebuildKeyLookup(List<string> list, HashSet<string> lookup)
    {
        lookup.Clear();
        for (int i = list.Count - 1; i >= 0; i--)
        {
            string normalized = NormalizeKey(list[i]);
            if (string.IsNullOrEmpty(normalized))
            {
                list.RemoveAt(i);
                continue;
            }

            list[i] = normalized;
            lookup.Add(normalized);
        }
    }

    private void NotifyChanged()
    {
        ProgressChanged?.Invoke();
    }

    private void NotifyResourceChanged(ResourceType type, int value)
    {
        TutorialResourceChanged?.Invoke(type, value);
        NotifyChanged();
    }

    private void ResetResources(bool notify)
    {
        money = 0;
        chip = 0;
        crystal = 0;
        supply = 0;

        if (!notify)
            return;

        TutorialResourceChanged?.Invoke(ResourceType.Money, money);
        TutorialResourceChanged?.Invoke(ResourceType.Chip, chip);
        TutorialResourceChanged?.Invoke(ResourceType.Crystal, crystal);
        TutorialResourceChanged?.Invoke(ResourceType.Supply, supply);
        NotifyChanged();
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }
}
