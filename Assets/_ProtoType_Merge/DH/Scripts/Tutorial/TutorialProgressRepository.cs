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

[DisallowMultipleComponent]
public sealed class TutorialProgressRepository : MonoBehaviour
{
    private const string RootName = "[DH_TutorialProgressRepository]";

    public static TutorialProgressRepository Instance { get; private set; }

    [Header("Tutorial Progress")]
    [SerializeField] private bool tutorialCompleted;
    [SerializeField] private int currentOrder = 1;
    [SerializeField] private TutorialPartyProgressState partyState = new TutorialPartyProgressState();
    [SerializeField] private List<TutorialUnitProgressState> unitStates = new List<TutorialUnitProgressState>();
    [SerializeField] private List<string> completedCellEventKeys = new List<string>();
    [SerializeField] private List<string> seenMessageKeys = new List<string>();
    [SerializeField] private List<string> inactiveObjectKeys = new List<string>();

    private readonly Dictionary<string, TutorialUnitProgressState> unitLookup =
        new Dictionary<string, TutorialUnitProgressState>(StringComparer.Ordinal);
    private readonly HashSet<string> completedCellEventLookup =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> seenMessageLookup =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> inactiveObjectLookup =
        new HashSet<string>(StringComparer.Ordinal);

    public bool TutorialCompleted => tutorialCompleted;
    public int CurrentOrder => currentOrder;
    public TutorialPartyProgressState PartyState => partyState;
    public IReadOnlyList<TutorialUnitProgressState> UnitStates => unitStates;
    public IReadOnlyList<string> CompletedCellEventKeys => completedCellEventKeys;
    public IReadOnlyList<string> SeenMessageKeys => seenMessageKeys;
    public IReadOnlyList<string> InactiveObjectKeys => inactiveObjectKeys;
    public bool HasAnyJoinedUnit => GetJoinedUnitCount() > 0;

    public event Action ProgressChanged;

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
        currentOrder = Mathf.Max(1, currentOrder);
        RebuildLookups();
    }

    public void ResetProgress()
    {
        tutorialCompleted = false;
        currentOrder = 1;
        partyState.Clear();
        unitStates.Clear();
        completedCellEventKeys.Clear();
        seenMessageKeys.Clear();
        inactiveObjectKeys.Clear();
        RebuildLookups();
        NotifyChanged();
    }

    public void MarkTutorialCompleted()
    {
        if (tutorialCompleted)
            return;

        tutorialCompleted = true;
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

    [ContextMenu("Rebuild Lookup")]
    public void RebuildLookups()
    {
        unitLookup.Clear();
        RebuildKeyLookup(completedCellEventKeys, completedCellEventLookup);
        RebuildKeyLookup(seenMessageKeys, seenMessageLookup);
        RebuildKeyLookup(inactiveObjectKeys, inactiveObjectLookup);

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

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }
}
