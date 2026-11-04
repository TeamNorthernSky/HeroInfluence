using UnityEngine;
using UnityEngine.UI;

/// <summary>탐사 파티 카드의 Core icon 클릭을 해당 영웅의 코어 선택 모달로 연결한다.</summary>
[DisallowMultipleComponent]
public class ExplorationCoreSelectLauncher : MonoBehaviour
{
    [SerializeField] private ExplorationCoreSelectController modal;
    [Tooltip("ExplorationHeroBoxController와 동일한 파티 ID. 빈 값은 첫 파티를 사용한다.")]
    [SerializeField] private string targetPartyId = "";
    [SerializeField] private Image[] equippedCoreIcons = new Image[4];

    private TutorialPartyComposition tutorialParty;
    private WorkshopManager workshop;
    private float nextRefresh;

    private void OnEnable() => RefreshIcons();

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.3f;
        RefreshIcons();
    }

    private void OnDisable()
    {
        if (workshop != null) workshop.OnStateChanged -= RefreshIcons;
        workshop = null;
    }

    private PartyPersistentData ResolveParty()
    {
        var repo = PartyPersistentRepository.Instance;
        if (repo == null || repo.Parties.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(targetPartyId) && repo.TryGetParty(targetPartyId, out var party))
            return party;
        return repo.Parties[0];
    }

    public void RefreshIcons()
    {
        if (gameObject.scene.name == "TutorialExploreScene")
        {
            if (tutorialParty == null) tutorialParty = FindFirstObjectByType<TutorialPartyComposition>();
            var keys = tutorialParty != null ? tutorialParty.GetJoinedUnitTemplateKeys() : null;
            var catalog = TutorialCatalog.Instance;
            for (int i = 0; i < equippedCoreIcons.Length; i++)
            {
                int core = 0;
                if (catalog != null && keys != null && i < keys.Count && catalog.TryGetPlayerUnitTemplate(keys[i], out var template))
                {
                    TutorialCombatUnitFactory.ResolveDefaultWeapon(catalog, template, out var weapon);
                    if (weapon != null) core = weapon.NumericWeaponId;
                }
                SetIcon(equippedCoreIcons[i], core);
            }
            return;
        }
        var current = GameManager.Instance != null ? GameManager.Instance.Workshop : null;
        if (workshop != current)
        {
            if (workshop != null) workshop.OnStateChanged -= RefreshIcons;
            workshop = current;
            if (workshop != null) workshop.OnStateChanged += RefreshIcons;
        }

        var party = ResolveParty();
        var members = party != null ? PartyFormation.OrderFrontFirst(party.UnitIndices, party.UnitSlots) : null;
        for (int i = 0; i < equippedCoreIcons.Length; i++)
        {
            var icon = equippedCoreIcons[i];
            if (icon == null) continue;
            int core = workshop != null && members != null && i < members.Count && members[i] > 0
                ? workshop.GetEquippedWeaponIndex(members[i]) : 0;
            SetIcon(icon, core);
        }
    }

    private void SetIcon(Image icon, int core)
    {
        if (icon == null) return;
        icon.overrideSprite = null;
        icon.sprite = modal != null ? modal.GetCoreIcon(core) : null;
        icon.enabled = true;
        icon.color = icon.sprite != null ? Color.white : Color.clear;
        icon.preserveAspect = true;
    }

    public void OpenSlot(int slotIndex)
    {
        if (gameObject.scene.name == "TutorialExploreScene" || modal == null || slotIndex < 0) return;
        PartyPersistentData party = ResolveParty();
        if (party == null) return;

        var members = PartyFormation.OrderFrontFirst(party.UnitIndices, party.UnitSlots);
        if (slotIndex >= members.Count || members[slotIndex] <= 0) return;
        modal.Open(members[slotIndex]);
    }
}
