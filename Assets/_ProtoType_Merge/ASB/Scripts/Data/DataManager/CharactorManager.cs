using System.Collections.Generic;
using UnityEngine;

public class CharactorManager : MonoBehaviour
{
    [System.Serializable]
    public class OwnedCharactorInfo
    {
        public string charactorId;
        public int level = 1;
        public bool isOwned = true;
    }

    // CSV 파싱 결과 캐시
    private readonly Dictionary<string, UnitData> unitTable = new Dictionary<string, UnitData>();

    [Header("Player Data (Temporary In-Memory)")]
    [SerializeField] private List<OwnedCharactorInfo> ownedCharactors = new List<OwnedCharactorInfo>();
    [SerializeField] private string selectedCharactorId;

    // 스펙: DataManager가 CSVLoader로부터 받은 List<UnitData>를 전달하면 캐싱합니다.
    public void SetUnitData(List<UnitData> units)
    {
        unitTable.Clear();
        if (units == null)
        {
            return;
        }

        int added = 0;
        for (int i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            if (unit == null || string.IsNullOrWhiteSpace(unit.Index))
            {
                continue;
            }

            unitTable[unit.Index] = unit;
            added++;
        }

        Debug.Log($"[CharactorManager] 플레이어 유닛 데이터 {added}건 캐시 (입력 행 {units.Count}).");
    }

    public UnitData GetCharactorData(string charactorId)
    {
        if (string.IsNullOrWhiteSpace(charactorId))
        {
            return null;
        }

        return unitTable.TryGetValue(charactorId, out var unit) ? unit : null;
    }

    public OwnedCharactorInfo GetOwnedCharactorInfo(string charactorId)
    {
        return ownedCharactors.Find(x => x.charactorId == charactorId);
    }

    public int GetCharactorLevel(string charactorId)
    {
        var info = GetOwnedCharactorInfo(charactorId);
        return info != null ? Mathf.Max(1, info.level) : 1;
    }

    public string GetSelectedCharactorId()
    {
        return selectedCharactorId;
    }

    public void SetSelectedCharactor(string charactorId)
    {
        selectedCharactorId = charactorId;
    }

    public void LevelUp(string charactorId, int amount = 1)
    {
        var info = GetOwnedCharactorInfo(charactorId);
        if (info == null)
        {
            return;
        }

        info.level = Mathf.Max(1, info.level + Mathf.Max(1, amount));
    }
}
