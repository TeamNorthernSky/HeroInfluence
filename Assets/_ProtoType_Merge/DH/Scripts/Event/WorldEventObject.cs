using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(MultiGridOccupant))]
public sealed class WorldEventObject : MonoBehaviour
{
    public static event Action<WorldEventObject, PartyGridMover> EventInteracted;
    private const string EditorPreviewVisualName = "__WorldEventNpcPreview";
    private const float SpeechBubbleHeightOffset = 0.3f;

    [Header("World Event")]
    [SerializeField] private string worldEventId;
    [SerializeField] private bool oneShot = true;
    [SerializeField] private WorldEventRotation placementRotation;

    [Header("Grid")]
    [SerializeField] private GridManager gridManager;

    [Header("NPC Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private LevelPrefabRegistry prefabRegistry;
    [SerializeField] private bool spawnNpcVisualOnEnable = true;

    [Header("NPC UI")]
    [SerializeField] private Image questionIcon;
    [SerializeField] private Transform speechBubbleRoot;

    private Sprite defaultQuestionIcon;
    private bool defaultQuestionIconCaptured;
    private Vector3 defaultSpeechBubbleLocalPosition;
    private bool defaultSpeechBubblePositionCaptured;

    private WorldEventRegistry worldEventRegistry;
    private GameObject spawnedNpcVisual;
    private Coroutine npcVisualRetryCoroutine;
    private bool isTriggering;
    private Transform interactionFacingVisual;
    private Quaternion rotationBeforeInteraction;
    private UnitVisualMotionController[] interactionPartyUnits = Array.Empty<UnitVisualMotionController>();
#if UNITY_EDITOR
    private bool editorPreviewRefreshQueued;
#endif

    public string WorldEventId => string.IsNullOrWhiteSpace(worldEventId) ? string.Empty : worldEventId.Trim();
    public bool OneShot => oneShot;
    public string ProgressKey => BuildProgressKey(WorldEventId);
    public int ResolvedNpcType => TryResolveTemplate(out DHWorldEventTemplate template) ? template.NpcType : 0;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyFootprint(ResolvedNpcType);

        if (Application.isPlaying && oneShot && IsCompleted())
        {
            gameObject.SetActive(false);
            return;
        }

        worldEventRegistry?.Register(this);

        if (Application.isPlaying && TryResolveTemplate(out DHWorldEventTemplate template))
            ApplyQuestionIcon(template.NpcType);

        if (Application.isPlaying && spawnNpcVisualOnEnable)
            RequestNpcVisualRefresh();
        else if (!Application.isPlaying)
            QueueEditorNpcVisualRefresh();
    }

    private void OnDisable()
    {
        worldEventRegistry?.Unregister(this);
        StopNpcVisualRetry();
        RestoreInteractionFacing();
        RestorePartyFacing();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            QueueEditorNpcVisualRefresh();
    }

    public Vector2Int GetCurrentGrid(GridManager targetGridManager = null)
    {
        GridManager resolvedGridManager = targetGridManager != null ? targetGridManager : ResolveGridManager();
        return resolvedGridManager != null ? resolvedGridManager.WorldToGrid(transform.position) : Vector2Int.zero;
    }

    public bool OccupiesGrid(Vector2Int grid, GridManager targetGridManager = null)
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (occupant != null)
            return occupant.OccupiesCell(grid);

        return GetCurrentGrid(targetGridManager) == grid;
    }

    public static RectInt GetFootprintBounds(Vector2Int anchor, int npcType, WorldEventRotation rotation)
    {
        Vector2Int second = anchor;
        if (npcType == 4)
        {
            switch (rotation)
            {
                case WorldEventRotation.Degrees90: second += Vector2Int.right; break;
                case WorldEventRotation.Degrees180: second += Vector2Int.down; break;
                case WorldEventRotation.Degrees270: second += Vector2Int.left; break;
                default: second += Vector2Int.up; break;
            }
        }
        Vector2Int min = Vector2Int.Min(anchor, second);
        return new RectInt(min, Vector2Int.Max(anchor, second) - min + Vector2Int.one);
    }

