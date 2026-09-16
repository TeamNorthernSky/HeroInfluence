using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Keeps the lobby roster's IP and core display bound to the existing public unit ID.
[DisallowMultipleComponent]
public sealed class AssociationRosterDetails : MonoBehaviour
{
    [SerializeField] private HeroProfileButton owner;
    [SerializeField] private TMP_Text influence;
    [SerializeField] private GameObject maximum;
    [SerializeField] private Image core;
    [SerializeField] private Sprite[] coreSprites;

    private void LateUpdate()
    {
        var repo = PersistentUnitRepository.Instance;
        if (owner == null || repo == null || !repo.TryGetUnit(owner.UnitIndex, out var unit) || unit == null)
        {
            influence.text = "—";
            maximum.SetActive(false);
            core.enabled = false;
            return;
        }
        influence.text = Mathf.FloorToInt(unit.CurrentInfluence).ToString();
        maximum.SetActive(unit.IngameStats.Influence > 0 && unit.CurrentInfluence >= unit.IngameStats.Influence);
        var workshop = GameManager.Instance != null ? GameManager.Instance.Workshop : null;
        int id = workshop != null ? workshop.GetEquippedWeaponIndex(owner.UnitIndex) : 0;
        core.sprite = id > 0 && coreSprites != null && id <= coreSprites.Length ? coreSprites[id - 1] : null;
        core.enabled = core.sprite != null;
    }
}