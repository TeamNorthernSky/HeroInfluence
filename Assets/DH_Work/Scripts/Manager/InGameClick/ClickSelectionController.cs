using UnityEngine;
using UnityEngine.EventSystems;

public class ClickSelectionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private AStarPathfinder pathfinder;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private Transform marker;

    [Header("Raycast")]
    [SerializeField] private LayerMask landLayerMask = ~0;
    [SerializeField] private float rayDistance = 500f;

    [Header("Camera (Quarter Follow on Move)")]
    [SerializeField] private QuarterViewCameraFollower cameraFollower;

    [Header("Path Preview")]
    [SerializeField] private PathPreviewRenderer pathPreviewRenderer;

    [Header("UI Input")]
    [SerializeField] private UIInputBlocker uiInputBlocker;

    [Header("Marker Visual")]
    [SerializeField] private Color validMarkerColor = Color.green;
    [SerializeField] private Color invalidMarkerColor = Color.red;

    private PartySelectionController partySelectionController;
    private MoveCommandPreviewController moveCommandPreviewController;

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (cameraFollower == null && mainCamera != null)
            cameraFollower = mainCamera.GetComponent<QuarterViewCameraFollower>();

        partySelectionController = new PartySelectionController(partyRegistry, cameraFollower, rayDistance);
        moveCommandPreviewController = new MoveCommandPreviewController(
            gridManager,
            pathfinder,
            marker,
            pathPreviewRenderer,
            validMarkerColor,
            invalidMarkerColor,
            rayDistance);

        partySelectionController.ActiveMoverChanged += HandleActiveMoverChanged;
        partySelectionController.ActiveMoverPathUpdated += HandleActiveMoverPathUpdated;
        partySelectionController.ActiveMoverMoveCompleted += HandleActiveMoverMoveCompleted;

        if (turnManager != null)
            turnManager.EnemyTurnStateChanged += HandleEnemyTurnStateChanged;

        partySelectionController.Initialize();
        moveCommandPreviewController.Initialize();
    }

    private void OnDestroy()
    {
        if (partySelectionController != null)
        {
            partySelectionController.ActiveMoverChanged -= HandleActiveMoverChanged;
            partySelectionController.ActiveMoverPathUpdated -= HandleActiveMoverPathUpdated;
            partySelectionController.ActiveMoverMoveCompleted -= HandleActiveMoverMoveCompleted;
            partySelectionController.Dispose();
        }

        if (turnManager != null)
            turnManager.EnemyTurnStateChanged -= HandleEnemyTurnStateChanged;
    }

    private void Update()
    {
        RefreshUILockState();

        if (Input.GetMouseButtonDown(0))
            TryHandleClick();

        PartyGridMover activeMover = partySelectionController != null ? partySelectionController.ActiveMover : null;
        if (activeMover != null && activeMover.IsMoving)
            moveCommandPreviewController?.UpdateRealtimePathPreview(activeMover);
    }

    private void TryHandleClick()
    {
        PartyGridMover activeMover = partySelectionController != null ? partySelectionController.ActiveMover : null;
        PartyRuntime activeRuntime = activeMover != null ? activeMover.GetComponent<PartyRuntime>() : null;
        if (mainCamera == null || gridManager == null || activeMover == null || pathfinder == null || marker == null)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (turnManager != null && turnManager.IsEnemyTurnRunning)
            return;

        if (activeMover.IsMoving || (activeRuntime != null && activeRuntime.IsInputLocked))
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (partySelectionController.TryHandleSelectionClick(ray))
            return;

        if (moveCommandPreviewController.TryConfirmMove(ray, activeMover))
        {
            partySelectionController.FocusActiveMover();
            return;
        }

        if (!TryGetClosestLandHit(ray, out RaycastHit landHit))
            return;

        Vector2Int clickedGrid = gridManager.WorldToGrid(landHit.point);
        if (gridManager.HasObstacle(clickedGrid))
            return;

        if (gridManager.HasOtherPlayer(clickedGrid, activeMover.transform))
            return;

        if (!gridManager.IsVisibleCell(clickedGrid))
            return;

        moveCommandPreviewController.PreviewMoveToGrid(activeMover, clickedGrid);
    }

    private bool TryGetClosestLandHit(Ray ray, out RaycastHit landHit)
    {
        landHit = default;
        Transform groundTransform = gridManager.GroundRaycastTransform;
        if (groundTransform == null)
            return false;

        float bestDist = float.PositiveInfinity;
        bool found = false;

        var hits = Physics.RaycastAll(ray, rayDistance, landLayerMask);
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            var t = h.transform;
            if (marker != null && (t == marker || t.IsChildOf(marker)))
                continue;

            if (t == groundTransform || (t != null && t.IsChildOf(groundTransform)))
            {
                if (h.distance < bestDist)
                {
                    bestDist = h.distance;
                    landHit = h;
                    found = true;
                }
            }
        }

        return found;
    }

    private void HandleActiveMoverChanged(PartyGridMover _)
    {
        moveCommandPreviewController?.ClearPreview();
    }

    private void HandleActiveMoverPathUpdated(System.Collections.Generic.List<Vector2Int> remainingPath)
    {
        moveCommandPreviewController?.HandlePathUpdated(remainingPath);
    }

    private void HandleActiveMoverMoveCompleted()
    {
        moveCommandPreviewController?.HandleMoveCompleted();
    }

    private void HandleEnemyTurnStateChanged(bool isEnemyTurnRunning)
    {
        if (!isEnemyTurnRunning)
            return;

        moveCommandPreviewController?.ClearPreview();
    }

    private void RefreshUILockState()
    {
        if (uiInputBlocker == null)
            return;

        PartyGridMover activeMover = partySelectionController != null ? partySelectionController.ActiveMover : null;
        PartyRuntime activeRuntime = activeMover != null ? activeMover.GetComponent<PartyRuntime>() : null;
        bool shouldLockUI =
            (turnManager != null && turnManager.IsEnemyTurnRunning) ||
            (activeMover != null && (activeMover.IsMoving || (activeRuntime != null && activeRuntime.IsInputLocked)));
        uiInputBlocker.SetLocked(shouldLockUI);
    }
}
