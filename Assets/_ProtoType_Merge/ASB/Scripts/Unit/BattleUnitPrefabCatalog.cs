using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleUnitPrefabSide
{
    Player,
    Enemy
}

[Serializable]
public sealed class BattleUnitPrefabEntry
{
    [SerializeField] private string unitKey;
    [SerializeField] private GameObject prefab;

    public string UnitKey => unitKey;
    public GameObject Prefab => prefab;
}

/// <summary>
/// Shared source of truth for Player and Enemy battle unit prefabs.
/// Keys are matched exactly with StringComparer.Ordinal after trimming whitespace.
/// </summary>
[CreateAssetMenu(fileName = "BattleUnitPrefabCatalog", menuName = "Battle/Unit Prefab Catalog")]
public sealed class BattleUnitPrefabCatalog : ScriptableObject
{
    [SerializeField] private List<BattleUnitPrefabEntry> playerPrefabs =
        new List<BattleUnitPrefabEntry>();
    [SerializeField] private List<BattleUnitPrefabEntry> enemyPrefabs =
        new List<BattleUnitPrefabEntry>();

    private readonly Dictionary<string, GameObject> playerPrefabByKey =
        new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private readonly Dictionary<string, GameObject> enemyPrefabByKey =
        new Dictionary<string, GameObject>(StringComparer.Ordinal);

    public IReadOnlyList<BattleUnitPrefabEntry> PlayerPrefabs => playerPrefabs;
    public IReadOnlyList<BattleUnitPrefabEntry> EnemyPrefabs => enemyPrefabs;

    private void OnEnable()
    {
        RebuildLookup();
    }

    public GameObject GetPlayerPrefab(string unitKey)
    {
        TryGetPlayerPrefab(unitKey, out GameObject prefab);
        return prefab;
    }

    public GameObject GetEnemyPrefab(string unitKey)
    {
        TryGetEnemyPrefab(unitKey, out GameObject prefab);
        return prefab;
    }

    public bool TryGetPlayerPrefab(string unitKey, out GameObject prefab)
    {
        return TryGetFromLookup(playerPrefabByKey, unitKey, out prefab);
    }

    public bool TryGetEnemyPrefab(string unitKey, out GameObject prefab)
    {
        return TryGetFromLookup(enemyPrefabByKey, unitKey, out prefab);
    }

    public bool TryGetPrefab(BattleUnitPrefabSide side, string unitKey, out GameObject prefab)
    {
        return side == BattleUnitPrefabSide.Player
            ? TryGetPlayerPrefab(unitKey, out prefab)
            : TryGetEnemyPrefab(unitKey, out prefab);
    }

    public void RebuildLookup()
    {
        playerPrefabByKey.Clear();
        enemyPrefabByKey.Clear();

        BuildLookup(playerPrefabs, playerPrefabByKey, BattleUnitPrefabSide.Player);
        BuildLookup(enemyPrefabs, enemyPrefabByKey, BattleUnitPrefabSide.Enemy);
    }

    private void BuildLookup(
        List<BattleUnitPrefabEntry> entries,
        Dictionary<string, GameObject> lookup,
        BattleUnitPrefabSide side)
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            BattleUnitPrefabEntry entry = entries[i];
            if (entry == null)
            {
                Debug.LogWarning($"[BattleUnitPrefabCatalog] {side}[{i}] entry is null.", this);
                continue;
            }

            string key = NormalizeKey(entry.UnitKey);
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning($"[BattleUnitPrefabCatalog] {side}[{i}] has an empty unit key.", this);
                continue;
            }

            if (entry.Prefab == null)
            {
                Debug.LogWarning($"[BattleUnitPrefabCatalog] {side} key '{key}' has no prefab.", this);
                continue;
            }

            if (lookup.ContainsKey(key))
            {
                Debug.LogError($"[BattleUnitPrefabCatalog] Duplicate {side} unit key '{key}'.", this);
                continue;
            }

            lookup.Add(key, entry.Prefab);
        }
    }

    private static bool TryGetFromLookup(
        Dictionary<string, GameObject> lookup,
        string unitKey,
        out GameObject prefab)
    {
        prefab = null;
        string key = NormalizeKey(unitKey);
        return !string.IsNullOrEmpty(key) &&
               lookup.TryGetValue(key, out prefab) &&
               prefab != null;
    }

    private static string NormalizeKey(string unitKey)
    {
        return string.IsNullOrWhiteSpace(unitKey) ? string.Empty : unitKey.Trim();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildLookup();
    }
#endif
}
