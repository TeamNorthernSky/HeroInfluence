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

    private readonly List<Vector2Int> revealBuffer = new List<Vector2Int>(81);
    private PartyGridMover subscribedMover;
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

    public int RevealRadius => revealRadius;

    public void SetRevealRadius(int radius)
    {
        revealRadius = Mathf.Max(0, radius);
    }

    [ContextMenu("Reveal Current Party Positions")]
    public void RevealAllCurrentPartyPositions()
    {
        if (ZoneEntryGuidanceController.SuppressGeneralFogReveal)
            return;

        if (fogGridManager == null || partyRegistry == null)
            return;

        PartyGridMover mover = partyRegistry.PlayerParty;
        if (mover != null)
            RevealAround(mover.GetCurrentGrid());
    }

    public void RevealAround(Vector2Int centerGrid)
    {
        if (ZoneEntryGuidanceController.SuppressGeneralFogReveal)
            return;

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

    // 반경 일반형 원형 마스크. revealRadius=4에서 종전 하드코드 제외 셀 (4,4)/(4,3)/(3,4)과 동일.
    private bool IsExcludedCornerOffset(int dx, int dy)
    {
        return dx * dx + dy * dy > revealRadius * (revealRadius + 1);
    }

    private void SubscribeToRegisteredParties()
    {
        if (partyRegistry == null)
            return;

        PartyGridMover mover = partyRegistry.PlayerParty;
        if (mover == null || subscribedMover == mover)
            return;

        UnsubscribeFromRegisteredParties();
        subscribedMover = mover;
        subscribedMover.GridEntered += HandlePartyGridEntered;
    }

    private void UnsubscribeFromRegisteredParties()
    {
        if (subscribedMover != null)
            subscribedMover.GridEntered -= HandlePartyGridEntered;

        subscribedMover = null;
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
