using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class OutpostFogRevealer : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;
    [SerializeField] private FogGridManager fogGridManager;
    [SerializeField] private LevelZoneLayoutLoader levelZoneLayoutLoader;
    [FormerlySerializedAs("mineRegistry")]
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField, Min(0)] private int revealRadius = 1;
    [SerializeField] private bool fogZoneOnClaim = true;
    [FormerlySerializedAs("revealClaimedMinesOnEnable")]
    [SerializeField] private bool revealClaimedOutpostsOnEnable = true;

    private void OnEnable()
    {
        AutoAssignReferences();
        Outpost.OutpostClaimed += HandleOutpostClaimed;

        if (revealClaimedOutpostsOnEnable)
            RevealAllClaimedOutposts();
    }

    private void OnDisable()
    {
        Outpost.OutpostClaimed -= HandleOutpostClaimed;
    }

    [ContextMenu("Reveal Claimed Outposts")]
    public void RevealAllClaimedOutposts()
    {
        if (gridManager == null || fogGridManager == null || outpostRegistry == null)
            return;

        IReadOnlyList<Outpost> outposts = outpostRegistry.Outposts;
        for (int i = 0; i < outposts.Count; i++)
        {
            Outpost outpost = outposts[i];
            if (outpost == null || outpost.outpostState != OutpostState.Claimed)
                continue;

            FogOutpostZone(outpost);
            RevealOutpostArea(outpost);
        }
    }

    private void HandleOutpostClaimed(Outpost outpost)
    {
        if (outpost == null)
            return;

        FogOutpostZone(outpost);
        RevealOutpostArea(outpost);
    }

    private void FogOutpostZone(Outpost outpost)
    {
        if (!fogZoneOnClaim || fogGridManager == null || levelZoneLayoutLoader == null)
            return;

        string zoneId = NormalizeZoneId(outpost.ZoneId);
        if (string.IsNullOrWhiteSpace(zoneId))
            return;

        IReadOnlyList<LoadedLevelZoneData> zones = levelZoneLayoutLoader.LoadedZones;
        for (int i = 0; i < zones.Count; i++)
        {
            LoadedLevelZoneData zone = zones[i];
            if (!string.Equals(NormalizeZoneId(zone.ZoneId), zoneId, System.StringComparison.Ordinal))
                continue;

            fogGridManager.MarkUnexploredCellsAsFogged(new RectInt(zone.Anchor, zone.Size));
            return;
        }
    }

    private void RevealOutpostArea(Outpost outpost)
    {
        if (gridManager == null || fogGridManager == null)
            return;

        Vector2Int outpostGrid = outpost.GetAnchorGrid(gridManager);
        fogGridManager.RevealArea(outpostGrid, revealRadius);
    }

    private void OnValidate()
    {
        AutoAssignReferences();
    }

    private void AutoAssignReferences()
    {
        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();

        if (levelZoneLayoutLoader == null)
            levelZoneLayoutLoader = FindFirstObjectByType<LevelZoneLayoutLoader>();
    }

    private static string NormalizeZoneId(string value)
    {
        return MapProgressKey.NormalizeSegment(value);
    }
}
