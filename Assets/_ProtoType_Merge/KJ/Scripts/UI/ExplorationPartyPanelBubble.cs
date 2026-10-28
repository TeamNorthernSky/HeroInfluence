using System.Reflection;
using UnityEngine;

/// <summary>Reads the HUD's actual unit bindings; never guesses by party or slot order.</summary>
public sealed class ExplorationPartyPanelBubble
{
    private static readonly FieldInfo UnitsField = typeof(ExplorationHeroBoxController)
        .GetField("boundUnits", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo SlotsField = typeof(ExplorationHeroBoxController)
        .GetField("slots", BindingFlags.Instance | BindingFlags.NonPublic);

    private ExplorationHeroBoxController owner;
    private int slotIndex;
    private ExplorationActionPointBubble bubble;
    private RectTransform profile;

    public bool IsShowing => bubble != null && bubble.gameObject.activeSelf;
    public float TotalDuration => bubble != null ? bubble.TotalDuration : 0f;
    public int UnitIndex { get; private set; }

    public bool HasBinding(int unitIndex)
    {
        if (owner == null || !owner.isActiveAndEnabled || profile == null || !profile.gameObject.activeInHierarchy ||
            UnitsField == null || SlotsField == null) return false;
        var units = UnitsField.GetValue(owner) as int[];
        var slots = SlotsField.GetValue(owner) as ExplorationHeroBoxController.HeroSlot[];
        return units != null && slotIndex < units.Length && units[slotIndex] == unitIndex &&
            slots != null && slotIndex < slots.Length && slots[slotIndex]?.root != null &&
            slots[slotIndex].root.transform == profile;
    }

    /// <summary>unitIndex is the character identity, not a HUD slot number.</summary>
    public bool ShowForUnit(int unitIndex, ExplorationActionPointBubble prefab, string message = null)
    {
        Hide();
        if (unitIndex <= 0 || prefab == null || !ResolveProfile(unitIndex)) return false;
        if (bubble == null)
        {
            bubble = Object.Instantiate(prefab);
            bubble.name = "PartyPanelMovementBubble";
            if (Application.isPlaying) Object.DontDestroyOnLoad(bubble.gameObject);
        }
        UnitIndex = unitIndex;
        bubble.PresentBesideProfile(profile, message);
        return IsShowing;
    }

    private bool ResolveProfile(int unitIndex)
    {
        if (HasBinding(unitIndex)) return true;
        owner = null;
        profile = null;
        if (UnitsField == null || SlotsField == null) return false;
        foreach (var candidate in Object.FindObjectsByType<ExplorationHeroBoxController>(FindObjectsSortMode.None))
        {
            if (!candidate.isActiveAndEnabled) continue;
            var units = UnitsField.GetValue(candidate) as int[];
            var slots = SlotsField.GetValue(candidate) as ExplorationHeroBoxController.HeroSlot[];
            if (units == null || slots == null) continue;
            for (int i = 0; i < units.Length && i < slots.Length; i++)
            {
                var root = slots[i]?.root;
                if (units[i] != unitIndex || root == null || !root.activeInHierarchy ||
                    !root.name.StartsWith("BTN_Explore_Profile", System.StringComparison.Ordinal)) continue;
                owner = candidate;
                slotIndex = i;
                profile = root.transform as RectTransform;
                return profile != null;
            }
        }
        return false;
    }

    public void Hide()
    {
        if (bubble != null) bubble.Hide();
        UnitIndex = 0;
    }

    public void Dispose()
    {
        if (bubble != null)
        {
            if (Application.isPlaying) Object.Destroy(bubble.gameObject);
            else Object.DestroyImmediate(bubble.gameObject);
        }
        bubble = null;
        owner = null;
        profile = null;
    }
}
