using System.Collections;
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
    [SerializeField, Tooltip("미완료 구역 진입 시 다음 실제 이동 목표에 표시하는 튜토리얼 화살표 셰이더입니다. 씬 참조로 빌드에 포함하며, 비워두면 노란 경로와 화살표 안내를 표시하지 않습니다.")]
    private Shader zoneGuidanceArrowShader;
    [SerializeField] private MoveRangeController moveRangeController;

    [Header("UI Input")]
    [SerializeField] private UIInputBlocker uiInputBlocker;

    [Header("Marker Visual")]
    [SerializeField] private Color validMarkerColor = Color.green;
    [SerializeField] private Color invalidMarkerColor = Color.red;

    private PartySelectionController partySelectionController;
    private MoveCommandPreviewController moveCommandPreviewController;
    private Coroutine pendingMoveRangeRefreshCoroutine;
    private RaycastHit[] pointerHits = new RaycastHit[64];
    private readonly System.Collections.Generic.List<Vector2Int> hoverInteractionCells =
        new System.Collections.Generic.List<Vector2Int>(32);
    private readonly System.Collections.Generic.List<RaycastResult> hoverUIHits = new System.Collections.Generic.List<RaycastResult>();
    private PointerEventData hoverPointer;
    private EventSystem hoverEventSystem;
    public static ClickSelectionController Active { get; private set; }

    private void OnEnable() => Active = this;

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (cameraFollower == null && mainCamera != null)
            cameraFollower = mainCamera.GetComponent<QuarterViewCameraFollower>();

        if (moveRangeController == null)
            moveRangeController = FindFirstObjectByType<MoveRangeController>();

        partySelectionController = new PartySelectionController(partyRegistry, cameraFollower, rayDistance);
        moveCommandPreviewController = new MoveCommandPreviewController(
            gridManager,
            pathfinder,
            marker,
            pathPreviewRenderer,
            validMarkerColor,
            invalidMarkerColor,
            rayDistance,
            moveRangeController);

        partySelectionController.ActiveMoverChanged += HandleActiveMoverChanged;
        partySelectionController.ActiveMoverPathUpdated += HandleActiveMoverPathUpdated;
        partySelectionController.ActiveMoverMoveCompleted += HandleActiveMoverMoveCompleted;

        if (turnManager != null)
            turnManager.EnemyTurnStateChanged += HandleEnemyTurnStateChanged;

        partySelectionController.Initialize();
        moveCommandPreviewController.Initialize();
        moveRangeController?.RefreshForMover(partySelectionController.ActiveMover);
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
        moveCommandPreviewController?.HideGuidance();
    }

    private void OnDestroy()
    {
        if (pendingMoveRangeRefreshCoroutine != null)
        {
            StopCoroutine(pendingMoveRangeRefreshCoroutine);
            pendingMoveRangeRefreshCoroutine = null;
        }

        moveCommandPreviewController?.Dispose();
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

        // 스크롤/포인터 이탈은 클릭만 차단하며 월드 안내는 계속 갱신한다.
        if (!WorldInputGate.IsBlocked && Input.GetMouseButtonDown(0))
            TryHandleClick();

        PartyGridMover activeMover = partySelectionController != null ? partySelectionController.ActiveMover : null;
        if (activeMover != null && !IsMoverUsable(activeMover))
        {
            partySelectionController?.ClearActiveMover();
            moveCommandPreviewController?.ClearPreview();
            activeMover = null;
        }

        if (activeMover != null && activeMover.IsMoving)
            moveCommandPreviewController?.UpdateRealtimePathPreview(activeMover);

        moveCommandPreviewController?.UpdateGuidance(activeMover, zoneGuidanceArrowShader,
            gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    public void ClearMovePreview()
    {
        moveCommandPreviewController?.ClearPreview();
    }

    public void ClearActiveMoverIf(PartyGridMover party)
    {
        if (party == null || partySelectionController == null)
            return;

        if (partySelectionController.ActiveMover != party)
            return;

        partySelectionController.ClearActiveMover();
        moveCommandPreviewController?.ClearPreview();
    }

    private void TryHandleClick()
    {
        PartyGridMover activeMover = partySelectionController != null ? partySelectionController.ActiveMover : null;
        if (mainCamera == null || gridManager == null || activeMover == null || pathfinder == null || marker == null)
            return;

        if (!IsMoverUsable(activeMover))
        {
            partySelectionController?.ClearActiveMover();
            moveCommandPreviewController?.ClearPreview();
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (turnManager != null && turnManager.IsEnemyTurnRunning)
            return;

        // [JC 추가 260511] 이동 중 클릭 → 정지. 마커는 유지하고 path를 현재 위치 기준 재계산
        if (activeMover.IsMoving)
        {
            activeMover.StopMovement();
            if (ZoneEntryGuidanceController.IsActive)
                moveCommandPreviewController?.ClearPreview();
            else
                moveCommandPreviewController?.RecomputePathForCurrent(activeMover);
            return;
        }

        if (IsPartyInputLocked(activeMover))
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

        if (moveCommandPreviewController.TryConfirmMoveAtGrid(clickedGrid, activeMover))
        {
            partySelectionController.FocusActiveMover();
            return;
        }

        moveCommandPreviewController.PreviewMoveToGrid(activeMover, clickedGrid);
    }

    private static bool IsMoverUsable(PartyGridMover mover)
    {
        return mover != null
            && mover.gameObject.activeInHierarchy
            && !DefeatedPartyReturnController.IsPartyWaiting(mover);
    }

    /// <summary>표시 전용 조회입니다. 경로 미리보기·선택·이동 명령을 실행하지 않습니다.</summary>
    public bool IsPointerOverUnreachableGround(Vector2 screenPosition)
    {
        PartyGridMover mover = partySelectionController?.ActiveMover;
        if (!isActiveAndEnabled || WorldInputGate.IsBlocked || SceneFadeController.HideCursorDuringTransition
            || ExplorationModalEvents.MapEventModalActive || DHGameEndState.IsEnding
            || mainCamera == null || gridManager == null || moveRangeController == null
            || !moveRangeController.isActiveAndEnabled || !IsMoverUsable(mover)
            || mover.IsMoving || IsPartyInputLocked(mover)
            || (turnManager != null && turnManager.IsEnemyTurnRunning)
            || IsPointerOverUI(screenPosition))
            return false;

        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        int count = CollectPointerHits(ray);
        if (!TryGetClosestLandHit(count, out RaycastHit landHit)) return false;
        Component interactionTarget = null;
        float interactionDistance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Transform hit = pointerHits[i].transform;
            if (hit == null || pointerHits[i].distance > landHit.distance + 0.01f) continue;
            if (partySelectionController.IsSelectionTarget(hit)
                || (moveCommandPreviewController != null && moveCommandPreviewController.IsConfirmableMarker(hit)))
                return false;

            Component target = GetInteractionHoverTarget(hit);
            if (target != null && pointerHits[i].distance < interactionDistance)
            {
                interactionTarget = target;
                interactionDistance = pointerHits[i].distance;
            }
        }

        if (interactionTarget != null)
            return !CanReachHoveredInteraction(interactionTarget, mover);

        Vector2Int cell = gridManager.WorldToGrid(landHit.point);
        interactionTarget = GetInteractionHoverTargetAtCell(cell);
        if (interactionTarget != null)
            return !CanReachHoveredInteraction(interactionTarget, mover);
        return !moveRangeController.AllowsCell(cell);
    }

    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        var system = EventSystem.current;
        if (system == null) return false;
        if (hoverEventSystem != system || hoverPointer == null)
        {
            hoverEventSystem = system;
            hoverPointer = new PointerEventData(system);
        }
        hoverPointer.Reset();
        hoverPointer.position = screenPosition;
        hoverUIHits.Clear();
        system.RaycastAll(hoverPointer, hoverUIHits);
        foreach (var hit in hoverUIHits)
            if (hit.module is UnityEngine.UI.GraphicRaycaster) return true;
        return false;
    }

    private static Component GetInteractionHoverTarget(Transform hit)
    {
        return hit.GetComponentInParent<IGridItemObject>() as Component
            ?? hit.GetComponentInParent<IGridEnemyObject>() as Component
            ?? (Component)hit.GetComponentInParent<Outpost>()
            ?? hit.GetComponentInParent<HeroUnionUnit>()
            ?? (Component)hit.GetComponentInParent<VillainUnionBase>()
            ?? hit.GetComponentInParent<TutorialBuildingObject>()
            ?? (Component)hit.GetComponentInParent<TutorialOutpostObject>()
            ?? hit.GetComponentInParent<MapEventObject>()
            ?? (Component)hit.GetComponentInParent<WorldEventObject>()
            ?? hit.GetComponentInParent<MainEventObject>()
            ?? (Component)hit.GetComponentInParent<SubEventObject>();
    }

    private Component GetInteractionHoverTargetAtCell(Vector2Int cell)
    {
        if (gridManager.TryGetGridItemObjectAtGrid(cell, out IGridItemObject item)) return item as Component;
        if (gridManager.TryGetGridEnemyObjectAtGrid(cell, out IGridEnemyObject enemy)) return enemy as Component;
        if (gridManager.TryGetOutpostObjectAtGrid(cell, out Outpost outpost)) return outpost;
        if (gridManager.TryGetHeroUnionObjectAtGrid(cell, out HeroUnionUnit heroUnion)) return heroUnion;
        if (gridManager.TryGetVillainUnionBaseAtGrid(cell, out VillainUnionBase villainUnion)) return villainUnion;
        if (gridManager.TryGetTutorialBuildingObjectAtGrid(cell, out TutorialBuildingObject building)) return building;
        if (gridManager.TryGetEventObjectAtGrid(cell, out MapEventObject mapEvent)) return mapEvent;
        if (gridManager.TryGetWorldEventObjectAtGrid(cell, out WorldEventObject worldEvent)) return worldEvent;
        if (gridManager.TryGetMainEventObjectAtGrid(cell, out MainEventObject mainEvent)) return mainEvent;
        if (gridManager.TryGetSubEventObjectAtGrid(cell, out SubEventObject subEvent)) return subEvent;
        return null;
    }

    private bool CanReachHoveredInteraction(Component target, PartyGridMover mover)
    {
        hoverInteractionCells.Clear();
        MultiGridOccupant occupant = target.GetComponent<MultiGridOccupant>();
        if (target is IGridEnemyObject enemy)
        {
            enemy.GetInteractionCells(hoverInteractionCells);
        }
        else if (target is WorldEventObject && occupant != null)
        {
            hoverInteractionCells.AddRange(occupant.GetAdjacentOuterCells());
        }
        else if (occupant != null && (target is Outpost || target is HeroUnionUnit ||
                 target is VillainUnionBase || target is TutorialBuildingObject || target is TutorialOutpostObject))
        {
            Vector2Int anchor = occupant.AnchorGrid;
            for (int x = 0; x < occupant.Size.x; x++)
                hoverInteractionCells.Add(new Vector2Int(anchor.x + x, anchor.y - 1));
        }
        else
        {
            Vector2Int origin = target is Outpost outpost
                ? outpost.GetAnchorGrid(gridManager)
                : gridManager.WorldToGrid(target.transform.position);
            int radius = target is MainEventObject mainEvent ? mainEvent.InteractionRadius : 1;
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (x != 0 || y != 0)
                        hoverInteractionCells.Add(origin + new Vector2Int(x, y));
        }

        Vector2Int currentGrid = mover.GetCurrentGrid();
        // Current-cell interaction needs no movement; other candidates use the cached BFS range.
        for (int i = 0; i < hoverInteractionCells.Count; i++)
            if (hoverInteractionCells[i] == currentGrid || moveRangeController.AllowsCell(hoverInteractionCells[i]))
                return true;

        return false;
    }

    private int CollectPointerHits(Ray ray)
    {
        int count;
        // 평소에는 버퍼를 재사용하고, 충돌체가 많은 곳에서만 확장해 판정 누락을 방지합니다.
        while ((count = Physics.RaycastNonAlloc(ray, pointerHits, rayDistance)) == pointerHits.Length)
            System.Array.Resize(ref pointerHits, pointerHits.Length * 2);
        return count;
    }

    private static bool IsPartyInputLocked(PartyGridMover mover)
    {
        if (mover == null)
            return false;

        PartyRuntime runtime = mover.GetComponent<PartyRuntime>();
        if (runtime != null && runtime.IsInputLocked)
            return true;

        CombatContext combatContext = CombatContext.Instance;
        return combatContext != null && combatContext.IsTutorial;
    }

    private bool TryGetClosestLandHit(Ray ray, out RaycastHit landHit)
        => TryGetClosestLandHit(CollectPointerHits(ray), out landHit);

    private bool TryGetClosestLandHit(int hitCount, out RaycastHit landHit)
    {
        landHit = default;
        Transform groundTransform = gridManager.GroundRaycastTransform;
        if (groundTransform == null)
            return false;

        float bestDist = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            var h = pointerHits[i];
            var t = h.transform;
            if ((landLayerMask.value & (1 << h.collider.gameObject.layer)) == 0) continue;
            if (marker != null && (t == marker || t.IsChildOf(marker)))
                continue;

            bool isLandHit = t == groundTransform || (t != null && t.IsChildOf(groundTransform));
            bool isRaisedTileTopHit = h.normal.y > 0.5f && gridManager.HasCellSurfaceOffset(gridManager.WorldToGrid(h.point));
            if (isLandHit || isRaisedTileTopHit)
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
        moveRangeController?.RefreshForMover(partySelectionController != null ? partySelectionController.ActiveMover : null);
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
        {
            QueueMoveRangeRefreshNextFrame();
            return;
        }

        moveCommandPreviewController?.ClearPreview();
        moveRangeController?.Hide();
    }

    private void QueueMoveRangeRefreshNextFrame()
    {
        if (pendingMoveRangeRefreshCoroutine != null)
            StopCoroutine(pendingMoveRangeRefreshCoroutine);

        pendingMoveRangeRefreshCoroutine = StartCoroutine(RefreshMoveRangeNextFrame());
    }

    private IEnumerator RefreshMoveRangeNextFrame()
    {
        yield return null;
        pendingMoveRangeRefreshCoroutine = null;
        moveRangeController?.RefreshForMover(partySelectionController != null ? partySelectionController.ActiveMover : null);
    }

    private void RefreshUILockState()
    {
        if (uiInputBlocker == null)
            return;

        PartyGridMover activeMover = partySelectionController != null ? partySelectionController.ActiveMover : null;
        bool shouldLockUI =
            (turnManager != null && turnManager.IsEnemyTurnRunning) ||
            (activeMover != null && (activeMover.IsMoving || IsPartyInputLocked(activeMover)));
        uiInputBlocker.SetLocked(shouldLockUI);
    }
}
