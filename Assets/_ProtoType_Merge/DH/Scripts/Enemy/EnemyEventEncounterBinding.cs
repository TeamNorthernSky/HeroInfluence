using UnityEngine;

[DisallowMultipleComponent]
public class EnemyEventEncounterBinding : MonoBehaviour
{
    [SerializeField, Min(0)] private int encounterChatZoneId;
    [SerializeField, Min(0)] private int encounterChatId;
    [SerializeField] private string eventBattleKey;
    [SerializeField] private string sourcePlacementKey;

    public int EncounterChatZoneId => Mathf.Max(0, encounterChatZoneId);
    public int EncounterChatId => Mathf.Max(0, encounterChatId);
    public string EventBattleKey => NormalizeKey(eventBattleKey);
    public string SourcePlacementKey => MapProgressKey.NormalizeSegment(sourcePlacementKey);
    public bool HasEventEncounter => EncounterChatZoneId > 0 && EncounterChatId > 0;

    public void Initialize(
        int nextEncounterChatZoneId,
        int nextEncounterChatId,
        string nextEventBattleKey,
        string nextSourcePlacementKey)
    {
        encounterChatZoneId = Mathf.Max(0, nextEncounterChatZoneId);
        encounterChatId = Mathf.Max(0, nextEncounterChatId);
        eventBattleKey = NormalizeKey(nextEventBattleKey);
        sourcePlacementKey = MapProgressKey.NormalizeSegment(nextSourcePlacementKey);
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
