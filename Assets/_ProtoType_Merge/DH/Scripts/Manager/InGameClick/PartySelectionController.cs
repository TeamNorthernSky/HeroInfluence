using System;
using System.Collections.Generic;
using UnityEngine;

public class PartySelectionController
{
    private readonly PartyRegistry partyRegistry;
    private readonly QuarterViewCameraFollower cameraFollower;
    private readonly float rayDistance;

    private PartyGridMover activeMover;

    public event Action<PartyGridMover> ActiveMoverChanged;
    public event Action<List<Vector2Int>> ActiveMoverPathUpdated;
    public event Action ActiveMoverMoveCompleted;

    public PartyGridMover ActiveMover => activeMover;

    public PartySelectionController(PartyRegistry partyRegistry, QuarterViewCameraFollower cameraFollower, float rayDistance)
    {
        this.partyRegistry = partyRegistry;
        this.cameraFollower = cameraFollower;
        this.rayDistance = rayDistance;
    }

    public void Initialize()
    {
        PartyGridMover[] partyMovers = GetPartyMovers();
        if (partyMovers.Length == 0)
            return;

        SetActiveMoverInternal(partyMovers[0], false);
    }

    public void Dispose()
    {
        UnsubscribeFromActiveMover();
    }

    public bool TryHandleSelectionClick(Ray ray)
    {
        var hits = Physics.RaycastAll(ray, rayDistance);
        float bestDist = float.PositiveInfinity;
        PartyGridMover clickedMover = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null)
                continue;

            PartyGridMover mover = hitTransform.GetComponentInParent<PartyGridMover>();
            if (mover == null || !IsRegisteredMover(mover))
                continue;

            if (hits[i].distance < bestDist)
            {
                bestDist = hits[i].distance;
                clickedMover = mover;
            }
        }

        if (clickedMover == null)
            return false;

        SetActiveMoverInternal(clickedMover, true);
        return true;
    }

    public void FocusActiveMover()
    {
        if (cameraFollower == null || activeMover == null)
            return;

        cameraFollower.SetFollowTarget(activeMover.transform);
        cameraFollower.RecenterOnFollowTarget();
        cameraFollower.SetFollowEnabled(true);
    }

    private bool IsRegisteredMover(PartyGridMover mover)
    {
        PartyGridMover[] partyMovers = GetPartyMovers();
        for (int i = 0; i < partyMovers.Length; i++)
        {
            if (partyMovers[i] == mover)
                return true;
        }

        return false;
    }

    private PartyGridMover[] GetPartyMovers()
    {
        return partyRegistry != null ? partyRegistry.PartyMovers : Array.Empty<PartyGridMover>();
    }

    private void SetActiveMoverInternal(PartyGridMover mover, bool notifyChange)
    {
        if (mover == null || mover == activeMover)
            return;

        UnsubscribeFromActiveMover();

        activeMover = mover;
        activeMover.PathUpdated += HandlePathUpdated;
        activeMover.MoveCompleted += HandleMoveCompleted;

        if (cameraFollower != null)
        {
            cameraFollower.SetFollowTarget(activeMover.transform);
            cameraFollower.RecenterOnFollowTarget();
        }

        if (notifyChange)
            ActiveMoverChanged?.Invoke(activeMover);
    }

    private void UnsubscribeFromActiveMover()
    {
        if (activeMover == null)
            return;

        activeMover.PathUpdated -= HandlePathUpdated;
        activeMover.MoveCompleted -= HandleMoveCompleted;
    }

    private void HandlePathUpdated(List<Vector2Int> remainingPath)
    {
        ActiveMoverPathUpdated?.Invoke(remainingPath);
    }

    private void HandleMoveCompleted()
    {
        ActiveMoverMoveCompleted?.Invoke();
    }
}
