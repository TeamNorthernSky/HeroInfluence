using UnityEngine;

/// <summary>탐사 파티 카드의 Core icon 클릭을 해당 영웅의 코어 선택 모달로 연결한다.</summary>
[DisallowMultipleComponent]
public class ExplorationCoreSelectLauncher : MonoBehaviour
{
    [SerializeField] private ExplorationCoreSelectController modal;
    [Tooltip("ExplorationHeroBoxController와 동일한 파티 ID. 빈 값은 첫 파티를 사용한다.")]
    [SerializeField] private string targetPartyId = "";

    public void OpenSlot(int slotIndex)
    {
        if (modal == null || slotIndex < 0) return;
        var repo = PartyPersistentRepository.Instance;
        if (repo == null || repo.Parties.Count == 0) return;

        PartyPersistentData party = null;
        if (!string.IsNullOrWhiteSpace(targetPartyId))
            repo.TryGetParty(targetPartyId, out party);
        // 기존 ExplorationHeroBoxController의 기본 파티 선택 규칙과 일치시킨다.
        if (party == null) party = repo.Parties[0];
        if (party == null) return;

        var members = PartyFormation.OrderFrontFirst(party.UnitIndices, party.UnitSlots);
        if (slotIndex >= members.Count || members[slotIndex] <= 0) return;
        modal.Open(members[slotIndex]);
    }
}
