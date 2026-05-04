using UnityEngine;

[CreateAssetMenu(
    fileName = "OutpostPlacementPreset",
    menuName = "DH Work/Level Editor/Outpost Placement Preset")]
public class OutpostPlacementPreset : ScriptableObject
{
    [SerializeField] private ResourceType resourceType = ResourceType.Supply;
    [SerializeField] private int resourcePerTurn = 5;
    [SerializeField] private OutpostState initialState = OutpostState.Unclaimed;

    public ResourceType ResourceType => resourceType;
    public int ResourcePerTurn => resourcePerTurn;
    public OutpostState InitialState => initialState;
}
