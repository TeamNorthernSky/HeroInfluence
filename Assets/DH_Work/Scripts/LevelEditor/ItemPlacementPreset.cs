using UnityEngine;

[CreateAssetMenu(
    fileName = "ItemPlacementPreset",
    menuName = "DH Work/Level Editor/Item Placement Preset")]
public class ItemPlacementPreset : ScriptableObject
{
    [SerializeField] private ResourceType resourceType = ResourceType.Money;
    [SerializeField] private int amount = 10;

    public ResourceType ResourceType => resourceType;
    public int Amount => amount;
}
