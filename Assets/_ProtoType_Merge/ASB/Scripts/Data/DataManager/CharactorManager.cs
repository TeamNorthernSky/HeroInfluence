using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 캐릭터 게임 상태 관리 (소유 여부, 레벨, 선택).
/// 마스터 데이터 조회는 DHCsvTemplateCatalog.Instance.TryGetPlayerTemplate() 을 사용합니다.
/// </summary>
public class CharactorManager : MonoBehaviour
{
    [System.Serializable]
    public class OwnedCharactorInfo
    {
        public string charactorId;
        public int level = 1;
        public bool isOwned = true;
    }

    [Header("Player Game State")]
    [SerializeField] private List<OwnedCharactorInfo> ownedCharactors = new List<OwnedCharactorInfo>();
    [SerializeField] private string selectedCharactorId;

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
        if (info == null) return;
        info.level = Mathf.Max(1, info.level + Mathf.Max(1, amount));
    }
}
