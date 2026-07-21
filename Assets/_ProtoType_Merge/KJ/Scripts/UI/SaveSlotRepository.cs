using System.IO;
using UnityEngine;

public static class SaveSlotRepository
{
    // [KJ 260703] 타이틀에서 고른 슬롯 번호. 게임 씬의 저장(GameSaveService)이 대상 슬롯으로 사용. 미설정 -1.
    public static int CurrentSlot = -1;

    // [KJ 260714] 이어하기 여부. true면 GameLoadGate가 새게임 리셋 대신 CurrentSlot 저장 데이터를 복원한다.
    // 새게임/이어하기 파일 모두 hasData=true가 될 수 있어(신규 슬롯은 껍데기 저장) 파일 내용으로 구분 불가 → 명시 플래그 사용.
    public static bool IsContinue = false;

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
