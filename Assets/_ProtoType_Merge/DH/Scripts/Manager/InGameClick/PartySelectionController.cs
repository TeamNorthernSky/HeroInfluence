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
        PartyGridMover mover = GetPlayerParty();
        if (CanSelectMover(mover))
            SetActiveMoverInternal(mover, false);
    }

    public void Dispose()
    {
        UnsubscribeFromActiveMover();
    }

    public void ClearActiveMover()
    {
        if (activeMover == null)
            return;

        UnsubscribeFromActiveMover();
        activeMover = null;
        ActiveMoverChanged?.Invoke(null);
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
            if (!CanSelectMover(mover))
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
        return mover != null && partyRegistry != null && partyRegistry.PlayerParty == mover;
    }

    private bool CanSelectMover(PartyGridMover mover)
    {
        return mover != null
            && mover.gameObject.activeInHierarchy
            && !DefeatedPartyReturnController.IsPartyWaiting(mover)
            && IsRegisteredMover(mover);
    }

    private PartyGridMover GetPlayerParty()
    {
        return partyRegistry != null ? partyRegistry.PlayerParty : null;
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
