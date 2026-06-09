using UnityEngine;

public class TargetSelectableVisual : MonoBehaviour, ITargetSelectableVisual
{
    [SerializeField] private BattleCharactor owner;

    [Header("Optional")]
    [SerializeField] private GameObject selectableMarker;
    [SerializeField] private GameObject hoveredMarker;

    public BattleCharactor Owner => owner;

    private void OnEnable()
    {
        if (owner == null)
            owner = GetComponentInParent<BattleCharactor>();
        TargetingVisualRegistry.Register(this);
    }

    private void OnDisable()
    {
        TargetingVisualRegistry.Unregister(this);
    }

    public void Bind(BattleCharactor newOwner)
    {
        TargetingVisualRegistry.Unregister(this);
        owner = newOwner;
        if (isActiveAndEnabled)
            TargetingVisualRegistry.Register(this);
    }

    public void SetSelectable(bool active)
    {
        if (selectableMarker != null)
            selectableMarker.SetActive(active);
    }

    public void SetHovered(bool active)
    {
        if (hoveredMarker != null)
            hoveredMarker.SetActive(active);
    }

    public void Clear()
    {
        SetSelectable(false);
        SetHovered(false);
    }
}
