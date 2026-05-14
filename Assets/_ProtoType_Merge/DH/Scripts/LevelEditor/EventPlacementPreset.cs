using UnityEngine;

[CreateAssetMenu(
    fileName = "EventPlacementPreset",
    menuName = "DH Work/Level Editor/Event Placement Preset")]
public class EventPlacementPreset : ScriptableObject
{
    [SerializeField] private string eventKey = "event_001";

    public string EventKey => eventKey;
}
