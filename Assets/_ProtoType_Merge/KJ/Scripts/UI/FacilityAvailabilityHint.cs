using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation for the build-mode hint. Does not change facility click routing.
public sealed class FacilityAvailabilityHint : MonoBehaviour
{
    public Image icon;
    public TMP_Text message;
    public Sprite canBuild, canUpgrade;
    private CanvasGroup group;
    private Button owner;
    private TMP_Text info, description;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        owner = transform.parent ? transform.parent.GetComponent<Button>() : null;
        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "Info") info = text;
            if (text.name == "Describtion") description = text;
        }
    }

    private void LateUpdate()
    {
        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            if (text != message) text.gameObject.SetActive(false);
        var gm = GameManager.Instance;
        HQDepartment dept = HQDepartment.Headquarters;
        bool found = owner != null && owner == LobbyUIRegistry.HqButton;
        if (!found)
            foreach (var facility in LobbyUIRegistry.Facilities)
                if (facility != null && (facility.LockedButton == owner || facility.UnlockedButton == owner))
                { dept = facility.Department; found = true; break; }
        bool eligible = found && gm != null && gm.HQ != null && gm.Economy != null && gm.HQ.CanUpgrade(dept);
        int level = eligible ? gm.HQ.GetLevel(dept) : 0;
        if (eligible)
            foreach (var cost in gm.HQ.GetUpgradeCost(dept, level))
                if (!gm.Economy.Has(cost.Key, cost.Value)) { eligible = false; break; }
        group.alpha = eligible ? 1f : 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        if (!eligible) return;
        icon.sprite = level == 0 ? canBuild : canUpgrade;
        message.text = (info != null ? info.text : "") + "\n" + (description != null ? description.text : "");
    }
}