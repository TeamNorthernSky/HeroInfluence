using System.IO;
using UnityEngine;

public static class SaveSlotRepository
{
    // [KJ 260703] 타이틀에서 고른 슬롯 번호. 게임 씬의 저장(GameSaveService)이 대상 슬롯으로 사용. 미설정 -1.
    public static int CurrentSlot = -1;

    // [KJ 260714] 이어하기 여부. true면 GameLoadGate가 새게임 리셋 대신 CurrentSlot 저장 데이터를 복원한다.
    // hasData reserves a slot; hasGameState identifies a completed exploration save.
    public static bool IsContinue = false;

    private static string GetPath(int slotIndex) =>
        Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");

    public static SaveSlotData Load(int slotIndex)
    {
        string path = GetPath(slotIndex);
        if (!File.Exists(path)) return new SaveSlotData { hasData = false };
        var saved = JsonUtility.FromJson<SlotStateProbe>(File.ReadAllText(path));
        if (saved == null) return new SaveSlotData();
        return new SaveSlotData
        {
            hasData = saved.hasData,
            savedAt = saved.savedAt,
            // Older complete saves have no hasGameState field, so validate their payload too.
            hasGameState = HasPlayableState(saved.hasData, saved.currentDay,
                saved.units?.Length ?? 0, saved.parties?.Length ?? 0, saved.resources?.Length ?? 0)
        };
    }

    public static bool CanContinue(GameSaveData data)
    {
        return data != null && HasPlayableState(data.hasData, data.currentDay,
            data.units?.Count ?? 0, data.parties?.Count ?? 0, data.resources?.Count ?? 0);
    }

    private static bool HasPlayableState(bool hasData, int day, int units, int parties, int resources)
    {
        return hasData && day >= 1 && units > 0 && parties > 0 && resources > 0;
    }

    [System.Serializable]
    private sealed class SlotStateProbe : SaveSlotData
    {
        public int currentDay;
        public UnitPersistentDataDiskRow[] units;
        public PartyPersistentData[] parties;
        public GameSaveData.ResourceEntry[] resources;
    }

    public static void Save(int slotIndex)
    {
        var data = new SaveSlotData
        {
            hasData = true,
            hasGameState = false,
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
