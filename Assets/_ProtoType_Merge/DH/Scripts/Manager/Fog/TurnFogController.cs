using UnityEngine;
using UnityEngine.Serialization;

public class TurnFogController : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private FogGridManager fogGridManager;
    [SerializeField] private PartyFogRevealer partyFogRevealer;
    [FormerlySerializedAs("mineFogRevealer")]
    [SerializeField] private OutpostFogRevealer outpostFogRevealer;
    [SerializeField] private CastleFogRevealer castleFogRevealer;

    private void OnEnable()
    {
        if (turnManager != null)
            turnManager.DayAdvanced += HandleDayAdvanced;

        SyncCurrentDay();
    }

    private void OnDisable()
    {
        if (turnManager != null)
            turnManager.DayAdvanced -= HandleDayAdvanced;
    }

    [ContextMenu("Sync Fog Day")]
    public void SyncCurrentDay()
    {
        if (turnManager == null || fogGridManager == null)
            return;

        fogGridManager.SetCurrentDay(turnManager.GetDay());
    }

    private void HandleDayAdvanced(int currentDay)
    {
        if (fogGridManager == null)
            return;

        fogGridManager.SetCurrentDay(currentDay);
        fogGridManager.ApplyDayProgression();
        partyFogRevealer?.RevealAllCurrentPartyPositions();
        outpostFogRevealer?.RevealAllClaimedOutposts();
        castleFogRevealer?.RevealAllCastles();
    }
}