    public System.Collections.Generic.IReadOnlyList<Vector2Int> GetInteractionCells()
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        return occupant != null ? occupant.GetAdjacentOuterCells() : Array.Empty<Vector2Int>();
    }

    public bool IsInteractionCell(Vector2Int grid)
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        return occupant != null ? occupant.IsAdjacentOuterCell(grid)
            : GridManager.GridDistance(grid, GetCurrentGrid()) == 1;
    }

    private void ApplyFootprint(int npcType)
    {
        GridManager manager = ResolveGridManager();
        if (manager == null)
            return;

        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (occupant == null)
            occupant = gameObject.AddComponent<MultiGridOccupant>();
        RectInt bounds = GetFootprintBounds(GetCurrentGrid(manager), npcType, placementRotation);
        occupant.SetSize(bounds.size);
        occupant.SetAnchorGrid(bounds.position);
        // UI is a sibling of the spawned visual and must not inherit placement rotation.
        transform.rotation = Quaternion.identity;
    }

    private void PositionNpcVisual()
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (spawnedNpcVisual == null || occupant == null || ResolveGridManager() == null)
            return;

        Vector3 center = (gridManager.GridToWorldCenter(occupant.AnchorGrid)
            + gridManager.GridToWorldCenter(occupant.AnchorGrid + occupant.Size - Vector2Int.one)) * 0.5f;
        center.y = transform.position.y;
        // Move only the visual; the placement anchor stays on the selected cell.
        spawnedNpcVisual.transform.position += center - transform.position;
    }

    private void PositionSpeechBubble()
    {
        if (speechBubbleRoot == null)
            return;

        if (!defaultSpeechBubblePositionCaptured)
        {
            defaultSpeechBubbleLocalPosition = speechBubbleRoot.localPosition;
            defaultSpeechBubblePositionCaptured = true;
        }

        speechBubbleRoot.localPosition = defaultSpeechBubbleLocalPosition;
        Vector3 position = speechBubbleRoot.position;
        position.y += SpeechBubbleHeightOffset;
        speechBubbleRoot.position = position;

        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (spawnedNpcVisual == null || occupant == null || occupant.Size == Vector2Int.one)
            return;

        bool hasBounds = false;
        Bounds bounds = default;
        Renderer[] renderers = spawnedNpcVisual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                continue;

            if (hasBounds)
                bounds.Encapsulate(renderer.bounds);
            else
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
        }

        if (!hasBounds)
            return;

        position.x = bounds.center.x;
        position.z = bounds.center.z;
        speechBubbleRoot.position = position;
    }

    public void ApplyInitialData(string nextWorldEventId, WorldEventRotation rotation = WorldEventRotation.Degrees0)
    {
        worldEventId = string.IsNullOrWhiteSpace(nextWorldEventId) ? string.Empty : nextWorldEventId.Trim();
        placementRotation = rotation;
        ApplyFootprint(ResolvedNpcType);

        if (Application.isPlaying && TryResolveTemplate(out DHWorldEventTemplate template))
            ApplyQuestionIcon(template.NpcType);

        if (Application.isPlaying && spawnNpcVisualOnEnable)
            RequestNpcVisualRefresh();
        else if (!Application.isPlaying)
            QueueEditorNpcVisualRefresh();
    }

    public bool TryTrigger(PartyGridMover party, Action<WorldEventObject> closedCallback = null)
    {
        if (!Application.isPlaying || isTriggering)
            return false;

        if (oneShot && IsCompleted())
            return false;

        if (string.IsNullOrWhiteSpace(WorldEventId))
        {
            Debug.LogWarning($"[WorldEventObject] {name}: WorldEventId is missing.", this);
            return false;
        }

        DHWorldEventRuntimeManager runtimeManager = DHWorldEventRuntimeManager.EnsureInstance();
        if (runtimeManager == null)
            return false;

        isTriggering = true;
        FacePartyForInteraction(party);
        FacePartyUnitsForInteraction(party);
        EventInteracted?.Invoke(this, party);

        bool started = runtimeManager.TryStartEvent(this, party, () =>
        {
            isTriggering = false;
            RestoreInteractionFacing();
            RestorePartyFacing();
            closedCallback?.Invoke(this);
        });

        if (!started)
        {
            isTriggering = false;
            RestoreInteractionFacing();
            RestorePartyFacing();
        }

        return started;
    }

    private void FacePartyUnitsForInteraction(PartyGridMover party)
    {
        RestorePartyFacing();
        if (party == null)
            return;

        Vector3 target = spawnedNpcVisual != null ? spawnedNpcVisual.transform.position : transform.position;
        Vector3 direction = target - party.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        float yaw = Mathf.Round(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg / 45f) * 45f;
        Vector3 facingDirection = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        interactionPartyUnits = party.GetComponentsInChildren<UnitVisualMotionController>();
        for (int i = 0; i < interactionPartyUnits.Length; i++)
        {
            UnitVisualMotionController unit = interactionPartyUnits[i];
            if (unit != null && unit.isActiveAndEnabled)
                unit.BeginInteractionFacing(facingDirection);
        }
    }

    private void RestorePartyFacing()
    {
        for (int i = 0; i < interactionPartyUnits.Length; i++)
        {
            if (interactionPartyUnits[i] != null)
                interactionPartyUnits[i].EndInteractionFacing();
        }

        interactionPartyUnits = Array.Empty<UnitVisualMotionController>();
    }

    private void FacePartyForInteraction(PartyGridMover party)
    {
        RestoreInteractionFacing();
        if (party == null || spawnedNpcVisual == null || !TryResolveTemplate(out DHWorldEventTemplate template))
            return;

        LevelPrefabRegistry registry = ResolvePrefabRegistry();
        if (registry == null || !registry.ShouldWorldEventNpcFacePartyOnInteraction(template.NpcType))
            return;

        Vector3 direction = party.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        interactionFacingVisual = spawnedNpcVisual.transform;
        rotationBeforeInteraction = interactionFacingVisual.localRotation;
        float yaw = Mathf.Round(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg / 45f) * 45f;
        // Face the party directly; the idle placement correction is restored afterwards.
        float deltaYaw = Mathf.DeltaAngle(interactionFacingVisual.eulerAngles.y, yaw);
        interactionFacingVisual.rotation = Quaternion.AngleAxis(deltaYaw, Vector3.up) * interactionFacingVisual.rotation;
    }

    private void RestoreInteractionFacing()
    {
        if (interactionFacingVisual != null)
            interactionFacingVisual.localRotation = rotationBeforeInteraction;
        interactionFacingVisual = null;
    }

    [ContextMenu("Refresh NPC Visual")]
    public void RefreshNpcVisual()
    {
        if (Application.isPlaying)
            TryRefreshNpcVisual();
        else
            RefreshEditorNpcVisual();
    }

    private void RequestNpcVisualRefresh()
    {
        if (TryRefreshNpcVisual())
            return;

        if (!isActiveAndEnabled || npcVisualRetryCoroutine != null)
            return;

        npcVisualRetryCoroutine = StartCoroutine(RetryRefreshNpcVisual());
    }

    private IEnumerator RetryRefreshNpcVisual()
    {
        const int retryFrames = 8;
        for (int i = 0; i < retryFrames; i++)
        {
            yield return null;

            if (!isActiveAndEnabled)
                break;

            if (TryRefreshNpcVisual())
                break;
        }

        npcVisualRetryCoroutine = null;
    }

    private bool TryRefreshNpcVisual()
    {
        if (!TryResolveTemplate(out DHWorldEventTemplate template) || template.NpcType < 0)
            return false;

        ApplyFootprint(template.NpcType);
        ApplyQuestionIcon(template.NpcType);

        LevelPrefabRegistry registry = ResolvePrefabRegistry();
        if (registry == null || !registry.TryGetWorldEventNpcPrefab(template.NpcType, out GameObject prefab) || prefab == null)
            return false;

        Transform root = visualRoot != null ? visualRoot : transform;
        DestroyNpcVisual(spawnedNpcVisual);
        DestroyNamedChild(root, EditorPreviewVisualName);

        spawnedNpcVisual = CreateNpcVisual(prefab, root, string.Empty, HideFlags.None);
        PositionNpcVisual();
        PositionSpeechBubble();
        return true;
    }

    private void ApplyQuestionIcon(int npcType)
    {
        if (questionIcon == null)
            return;

        if (!defaultQuestionIconCaptured)
        {
            defaultQuestionIcon = questionIcon.sprite;
            defaultQuestionIconCaptured = true;
        }

        LevelPrefabRegistry registry = ResolvePrefabRegistry();
        questionIcon.sprite = registry != null && registry.TryGetWorldEventNpcQuestionIcon(npcType, out Sprite icon)
            ? icon
            : defaultQuestionIcon;
    }

    private void QueueEditorNpcVisualRefresh()
    {
#if UNITY_EDITOR
        if (editorPreviewRefreshQueued || !CanUseEditorPreview())
            return;

        editorPreviewRefreshQueued = true;
        UnityEditor.EditorApplication.delayCall += RefreshQueuedEditorNpcVisual;
#endif
    }

