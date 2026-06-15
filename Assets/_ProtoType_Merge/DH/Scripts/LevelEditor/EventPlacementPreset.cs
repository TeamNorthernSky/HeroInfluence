using UnityEngine;

[CreateAssetMenu(
    fileName = "EventPlacementPreset",
    menuName = "DH Work/Level Editor/Event Placement Preset")]
public class EventPlacementPreset : ScriptableObject
{
    [SerializeField] private MapEventType eventType = MapEventType.TrainingHp;
    [SerializeField, Min(1)] private int requireAmount = 100;
    [SerializeField, Min(0)] private int effectAmount = 3;

    public MapEventType EventType => eventType;
    public string EventKey => MapEventTypeUtility.ToEventKey(eventType);
    public int RequireAmount => Mathf.Max(1, requireAmount);
    public int EffectAmount => Mathf.Max(0, effectAmount);

    private void OnValidate()
    {
        requireAmount = Mathf.Max(1, requireAmount);
        effectAmount = Mathf.Max(0, effectAmount);
    }
}
