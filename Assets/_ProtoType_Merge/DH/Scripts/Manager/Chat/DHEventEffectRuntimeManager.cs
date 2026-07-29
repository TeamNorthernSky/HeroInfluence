using System;
using System.Collections.Generic;
using UnityEngine;

public enum DHEventEffectKind
{
    None,
    SetFlag,
    DisableFlag,
    StartBattle,
    Reward,
    RevealFog,
    DisableEvent,
    Unknown
}

[Serializable]
public class DHEventEffectToken
{
    public string rawToken;
    public DHEventEffectKind kind;
    public string payload;

    public DHEventEffectToken() { }

    public DHEventEffectToken(string rawToken, DHEventEffectKind kind, string payload)
    {
        this.rawToken = rawToken;
        this.kind = kind;
        this.payload = payload;
    }
}

[Serializable]
public class DHEventEffectExecutionContext
{
    public int ZoneId;
    public int SourceChatId;
    public int ResumeChatId;

    public DHEventEffectExecutionContext() { }

    public DHEventEffectExecutionContext(int zoneId, int sourceChatId, int resumeChatId)
    {
        ZoneId = zoneId;
        SourceChatId = sourceChatId;
        ResumeChatId = resumeChatId;
    }
}

[Serializable]
public class DHEventBattleEffectRequest
{
    public string BattleKey;
    public int ZoneId;
    public int SourceChatId;
    public int ResumeChatId;

    public DHEventBattleEffectRequest() { }

    public DHEventBattleEffectRequest(string battleKey, DHEventEffectExecutionContext context)
    {
        BattleKey = battleKey;

        if (context == null)
            return;

        ZoneId = context.ZoneId;
        SourceChatId = context.SourceChatId;
        ResumeChatId = context.ResumeChatId;
    }
}

[DisallowMultipleComponent]
public sealed class DHEventEffectRuntimeManager : MonoBehaviour
{
    private const string RootName = "[DH_EventEffectRuntime]";
    private const string SetFlagPrefix = "Set_Flag_";
    private const string DisableFlagPrefix = "Disable_Flag_";
    private const string StartBattlePrefix = "Start_Battle_";
    private const string StartBattleEventPrefix = "Start_BE";
    private const string RewardPrefix = "Reward_";
    private const string RevealFogPrefix = "Reveal_Fog_";
    private const string DisablePrefix = "Disable_";

    public static DHEventEffectRuntimeManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool logUnhandledEffects;
    [SerializeField] private List<DHEventEffectToken> lastParsedEffects = new List<DHEventEffectToken>();

    public IReadOnlyList<DHEventEffectToken> LastParsedEffects => lastParsedEffects;