#if UNITY_EDITOR
    private void RefreshQueuedEditorNpcVisual()
    {
        editorPreviewRefreshQueued = false;
        if (this == null || Application.isPlaying)
            return;

        RefreshEditorNpcVisual();
    }
#endif

    private void RefreshEditorNpcVisual()
    {
        if (!CanUseEditorPreview())
            return;

        ResolveReferences();

        Transform root = visualRoot != null ? visualRoot : transform;
        DestroyNamedChild(root, EditorPreviewVisualName);

        if (!TryResolveTemplate(out DHWorldEventTemplate template) || template.NpcType < 0)
            return;

        ApplyFootprint(template.NpcType);

        LevelPrefabRegistry registry = ResolvePrefabRegistry();
        if (registry == null || !registry.TryGetWorldEventNpcPrefab(template.NpcType, out GameObject prefab) || prefab == null)
            return;

        spawnedNpcVisual = CreateNpcVisual(
            prefab,
            root,
            EditorPreviewVisualName,
            HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild);
        PositionNpcVisual();
        PositionSpeechBubble();
    }

    private bool CanUseEditorPreview()
    {
        return !Application.isPlaying
            && gameObject != null
            && gameObject.scene.IsValid()
            && gameObject.scene.isLoaded;
    }

    private GameObject CreateNpcVisual(GameObject prefab, Transform root, string overrideName, HideFlags hideFlags)
    {
        GameObject instance = Instantiate(prefab, root);
        if (!string.IsNullOrWhiteSpace(overrideName))
            instance.name = overrideName;

        instance.hideFlags = hideFlags;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.Euler(0f, 180f + (int)placementRotation, 0f);
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private static void DestroyNpcVisual(GameObject visual)
    {
        if (visual == null)
            return;

        if (Application.isPlaying)
            Destroy(visual);
        else
            DestroyImmediate(visual);
    }

    private static void DestroyNamedChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child == null || child.name != childName)
                continue;

            DestroyNpcVisual(child.gameObject);
        }
    }

    private void StopNpcVisualRetry()
    {
        if (npcVisualRetryCoroutine == null)
            return;

        StopCoroutine(npcVisualRetryCoroutine);
        npcVisualRetryCoroutine = null;
    }

    public void Complete()
    {
        if (oneShot)
            MarkCompleted();

        gameObject.SetActive(false);
    }

    private bool IsCompleted()
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        return repository != null && repository.IsEventCompleted(ProgressKey);
    }

    private void MarkCompleted()
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        repository?.MarkEventCompleted(ProgressKey);
    }

    private GridManager ResolveGridManager()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        return gridManager;
    }

    private void ResolveReferences()
    {
        ResolveGridManager();

        if (worldEventRegistry == null)
            worldEventRegistry = FindFirstObjectByType<WorldEventRegistry>();

        if (visualRoot == null)
            visualRoot = transform;

        ResolvePrefabRegistry();
    }

    private LevelPrefabRegistry ResolvePrefabRegistry()
    {
        if (prefabRegistry == null)
            prefabRegistry = FindFirstObjectByType<LevelPrefabRegistry>();

        return prefabRegistry;
    }

    private bool TryResolveTemplate(out DHWorldEventTemplate template)
    {
        template = null;
        if (string.IsNullOrWhiteSpace(WorldEventId))
            return false;

        DHWorldEventCatalog catalog = DHWorldEventCatalog.Instance != null
            ? DHWorldEventCatalog.Instance
            : FindFirstObjectByType<DHWorldEventCatalog>();
        return catalog != null && catalog.TryGetEvent(WorldEventId, out template) && template != null;
    }

    private static string BuildProgressKey(string eventId)
    {
        string normalizedId = MapProgressKey.NormalizeSegment(eventId);
        return string.IsNullOrWhiteSpace(normalizedId)
            ? "world_event"
            : $"world_event_{normalizedId}";
    }
}

