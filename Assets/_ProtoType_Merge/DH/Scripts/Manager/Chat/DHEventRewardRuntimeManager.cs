using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DHEventRewardRuntimeManager : MonoBehaviour
{
    private const string RootName = "[DH_EventRewardRuntime]";
    private const string JusticeKey = "10001";
    private const string RuminaKey = "10002";
    private const string BlackBulletKey = "10003";
    private const string NekomingKey = "10004";

    public static DHEventRewardRuntimeManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool logRewards;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static DHEventRewardRuntimeManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject root = new GameObject(RootName);
        DontDestroyOnLoad(root);
        return root.AddComponent<DHEventRewardRuntimeManager>();
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

    private void OnEnable()
    {
        DHEventEffectRuntimeManager.EnsureInstance().RewardRequested += HandleRewardRequested;
    }

    private void OnDisable()
    {
        DHEventEffectRuntimeManager effectManager = DHEventEffectRuntimeManager.Instance;
        if (effectManager != null)
            effectManager.RewardRequested -= HandleRewardRequested;
    }

    private void HandleRewardRequested(string rewardPayload)
    {
        if (!int.TryParse((rewardPayload ?? string.Empty).Trim(), out int rewardId) || rewardId <= 0)
        {
            Debug.LogWarning($"[DHEventRewardRuntime] Invalid reward id: {rewardPayload}", this);
            return;
        }

        EventScriptCatalog catalog = EventScriptCatalog.Instance;
        if (catalog == null)
        {
            Debug.LogWarning($"[DHEventRewardRuntime] EventScriptCatalog is missing. Reward skipped: {rewardId}", this);
            return;
        }

        if (!catalog.TryGetEventRewardTemplate(rewardId, out DHEventRewardTemplate reward) || reward == null)
        {
            Debug.LogWarning($"[DHEventRewardRuntime] Reward was not found: {rewardId}", this);
            return;
        }

        ApplyReward(reward);
    }

    private void ApplyReward(DHEventRewardTemplate reward)
    {
        if (reward == null)
            return;

        // Rewards are applied immediately to persistent state; reward IDs are not tracked as once-only state here.
        ApplyResources(reward.Money, reward.Medal, reward.Gem, reward.Supply);
        ApplyExpToAlivePartyUnits(reward.Exp);

        IReadOnlyList<DHEventCharacterRewardEntry> characterRewards = reward.CharacterRewards;
        if (characterRewards != null)
        {
            for (int i = 0; i < characterRewards.Count; i++)
            {
                DHEventCharacterRewardEntry entry = characterRewards[i];
                ApplyCharacterReward(
                    entry.UnitTemplateKey,
                    entry.MaxHp,
                    entry.Atk,
                    entry.Def,
                    entry.Influence,
                    entry.Heal);
            }
        }

        SyncPlayerPartySceneUnits();

        if (logRewards)
            Debug.Log($"[DHEventRewardRuntime] Applied {reward.SourceType} reward {reward.RewardId}: {reward.RewardName}", this);
    }

    private static void ApplyResources(int money, int medal, int gem, int supply)
    {
        EconomyManager economy = Game.Economy;
        if (economy == null)
            return;

        // Snapshot/save stores the resulting economy values through DHEconomySnapshotSection.
        economy.Add(ResourceType.Money, money);
        economy.Add(ResourceType.Chip, medal);
        economy.Add(ResourceType.Crystal, gem);
        economy.Add(ResourceType.Supply, supply);
    }

    private static void ApplyExpToAlivePartyUnits(int exp)
    {
        if (exp <= 0)
            return;

        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        if (unitRepository == null)
            return;

        IReadOnlyList<int> unitIndices = ResolvePlayerPartyUnitIndices();
        for (int i = 0; i < unitIndices.Count; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex <= 0 || !unitRepository.TryGetUnit(unitIndex, out UnitPersistentData data) || data == null)
                continue;

            // Dead or incapacitated units do not receive event reward EXP.
            if (data.IsIncapacitated || data.CurrentHp <= 0f)
                continue;

            unitRepository.AddExp(unitIndex, exp);
        }
    }

    private static void ApplyCharacterReward(
        string unitTemplateKey,
        int maxHp,
        int atk,
        int def,
        int influence,
        int heal)
    {
        if (maxHp == 0 && atk == 0 && def == 0 && influence == 0 && heal == 0)
            return;

        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        if (unitRepository == null || !TryFindUnitByTemplateKey(unitRepository, unitTemplateKey, out int unitIndex))
            return;

        unitRepository.ApplyEventRewardStats(unitIndex, maxHp, atk, def, influence, heal);
    }

    private static bool TryFindUnitByTemplateKey(PersistentUnitRepository repository, string unitTemplateKey, out int unitIndex)
    {
        unitIndex = 0;
        if (repository == null || string.IsNullOrWhiteSpace(unitTemplateKey))
            return false;

        IReadOnlyList<UnitPersistentData> units = repository.Units;
        for (int i = 0; i < units.Count; i++)
        {
            UnitPersistentData data = units[i];
            if (data == null)
                continue;

            if (!string.Equals(data.UnitTemplateKey, unitTemplateKey, StringComparison.Ordinal))
                continue;

            unitIndex = data.UnitIndex;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<int> ResolvePlayerPartyUnitIndices()
    {
        PartyPersistentRepository partyRepository = PartyPersistentRepository.Instance;
        PartyGridMover party = ResolvePlayerParty();
        if (partyRepository != null && party != null)
        {
            PartyIdentity identity = party.GetComponent<PartyIdentity>();
            if (identity != null &&
                partyRepository.TryGetParty(identity.PartyId, out PartyPersistentData partyData) &&
                partyData != null)
            {
                return partyData.UnitIndices;
            }
        }

        PartyComposition composition = party != null ? party.GetComponent<PartyComposition>() : null;
        if (composition != null)
            return composition.UnitIndices;

        return ResolveDefaultHeroUnitIndices();
    }

    private static IReadOnlyList<int> ResolveDefaultHeroUnitIndices()
    {
        var result = new List<int>(4);
        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null)
            return result;

        TryAddUnitIndexByTemplateKey(repository, result, JusticeKey);
        TryAddUnitIndexByTemplateKey(repository, result, RuminaKey);
        TryAddUnitIndexByTemplateKey(repository, result, BlackBulletKey);
        TryAddUnitIndexByTemplateKey(repository, result, NekomingKey);
        return result;
    }

    private static void TryAddUnitIndexByTemplateKey(PersistentUnitRepository repository, List<int> result, string unitTemplateKey)
    {
        if (repository == null || result == null)
            return;

        if (TryFindUnitByTemplateKey(repository, unitTemplateKey, out int unitIndex))
            result.Add(unitIndex);
    }

    private static void SyncPlayerPartySceneUnits()
    {
        IReadOnlyList<int> unitIndices = ResolvePlayerPartyUnitIndices();
        if (unitIndices == null || unitIndices.Count == 0)
            return;

        PartyRepositorySync.ApplyUnitsToScene(unitIndices);
    }

    private static PartyGridMover ResolvePlayerParty()
    {
        PartyRegistry registry = FindFirstObjectByType<PartyRegistry>();
        if (registry != null && registry.PlayerParty != null)
            return registry.PlayerParty;

        return FindFirstObjectByType<PartyGridMover>();
    }
}
