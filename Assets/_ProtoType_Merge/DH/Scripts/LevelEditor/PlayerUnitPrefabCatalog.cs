using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PlayerUnitPrefabCatalog",
    menuName = "DH Work/Prefab Catalogs/Player Unit Prefab Catalog")]
public class PlayerUnitPrefabCatalog : ScriptableObject
{
    [SerializeField] private List<PlayerUnitPrefabEntry> playerUnitPrefabs = new List<PlayerUnitPrefabEntry>();

    public bool TryGetPlayerUnitPrefab(string unitTemplateKey, out PartyUnitState prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(unitTemplateKey) ? string.Empty : unitTemplateKey.Trim();
        if (playerUnitPrefabs != null)
        {
            for (int i = 0; i < playerUnitPrefabs.Count; i++)
            {
                if (!string.Equals(playerUnitPrefabs[i].UnitTemplateKey, normalizedKey, StringComparison.Ordinal))
                    continue;

                prefab = playerUnitPrefabs[i].Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }
}

[Serializable]
public struct PlayerUnitPrefabEntry
{
    [SerializeField] private string unitTemplateKey;
    [SerializeField] private PartyUnitState prefab;

    public string UnitTemplateKey => string.IsNullOrWhiteSpace(unitTemplateKey) ? string.Empty : unitTemplateKey.Trim();
    public PartyUnitState Prefab => prefab;
}
