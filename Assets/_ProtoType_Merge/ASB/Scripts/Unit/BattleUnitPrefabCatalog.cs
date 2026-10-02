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
    [Tooltip("이 프리팹(외형)을 함께 쓰는 다른 데이터 인덱스. 예: 소총수 20002 프리팹을 인질극 소총수 20006도 사용. " +
             "스탯·스킬·AI는 각 인덱스의 테이블 행을 그대로 쓰고, 프리팹만 이 항목을 공유한다.")]
    [SerializeField] private List<string> additionalUnitKeys = new List<string>();

    public string UnitKey => unitKey;
    public GameObject Prefab => prefab;
    public IReadOnlyList<string> AdditionalUnitKeys => additionalUnitKeys;
}

/// <summary>
/// Shared source of truth for Player and Enemy battle unit prefabs.
/// Keys are matched exactly with StringComparer.Ordinal after trimming whitespace.
/// 데이터 인덱스(스탯·스킬·AI)와 외형(프리팹)을 분리한다: 한 항목 = 프리팹 1개 + 그 프리팹을 쓰는 인덱스들
/// (대표 unitKey + additionalUnitKeys).
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

            AddKey(lookup, side, key, entry.Prefab);

            IReadOnlyList<string> aliases = entry.AdditionalUnitKeys;
            if (aliases == null)
                continue;

            for (int a = 0; a < aliases.Count; a++)
            {
                string alias = NormalizeKey(aliases[a]);
                if (string.IsNullOrEmpty(alias))
                {
                    Debug.LogWarning($"[BattleUnitPrefabCatalog] {side} key '{key}' has an empty additional key.", this);
                    continue;
                }

                AddKey(lookup, side, alias, entry.Prefab);
            }
        }
    }

    private void AddKey(Dictionary<string, GameObject> lookup, BattleUnitPrefabSide side, string key, GameObject prefab)
    {
        if (lookup.ContainsKey(key))
        {
            Debug.LogError($"[BattleUnitPrefabCatalog] Duplicate {side} unit key '{key}'.", this);
            return;
        }

        lookup.Add(key, prefab);
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
