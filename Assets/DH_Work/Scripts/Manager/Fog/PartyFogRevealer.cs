using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PartyFogRevealer : MonoBehaviour
{
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private FogGridManager fogGridManager;
    [SerializeField, Min(0)] private int revealRadius = 4;
    [SerializeField] private bool useRoundedMask = true;
    [SerializeField] private bool revealCurrentPositionsOnEnable = true;

    private readonly HashSet<PartyGridMover> subscribedMovers = new HashSet<PartyGridMover>();
    private readonly List<Vector2Int> revealBuffer = new List<Vector2Int>(81);
    private Coroutine initialRevealCoroutine;

    private void OnEnable()
    {
        SubscribeToRegisteredParties();

        if (revealCurrentPositionsOnEnable)
            QueueInitialReveal();
    }

    private void OnDisable()
    {
        if (initialRevealCoroutine != null)
        {
            StopCoroutine(initialRevealCoroutine);
            initialRevealCoroutine = null;
        }

        UnsubscribeFromRegisteredParties();
    }

    [ContextMenu("Reveal Current Party Positions")]
    public void RevealAllCurrentPartyPositions()
    {
        if (fogGridManager == null || partyRegistry == null)
            return;

        PartyGridMover[] movers = partyRegistry.PartyMovers;
        for (int i = 0; i < movers.Length; i++)
        {
            PartyGridMover mover = movers[i];
            if (mover == null)
                continue;

            RevealAround(mover.GetCurrentGrid());
        }
    }

    public void RevealAround(Vector2Int centerGrid)
    {
        if (fogGridManager == null)
            return;

        revealBuffer.Clear();

        for (int dx = -revealRadius; dx <= revealRadius; dx++)
        {
            for (int dy = -revealRadius; dy <= revealRadius; dy++)
            {
                if (useRoundedMask && IsExcludedCornerOffset(dx, dy))
                    continue;

                revealBuffer.Add(centerGrid + new Vector2Int(dx, dy));
            }
        }

        fogGridManager.RevealCells(revealBuffer);
    }

    private bool IsExcludedCornerOffset(int dx, int dy)
    {
        if (revealRadius != 4)
            return false;

        int absX = Mathf.Abs(dx);
        int absY = Mathf.Abs(dy);

        return (absX == 4 && absY == 4)
            || (absX == 4 && absY == 3)
            || (absX == 3 && absY == 4);
    }

    private void SubscribeToRegisteredParties()
    {
        if (partyRegistry == null)
            return;

        PartyGridMover[] movers = partyRegistry.PartyMovers;
        for (int i = 0; i < movers.Length; i++)
        {
            PartyGridMover mover = movers[i];
            if (mover == null || subscribedMovers.Contains(mover))
                continue;

            mover.GridEntered += HandlePartyGridEntered;
            subscribedMovers.Add(mover);
        }
    }

    private void UnsubscribeFromRegisteredParties()
    {
        foreach (PartyGridMover mover in subscribedMovers)
        {
            if (mover == null)
                continue;

            mover.GridEntered -= HandlePartyGridEntered;
        }

        subscribedMovers.Clear();
    }

    private void HandlePartyGridEntered(Vector2Int currentGrid)
    {
        if (fogGridManager == null)
            return;

        RevealAround(currentGrid);
    }

    private void QueueInitialReveal()
    {
        if (!Application.isPlaying)
        {
            RevealAllCurrentPartyPositions();
            return;
        }

        if (initialRevealCoroutine != null)
            StopCoroutine(initialRevealCoroutine);

        initialRevealCoroutine = StartCoroutine(RevealCurrentPositionsNextFrame());
    }

    private IEnumerator RevealCurrentPositionsNextFrame()
    {
        yield return null;
        initialRevealCoroutine = null;
        RevealAllCurrentPartyPositions();
    }
}
