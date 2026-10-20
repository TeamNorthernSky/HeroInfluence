using System.Reflection;
using UnityEngine;

/// <summary>Opens the exploration core picker for the hero shown on this license.</summary>
[DisallowMultipleComponent]
public sealed class HeroLicenseCoreSelectLauncher : MonoBehaviour
{
    [SerializeField] private HeroInfoModal owner;
    [SerializeField] private ExplorationCoreSelectController coreSelectionPrefab;
    private ExplorationCoreSelectController instance;
    private static readonly FieldInfo UnitIndexField = typeof(HeroInfoModal).GetField(
        "currentUnitIndex", BindingFlags.Instance | BindingFlags.NonPublic);

    public void OpenCoreSelection()
    {
        if (owner == null || coreSelectionPrefab == null || UnitIndexField == null) return;
        int unitIndex = (int)UnitIndexField.GetValue(owner);
        var units = PersistentUnitRepository.Instance;
        if (unitIndex <= 0 || units == null || !units.TryGetUnit(unitIndex, out var unit) || unit == null) return;
        // The license owns a Canvas in both scenes. Keep the picker inside it,
        // outside the animated Panel, so it also works on the persistent UI.
        if (instance == null) instance = Instantiate(coreSelectionPrefab, transform, false);
        instance.transform.SetAsLastSibling();
        instance.Open(unitIndex);
    }

    private void OnDisable()
    {
        if (instance != null) instance.Close();
    }
}
