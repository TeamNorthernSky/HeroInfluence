using System.IO;
using UnityEngine;

public static class SaveSlotRepository
{
    private static string GetPath(int slotIndex) =>
        Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");

    public static SaveSlotData Load(int slotIndex)
    {
        string path = GetPath(slotIndex);
        if (!File.Exists(path)) return new SaveSlotData { hasData = false };
        return JsonUtility.FromJson<SaveSlotData>(File.ReadAllText(path));
    }

    public static void Save(int slotIndex)
    {
        var data = new SaveSlotData
        {
            hasData = true,
            savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        };
        File.WriteAllText(GetPath(slotIndex), JsonUtility.ToJson(data));
    }

    public static void Delete(int slotIndex)
    {
        string path = GetPath(slotIndex);
        if (File.Exists(path)) File.Delete(path);
    }
}
