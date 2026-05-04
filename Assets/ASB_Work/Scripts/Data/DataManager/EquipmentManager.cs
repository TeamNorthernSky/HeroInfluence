using System.Collections.Generic;
using UnityEngine;

public class EquipmentManager : MonoBehaviour
{
    [System.Serializable]
    public class CharactorEquipmentLoadout
    {
        public string charactorId;
        public List<string> equippedEquipmentIds = new List<string>();
    }

    [Header("Master Data")]
    [SerializeField] private List<EquipmentData> equipmentDatabase = new List<EquipmentData>();

    [Header("Equip State")]
    [SerializeField] private List<CharactorEquipmentLoadout> loadouts = new List<CharactorEquipmentLoadout>();

    public EquipmentData GetEquipmentData(string equipmentId)
    {
        return equipmentDatabase.Find(x => x != null && x.EquipmentId == equipmentId);
    }

    public List<EquipmentData> GetEquippedEquipments(string charactorId)
    {
        var result = new List<EquipmentData>();
        var loadout = loadouts.Find(x => x.charactorId == charactorId);
        if (loadout == null)
        {
            return result;
        }

        for (int i = 0; i < loadout.equippedEquipmentIds.Count; i++)
        {
            string equipmentId = loadout.equippedEquipmentIds[i];
            var data = GetEquipmentData(equipmentId);
            if (data != null)
            {
                result.Add(data);
            }
        }

        return result;
    }
}