    public event Action<string> BattleRequested;
    public event Action<DHEventBattleEffectRequest> EventBattleRequested;
    public event Action<string> RewardRequested;
    public event Action<string> FogRevealRequested;
    public event Action<string> EventDisableRequested;
    public event Action<string> UnknownEffectReceived;
    public event Action<DHEventEffectToken> EffectDispatched;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static DHEventEffectRuntimeManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject root = new GameObject(RootName);
        DontDestroyOnLoad(root);
        return root.AddComponent<DHEventEffectRuntimeManager>();
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
    }

    public void ExecuteEffects(string rawEffect)
    {
        ExecuteEffects(rawEffect, null);
    }

    public void ExecuteEffects(string rawEffect, DHEventEffectExecutionContext context)
    {
        lastParsedEffects.Clear();

        IReadOnlyList<DHEventEffectToken> tokens = ParseEffects(rawEffect);
        for (int i = 0; i < tokens.Count; i++)
        {
            DHEventEffectToken token = tokens[i];
            lastParsedEffects.Add(token);
            ExecuteToken(token, context);
        }
    }

    public void ExecuteToken(DHEventEffectToken token)
    {
        ExecuteToken(token, null);
    }

    public void ExecuteToken(DHEventEffectToken token, DHEventEffectExecutionContext context)
    {
        if (token == null || token.kind == DHEventEffectKind.None)
            return;

        DHEventStateRepository stateRepository = DHEventStateRepository.EnsureInstance();

        switch (token.kind)
        {
            case DHEventEffectKind.SetFlag:
                stateRepository.SetFlag(token.payload, true);
                break;
            case DHEventEffectKind.DisableFlag:
                stateRepository.SetFlag(token.payload, false);
                break;
            case DHEventEffectKind.StartBattle:
                LogUnhandled(token);
                BattleRequested?.Invoke(token.payload);
                EventBattleRequested?.Invoke(new DHEventBattleEffectRequest(token.payload, context));
                break;
            case DHEventEffectKind.Reward:
                LogUnhandled(token);
                RewardRequested?.Invoke(token.payload);
                break;
            case DHEventEffectKind.RevealFog:
                LogUnhandled(token);
                FogRevealRequested?.Invoke(token.payload);
                break;
            case DHEventEffectKind.DisableEvent:
                LogUnhandled(token);
                ApplyEventDisable(token.payload);
                EventDisableRequested?.Invoke(token.payload);
                break;
            default:
                if (logUnhandledEffects)
                    Debug.LogWarning($"[DHEventEffectRuntime] Unknown event effect: {token.rawToken}", this);
                UnknownEffectReceived?.Invoke(token.rawToken);
                break;
        }

        EffectDispatched?.Invoke(token);
    }

    public static IReadOnlyList<DHEventEffectToken> ParseEffects(string rawEffect)
    {
        var result = new List<DHEventEffectToken>();
        if (string.IsNullOrWhiteSpace(rawEffect))
            return result;

        string[] parts = rawEffect.Split(new[] { '\\', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string token = parts[i]?.Trim().Trim('\'', '"');
            if (string.IsNullOrEmpty(token))
                continue;

            result.Add(ClassifyToken(token));
        }

        return result;
    }

    public static DHEventEffectToken ClassifyToken(string rawToken)
    {
        string token = rawToken?.Trim().Trim('\'', '"');
        if (string.IsNullOrEmpty(token))
            return new DHEventEffectToken(string.Empty, DHEventEffectKind.None, string.Empty);

        if (token.StartsWith(SetFlagPrefix, StringComparison.Ordinal))
            return new DHEventEffectToken(
                token,
                DHEventEffectKind.SetFlag,
                DHEventStateRepository.NormalizeFlagName(token));

        if (token.StartsWith(DisableFlagPrefix, StringComparison.Ordinal))
            return new DHEventEffectToken(
                token,
                DHEventEffectKind.DisableFlag,
                DHEventStateRepository.NormalizeFlagName(token));

        if (token.StartsWith(StartBattlePrefix, StringComparison.Ordinal))
            return new DHEventEffectToken(
                token,
                DHEventEffectKind.StartBattle,
                token.Substring(StartBattlePrefix.Length).Trim());

        if (token.StartsWith(StartBattleEventPrefix, StringComparison.Ordinal))
            return new DHEventEffectToken(token, DHEventEffectKind.StartBattle, token.Substring("Start_".Length).Trim());

        if (token.StartsWith(RewardPrefix, StringComparison.Ordinal))
            return new DHEventEffectToken(token, DHEventEffectKind.Reward, token.Substring(RewardPrefix.Length).Trim());

        if (token.StartsWith(RevealFogPrefix, StringComparison.Ordinal))
            return new DHEventEffectToken(token, DHEventEffectKind.RevealFog, token.Substring(RevealFogPrefix.Length).Trim());

        if (token.StartsWith(DisablePrefix, StringComparison.Ordinal))
            return new DHEventEffectToken(token, DHEventEffectKind.DisableEvent, token.Substring(DisablePrefix.Length).Trim());

        return new DHEventEffectToken(token, DHEventEffectKind.Unknown, token);
    }

    private void LogUnhandled(DHEventEffectToken token)
    {
        if (!logUnhandledEffects || token == null)
            return;

        Debug.Log($"[DHEventEffectRuntime] Event effect recognized but not implemented yet: {token.rawToken}", this);
    }

    private static void ApplyEventDisable(string eventKey)
    {
        string normalizedKey = DHEventStateRepository.NormalizeKey(eventKey);
        if (string.IsNullOrEmpty(normalizedKey))
            return;

        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        if (progressRepository != null)
        {
            progressRepository.MarkMainEventCompleted(normalizedKey);
            progressRepository.MarkSubEventCompleted(normalizedKey);
        }

        DisableMainEvents(normalizedKey);
        DisableSubEvents(normalizedKey);
    }

    private static void DisableMainEvents(string eventKey)
    {
        MainEventObject[] mainEvents = UnityEngine.Object.FindObjectsByType<MainEventObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < mainEvents.Length; i++)
        {
            MainEventObject mainEvent = mainEvents[i];
            if (mainEvent == null || !IsSameEventKey(mainEvent.EventKey, eventKey))
                continue;

            mainEvent.DeactivateByDisableEffect();
        }
    }

    private static void DisableSubEvents(string eventKey)
    {
        SubEventObject[] subEvents = UnityEngine.Object.FindObjectsByType<SubEventObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < subEvents.Length; i++)
        {
            SubEventObject subEvent = subEvents[i];
            if (subEvent == null || !IsSameEventKey(subEvent.EventKey, eventKey))
                continue;

            subEvent.gameObject.SetActive(false);
        }
    }

    private static bool IsSameEventKey(string left, string right)
    {
        return string.Equals(
            DHEventStateRepository.NormalizeKey(left),
            DHEventStateRepository.NormalizeKey(right),
            StringComparison.Ordinal);
    }
}
