using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentData", menuName = "ASB/Data/Equipment Data")]
public class EquipmentData : ScriptableObject
{
    [SerializeField] private string equipmentId;
    [SerializeField] private string displayName;
    [SerializeField] private StatBlock statBonus = new StatBlock(0, 0, 0, 0, 1f, 0.01f, 0.01f, 0f);

    public string EquipmentId => equipmentId;
    public string DisplayName => displayName;
    public StatBlock StatBonus => statBonus;
}
