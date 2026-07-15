using System;
using System.Collections.Generic;
using UnityEngine;

public enum DHChatEffectKind
{
    None,
    SetFlag,
    StartBattle,
    Reward,
    RevealFog,
    Unknown
}

[Serializable]
public class DHChatFlagState
{
    public string flagName;
    public bool value;

    public DHChatFlagState() { }

    public DHChatFlagState(string flagName, bool value)
    {
        this.flagName = flagName;
        this.value = value;
    }
}

[Serializable]
public class DHChatNumericState
{
    public string key;
    public float value;

    public DHChatNumericState() { }

    public DHChatNumericState(string key, float value)
    {
        this.key = key;
        this.value = value;
    }
}

[Serializable]
public class DHChatEffectToken
{
    public string rawToken;
    public DHChatEffectKind kind;
    public string payload;

    public DHChatEffectToken() { }

    public DHChatEffectToken(string rawToken, DHChatEffectKind kind, string payload)
    {
        this.rawToken = rawToken;
        this.kind = kind;
        this.payload = payload;
    }
}

[DisallowMultipleComponent]
public sealed class DHChatEffectRuntimeManager : MonoBehaviour
{
    private const string RootName = "[DH_ChatEffectRuntime]";
    private const string SetFlagPrefix = "Set_Flag_";
    private const string StartBattlePrefix = "Start_Battle_";
    private const string RewardPrefix = "Reward_";
    private const string RevealFogPrefix = "Reveal_Fog_";

    public static DHChatEffectRuntimeManager Instance { get; private set; }

    [Header("Runtime Flags")]
    [SerializeField] private List<DHChatFlagState> flags = new List<DHChatFlagState>();

    [Header("Runtime Numeric Values")]
    [SerializeField] private List<DHChatNumericState> numericStates = new List<DHChatNumericState>();

    [Header("Debug")]
    [SerializeField] private List<DHChatEffectToken> lastParsedEffects = new List<DHChatEffectToken>();

    private readonly Dictionary<string, DHChatFlagState> flagLookup = new Dictionary<string, DHChatFlagState>(StringComparer.Ordinal);
    private readonly Dictionary<string, DHChatNumericState> numericLookup = new Dictionary<string, DHChatNumericState>(StringComparer.Ordinal);

    public IReadOnlyList<DHChatFlagState> Flags => flags;
    public IReadOnlyList<DHChatNumericState> NumericStates => numericStates;
    public IReadOnlyList<DHChatEffectToken> LastParsedEffects => lastParsedEffects;

    public event Action<string, bool> FlagChanged;
    public event Action<string, float> NumericValueChanged;
    public event Action<string> BattleRequested;
    public event Action<string> RewardRequested;
    public event Action<string> FogRevealRequested;
    public event Action<string> UnknownEffectReceived;
    public event Action<DHChatEffectToken> EffectDispatched;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static DHChatEffectRuntimeManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject root = new GameObject(RootName);
        DontDestroyOnLoad(root);
        return root.AddComponent<DHChatEffectRuntimeManager>();
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
            DHChatFlagState state = flags[i];
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
            DHChatNumericState state = numericStates[i];
            if (state == null || string.IsNullOrWhiteSpace(state.key))
            {
                numericStates.RemoveAt(i);
                continue;
            }

