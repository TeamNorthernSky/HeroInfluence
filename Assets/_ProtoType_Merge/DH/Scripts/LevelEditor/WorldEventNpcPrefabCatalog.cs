using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WorldEventNpcPrefabCatalog",
    menuName = "DH Work/Prefab Catalogs/World Event NPC Prefab Catalog")]
public sealed class WorldEventNpcPrefabCatalog : ScriptableObject
{
    [SerializeField] private WorldEventObject worldEventBasePrefab;
    [SerializeField] private List<WorldEventNpcPrefabEntry> npcPrefabs = new List<WorldEventNpcPrefabEntry>();

    public bool TryGetWorldEventPrefab(out WorldEventObject prefab)
    {
        prefab = worldEventBasePrefab;
        return prefab != null;
    }

    public bool TryGetNpcPrefab(int npcType, out GameObject prefab)
    {
        if (npcPrefabs != null)
        {
            for (int i = 0; i < npcPrefabs.Count; i++)
            {
                WorldEventNpcPrefabEntry entry = npcPrefabs[i];
                if (entry.NpcType != npcType)
                    continue;

                prefab = entry.Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }

    public bool TryGetQuestionIcon(int npcType, out Sprite icon)
    {
        if (npcPrefabs != null)
        {
            for (int i = 0; i < npcPrefabs.Count; i++)
            {
                WorldEventNpcPrefabEntry entry = npcPrefabs[i];
                if (entry.NpcType != npcType)
                    continue;

                icon = entry.QuestionIcon;
                return icon != null;
            }
        }

        icon = null;
        return false;
    }

    public bool ShouldFacePartyOnInteraction(int npcType)
    {
        if (npcPrefabs != null)
        {
            for (int i = 0; i < npcPrefabs.Count; i++)
            {
                WorldEventNpcPrefabEntry entry = npcPrefabs[i];
                if (entry.NpcType == npcType)
                    return entry.Prefab != null && entry.FacePartyOnInteraction;
            }
        }

        return false;
    }
}

[Serializable]
public struct WorldEventNpcPrefabEntry
{
    [SerializeField] private int npcType;
    [SerializeField] private GameObject prefab;
    [SerializeField] private bool facePartyOnInteraction;
    [SerializeField] private Sprite questionIcon;

    public int NpcType => npcType;
    public GameObject Prefab => prefab;
    public bool FacePartyOnInteraction => facePartyOnInteraction;
    public Sprite QuestionIcon => questionIcon;
}
