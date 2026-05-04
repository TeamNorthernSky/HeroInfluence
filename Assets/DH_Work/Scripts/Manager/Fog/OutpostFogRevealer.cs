using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class OutpostFogRevealer : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;
    [SerializeField] private FogGridManager fogGridManager;
    [FormerlySerializedAs("mineRegistry")]
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField, Min(0)] private int revealRadius = 1;
    [FormerlySerializedAs("revealClaimedMinesOnEnable")]
    [SerializeField] private bool revealClaimedOutpostsOnEnable = true;

    private void OnEnable()
    {
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

            RevealOutpostArea(outpost);
        }
    }

    private void HandleOutpostClaimed(Outpost outpost)
    {
        if (outpost == null)
            return;

        RevealOutpostArea(outpost);
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
        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();
    }
}