            state.key = NormalizeKey(state.key);
            numericLookup[state.key] = state;
        }
    }

    public void ExecuteEffects(string rawEffect)
    {
        lastParsedEffects.Clear();

        IReadOnlyList<DHChatEffectToken> tokens = ParseEffects(rawEffect);
        for (int i = 0; i < tokens.Count; i++)
        {
            DHChatEffectToken token = tokens[i];
            lastParsedEffects.Add(token);
            ExecuteToken(token);
        }
    }

    public void ExecuteToken(DHChatEffectToken token)
    {
        if (token == null || token.kind == DHChatEffectKind.None)
            return;

        switch (token.kind)
        {
            case DHChatEffectKind.SetFlag:
                SetFlag(token.payload, true);
                break;
            case DHChatEffectKind.StartBattle:
                Debug.Log($"[DHChatEffectRuntime] Battle requested: {token.payload}");
                BattleRequested?.Invoke(token.payload);
                break;
            case DHChatEffectKind.Reward:
                Debug.Log($"[DHChatEffectRuntime] Reward requested: {token.payload}");
                RewardRequested?.Invoke(token.payload);
                break;
            case DHChatEffectKind.RevealFog:
                Debug.Log($"[DHChatEffectRuntime] Fog reveal requested: {token.payload}");
                FogRevealRequested?.Invoke(token.payload);
                break;
            default:
                Debug.LogWarning($"[DHChatEffectRuntime] Unknown chat effect: {token.rawToken}");
                UnknownEffectReceived?.Invoke(token.rawToken);
                break;
        }

        EffectDispatched?.Invoke(token);
    }

    public bool GetFlag(string flagName)
    {
        string normalized = NormalizeFlagName(flagName);
        return !string.IsNullOrEmpty(normalized) &&
               flagLookup.TryGetValue(normalized, out DHChatFlagState state) &&
               state != null &&
               state.value;
    }

    public void SetFlag(string flagName, bool value)
    {
        string normalized = NormalizeFlagName(flagName);
        if (string.IsNullOrEmpty(normalized))
            return;

        if (!flagLookup.TryGetValue(normalized, out DHChatFlagState state) || state == null)
        {
            state = new DHChatFlagState(normalized, value);
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

        if (numericLookup.TryGetValue(normalized, out DHChatNumericState numericState) && numericState != null)
        {
            value = numericState.value;
            return true;
        }

        string flagName = NormalizeFlagName(normalized);
        if (flagLookup.TryGetValue(flagName, out DHChatFlagState flagState) && flagState != null)
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

        if (!numericLookup.TryGetValue(normalized, out DHChatNumericState state) || state == null)
        {
            state = new DHChatNumericState(normalized, value);
            numericStates.Add(state);
            numericLookup[normalized] = state;
        }
        else
        {
            state.value = value;
        }

        NumericValueChanged?.Invoke(normalized, value);
    }

    public List<DHChatFlagState> CaptureFlagSnapshot()
    {
        var snapshot = new List<DHChatFlagState>(flags.Count);
        for (int i = 0; i < flags.Count; i++)
        {
            DHChatFlagState state = flags[i];
            if (state == null || string.IsNullOrWhiteSpace(state.flagName))
                continue;

            snapshot.Add(new DHChatFlagState(NormalizeFlagName(state.flagName), state.value));
        }

        return snapshot;
    }

    public void RestoreFlagSnapshot(IEnumerable<DHChatFlagState> snapshot)
    {
        flags.Clear();
        flagLookup.Clear();

        if (snapshot == null)
            return;

        foreach (DHChatFlagState state in snapshot)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.flagName))
                continue;

            SetFlag(state.flagName, state.value);
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

    public static IReadOnlyList<DHChatEffectToken> ParseEffects(string rawEffect)
    {
        var result = new List<DHChatEffectToken>();
        if (string.IsNullOrWhiteSpace(rawEffect))
            return result;

        string[] parts = rawEffect.Split(new[] { '\\', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string token = parts[i]?.Trim();
            if (string.IsNullOrEmpty(token))
                continue;

            result.Add(ClassifyToken(token));
        }

        return result;
    }

    public static DHChatEffectToken ClassifyToken(string rawToken)
    {
        string token = rawToken?.Trim();
        if (string.IsNullOrEmpty(token))
            return new DHChatEffectToken(string.Empty, DHChatEffectKind.None, string.Empty);

        if (token.StartsWith(SetFlagPrefix, StringComparison.Ordinal))
        {
            string flagName = "Flag_" + token.Substring(SetFlagPrefix.Length);
            return new DHChatEffectToken(token, DHChatEffectKind.SetFlag, NormalizeFlagName(flagName));
        }

        if (token.StartsWith(StartBattlePrefix, StringComparison.Ordinal))
            return new DHChatEffectToken(token, DHChatEffectKind.StartBattle, token.Substring(StartBattlePrefix.Length).Trim());

        if (token.StartsWith(RewardPrefix, StringComparison.Ordinal))
            return new DHChatEffectToken(token, DHChatEffectKind.Reward, token.Substring(RewardPrefix.Length).Trim());

        if (token.StartsWith(RevealFogPrefix, StringComparison.Ordinal))
            return new DHChatEffectToken(token, DHChatEffectKind.RevealFog, token.Substring(RevealFogPrefix.Length).Trim());

        return new DHChatEffectToken(token, DHChatEffectKind.Unknown, token);
    }

    public static string NormalizeFlagName(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName))
            return string.Empty;

        string normalized = flagName.Trim();
        if (normalized.StartsWith(SetFlagPrefix, StringComparison.Ordinal))
            normalized = "Flag_" + normalized.Substring(SetFlagPrefix.Length);

        return normalized;
    }

    public static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }
}
