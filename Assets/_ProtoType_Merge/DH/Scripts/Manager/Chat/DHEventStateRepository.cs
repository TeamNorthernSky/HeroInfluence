using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DHEventFlagState
{
    public string flagName;
    public bool value;

    public DHEventFlagState() { }

    public DHEventFlagState(string flagName, bool value)
    {
        this.flagName = flagName;
        this.value = value;
    }
}

[Serializable]
public class DHEventNumericState
{
    public string key;
    public float value;

    public DHEventNumericState() { }

    public DHEventNumericState(string key, float value)
    {
        this.key = key;
        this.value = value;
    }
}

[DisallowMultipleComponent]
public sealed class DHEventStateRepository : MonoBehaviour
{
    private const string RootName = "[DH_EventStateRepository]";
    private const string SetFlagPrefix = "Set_Flag_";
    private const string DisableFlagPrefix = "Disable_Flag_";
    private const string FlagPrefix = "Flag_";

    public static DHEventStateRepository Instance { get; private set; }

    [Header("Runtime Flags")]
    [SerializeField] private List<DHEventFlagState> flags = new List<DHEventFlagState>();

    [Header("Runtime Numeric Values")]
    [SerializeField] private List<DHEventNumericState> numericStates = new List<DHEventNumericState>();

    // DDOL store for event branch state. Snapshot/save code copies these lists through capture APIs.
    private readonly Dictionary<string, DHEventFlagState> flagLookup =
        new Dictionary<string, DHEventFlagState>(StringComparer.Ordinal);
    private readonly Dictionary<string, DHEventNumericState> numericLookup =
        new Dictionary<string, DHEventNumericState>(StringComparer.Ordinal);

    public IReadOnlyList<DHEventFlagState> Flags => flags;
    public IReadOnlyList<DHEventNumericState> NumericStates => numericStates;

    public event Action<string, bool> FlagChanged;
    public event Action<string, float> NumericValueChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static DHEventStateRepository EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject root = new GameObject(RootName);
        DontDestroyOnLoad(root);
        return root.AddComponent<DHEventStateRepository>();
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
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    [ContextMenu("Rebuild Lookup")]
    public void RebuildLookup()
    {
        flagLookup.Clear();
        numericLookup.Clear();

        for (int i = flags.Count - 1; i >= 0; i--)
        {
            DHEventFlagState state = flags[i];
            if (state == null || string.IsNullOrWhiteSpace(state.flagName))
            {
                flags.RemoveAt(i);
                continue;
            }

            state.flagName = NormalizeFlagName(state.flagName);
            flagLookup[state.flagName] = state;
        }

        for (int i = numericStates.Count - 1; i >= 0; i--)
        {
            DHEventNumericState state = numericStates[i];
            if (state == null || string.IsNullOrWhiteSpace(state.key))
            {
                numericStates.RemoveAt(i);
                continue;
            }

            state.key = NormalizeKey(state.key);
            numericLookup[state.key] = state;
        }
    }

    public bool GetFlag(string flagName)
    {
        string normalized = NormalizeFlagName(flagName);
        return !string.IsNullOrEmpty(normalized) &&
               flagLookup.TryGetValue(normalized, out DHEventFlagState state) &&
               state != null &&
               state.value;
    }

    public void SetFlag(string flagName, bool value)
    {
        string normalized = NormalizeFlagName(flagName);
        if (string.IsNullOrEmpty(normalized))
            return;

        if (!flagLookup.TryGetValue(normalized, out DHEventFlagState state) || state == null)
        {
            state = new DHEventFlagState(normalized, value);
            flags.Add(state);
            flagLookup[normalized] = state;
        }
        else
        {
            state.value = value;
        }

        FlagChanged?.Invoke(normalized, value);
    }

