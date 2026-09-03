using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Transitional scene bridge for the shared BattleUnitPrefabCatalog.
/// New consumers should reference the catalog directly when possible.
/// </summary>
[DisallowMultipleComponent]
public sealed class TmpBattlePrefabManager : MonoBehaviour
{
    [Serializable]
    public sealed class PrefabEntry
    {
        [SerializeField] private string index;
        [SerializeField] private GameObject prefab;

        public string Index => index;
        public GameObject Prefab => prefab;
    }

    [Header("Shared Catalog")]
    [SerializeField] private BattleUnitPrefabCatalog catalog;

    [Header("Legacy Registry")]
    [SerializeField] private List<PrefabEntry> prefabs = new List<PrefabEntry>();

    private readonly Dictionary<string, GameObject> prefabByIndex =
        new Dictionary<string, GameObject>(StringComparer.Ordinal);

    public static TmpBattlePrefabManager Instance { get; private set; }
    public BattleUnitPrefabCatalog Catalog => catalog;
    public IReadOnlyList<PrefabEntry> Prefabs => prefabs;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[TmpBattlePrefabManager] The current scene contains more than one manager.", this);
            enabled = false;
            return;
        }

        Instance = this;
        RebuildLookup();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public GameObject GetPrefab(string index)
    {
        TryGetPrefab(index, out GameObject prefab);
        return prefab;
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
        if (catalog != null && catalog.TryGetPlayerPrefab(unitKey, out prefab))
            return true;

        return TryGetLegacyPrefab(unitKey, out prefab);
    }

    public bool TryGetEnemyPrefab(string unitKey, out GameObject prefab)
    {
        if (catalog != null && catalog.TryGetEnemyPrefab(unitKey, out prefab))
            return true;

        return TryGetLegacyPrefab(unitKey, out prefab);
    }

    public bool TryGetPrefab(BattleUnitPrefabSide side, string unitKey, out GameObject prefab)
    {
        return side == BattleUnitPrefabSide.Player
            ? TryGetPlayerPrefab(unitKey, out prefab)
            : TryGetEnemyPrefab(unitKey, out prefab);
    }

    public bool TryGetPrefab(string index, out GameObject prefab)
    {
        if (TryGetLegacyPrefab(index, out prefab))
            return true;

        if (catalog != null && catalog.TryGetPlayerPrefab(index, out prefab))
            return true;

        return catalog != null && catalog.TryGetEnemyPrefab(index, out prefab);
    }

    public void RebuildLookup()
    {
        if (catalog != null)
            catalog.RebuildLookup();

        prefabByIndex.Clear();

        for (int i = 0; i < prefabs.Count; i++)
        {
            PrefabEntry entry = prefabs[i];
            if (entry == null)
            {
                Debug.LogWarning($"[TmpBattlePrefabManager] Prefabs[{i}] 항목이 비어 있습니다.", this);
                continue;
            }

            string key = NormalizeIndex(entry.Index);
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning($"[TmpBattlePrefabManager] Prefabs[{i}]의 인덱스가 비어 있습니다.", this);
                continue;
            }

            if (entry.Prefab == null)
            {
                Debug.LogWarning($"[TmpBattlePrefabManager] 인덱스 '{key}'에 프리팹이 등록되지 않았습니다.", this);
                continue;
            }

            if (prefabByIndex.ContainsKey(key))
            {
                Debug.LogError($"[TmpBattlePrefabManager] 인덱스 '{key}'가 중복 등록되었습니다.", this);
                continue;
            }

            prefabByIndex.Add(key, entry.Prefab);
        }
    }


    private bool TryGetLegacyPrefab(string index, out GameObject prefab)
    {
        prefab = null;
        string key = NormalizeIndex(index);
        return !string.IsNullOrEmpty(key) &&
               prefabByIndex.TryGetValue(key, out prefab) &&
               prefab != null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildLookup();
    }
#endif

    private static string NormalizeIndex(string index)
    {
        return string.IsNullOrWhiteSpace(index) ? string.Empty : index.Trim();
    }
}
