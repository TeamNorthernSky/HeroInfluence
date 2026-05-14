using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "OutpostPlacementPreset",
    menuName = "DH Work/Level Editor/Outpost Placement Preset")]
public class OutpostPlacementPreset : ScriptableObject
{
    [FormerlySerializedAs("resourceType")]
    [SerializeField] private OutpostType outpostType = OutpostType.Bank;
    [SerializeField] private int resourcePerTurn = 5;
    [SerializeField] private OutpostState initialState = OutpostState.Unclaimed;

    public OutpostType OutpostType => OutpostTypeUtility.Normalize(outpostType);
    public int ResourcePerTurn => resourcePerTurn;
    public OutpostState InitialState => initialState;

    private void OnValidate()
    {
        outpostType = OutpostTypeUtility.Normalize(outpostType);
        resourcePerTurn = Mathf.Max(1, resourcePerTurn);
    }
}