    public bool TryGetNumericValue(string key, out float value)
    {
        value = 0f;
        string normalized = NormalizeKey(key);
        if (string.IsNullOrEmpty(normalized))
            return false;

        // Numeric states also cover temporary battle results such as Flag_BattleResult and HostageInjuredCount.
        if (numericLookup.TryGetValue(normalized, out DHEventNumericState numericState) && numericState != null)
        {
            value = numericState.value;
            return true;
        }

        string flagName = NormalizeFlagName(normalized);
        if (flagLookup.TryGetValue(flagName, out DHEventFlagState flagState) && flagState != null)
        {
            value = flagState.value ? 1f : 0f;
            return true;
        }

        return false;
    }

    public void SetNumericValue(string key, float value)
    {
        string normalized = NormalizeKey(key);
        if (string.IsNullOrEmpty(normalized))
            return;

        if (!numericLookup.TryGetValue(normalized, out DHEventNumericState state) || state == null)
        {
            state = new DHEventNumericState(normalized, value);
            numericStates.Add(state);
            numericLookup[normalized] = state;
        }
        else
        {
            state.value = value;
        }

        NumericValueChanged?.Invoke(normalized, value);
    }

    public List<DHEventFlagState> CaptureFlagSnapshot()
    {
        var snapshot = new List<DHEventFlagState>(flags.Count);
        for (int i = 0; i < flags.Count; i++)
        {
            DHEventFlagState state = flags[i];
            if (state == null || string.IsNullOrWhiteSpace(state.flagName))
                continue;

            snapshot.Add(new DHEventFlagState(NormalizeFlagName(state.flagName), state.value));
        }

        return snapshot;
    }

    public List<DHEventNumericState> CaptureNumericSnapshot()
    {
        var snapshot = new List<DHEventNumericState>(numericStates.Count);
        for (int i = 0; i < numericStates.Count; i++)
        {
            DHEventNumericState state = numericStates[i];
            if (state == null || string.IsNullOrWhiteSpace(state.key))
                continue;

            snapshot.Add(new DHEventNumericState(
                NormalizeKey(state.key),
                state.value));
        }

        return snapshot;
    }

    public void RestoreFlagSnapshot(IEnumerable<DHEventFlagState> snapshot)
    {
        flags.Clear();
        flagLookup.Clear();

        if (snapshot == null)
            return;

        foreach (DHEventFlagState state in snapshot)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.flagName))
                continue;

            SetFlag(state.flagName, state.value);
        }
    }

    public void RestoreNumericSnapshot(IEnumerable<DHEventNumericState> snapshot)
    {
        numericStates.Clear();
        numericLookup.Clear();

        if (snapshot == null)
            return;

        foreach (DHEventNumericState state in snapshot)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.key))
                continue;

            SetNumericValue(state.key, state.value);
        }
    }

    [ContextMenu("Clear Flags")]
    public void ClearFlags()
    {
        flags.Clear();
        flagLookup.Clear();
    }

    [ContextMenu("Clear Numeric Values")]
    public void ClearNumericValues()
    {
        numericStates.Clear();
        numericLookup.Clear();
    }

    [ContextMenu("Clear All Event State")]
    public void ClearAllState()
    {
        ClearFlags();
        ClearNumericValues();
    }

    public static string NormalizeFlagName(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName))
            return string.Empty;

        // Data table effects may use Set_Flag_*/Disable_Flag_* while branch conditions usually use Flag_*.
        string normalized = flagName.Trim();
        if (normalized.StartsWith(SetFlagPrefix, StringComparison.Ordinal))
            normalized = FlagPrefix + normalized.Substring(SetFlagPrefix.Length);
        else if (normalized.StartsWith(DisableFlagPrefix, StringComparison.Ordinal))
            normalized = FlagPrefix + normalized.Substring(DisableFlagPrefix.Length);
        else if (!normalized.StartsWith(FlagPrefix, StringComparison.Ordinal))
            normalized = FlagPrefix + normalized;

        return normalized;
    }

    public static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }
}
