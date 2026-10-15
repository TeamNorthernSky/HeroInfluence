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

    private void Awake() => ResolveReferences();

    private void ResolveReferences()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        owner = GetComponentInParent<Button>();
        if (icon == null) icon = transform.Find("AvailabilityIcon")?.GetComponent<Image>();
        if (message == null) message = transform.Find("AvailabilityMessage")?.GetComponent<TMP_Text>();
        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == "Info") info = text;
            if (text.name == "Describtion") description = text;
        }
    }

    private void LateUpdate()
    {
        ResolveReferences();
        foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            if (text != message) text.gameObject.SetActive(false);
        var gm = GameManager.Instance;
        HQDepartment dept = HQDepartment.Headquarters;
        bool found = owner != null && owner == LobbyUIRegistry.HqButton;
        if (!found)
            foreach (var facility in LobbyUIRegistry.Facilities)
                if (facility != null && (facility.LockedButton == owner || facility.UnlockedButton == owner))
                { dept = facility.Department; found = true; break; }
        int level = found && gm != null && gm.HQ != null ? gm.HQ.GetLevel(dept) : 0;
        // Match the room highlight/click rules; affordability is checked by the confirmation modal.
        bool eligible = found && (level == 0
            ? HQBuildModeController.IsBuildable(dept)
            : HQBuildModeController.IsUpgradeable(dept));
        Render(eligible, level);
    }

    private void Render(bool eligible, int level)
    {
        group.alpha = eligible ? 1f : 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        if (icon != null)
        {
            icon.gameObject.SetActive(eligible);
            icon.enabled = eligible;
            icon.sprite = level == 0 ? canBuild : canUpgrade;
        }
        if (message != null)
        {
            message.gameObject.SetActive(eligible);
            message.enabled = eligible;
            string title = info != null ? info.text : "";
            if (string.IsNullOrWhiteSpace(title)) title = level == 0 ? "건설 가능" : "업그레이드 가능";
            string body = description != null ? description.text : "";
            message.text = string.IsNullOrWhiteSpace(body) ? title : title + "\n" + body;
        }
    }
}
