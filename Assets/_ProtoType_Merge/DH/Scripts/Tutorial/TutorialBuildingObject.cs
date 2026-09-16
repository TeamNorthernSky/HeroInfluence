using System.Collections.Generic;
using UnityEngine;

public enum TutorialBuildingType
{
    Association = 0,
    EnemyBase = 1
}

[DisallowMultipleComponent]
[RequireComponent(typeof(MultiGridOccupant))]
public class TutorialBuildingObject : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private string buildingKey;
    [SerializeField] private TutorialBuildingType buildingType = TutorialBuildingType.Association;

    [Header("Placement")]
    [SerializeField] private Vector3 anchorLocalOffset;

    [Header("Overlay")]
    [SerializeField] private bool showInteractionOverlay = true;
    [SerializeField] private Color associationOverlayColor = new Color(0f, 0.35f, 1f, 0.28f);
    [SerializeField] private Color enemyBaseOverlayColor = new Color(1f, 0.15f, 0.15f, 0.28f);
    [SerializeField] private InteractionCellOverlayController overlayController;

    private MultiGridOccupant multiGridOccupant;
    private Renderer[] cachedRenderers;

    public string BuildingKey => string.IsNullOrWhiteSpace(buildingKey) ? string.Empty : buildingKey.Trim();
    public TutorialBuildingType BuildingType => buildingType;
    public Vector3 AnchorLocalOffset => anchorLocalOffset;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshInteractionOverlay();
    }

    private void OnDisable()
    {
        ClearInteractionOverlay();
    }

    private void OnValidate()
    {
        ResolveReferences();
        if (Application.isPlaying && isActiveAndEnabled)
            RefreshInteractionOverlay();
    }

    public void SetBuildingKey(string nextBuildingKey)
    {
        buildingKey = string.IsNullOrWhiteSpace(nextBuildingKey) ? string.Empty : nextBuildingKey.Trim();
    }

    public void SetBuildingType(TutorialBuildingType nextBuildingType)
    {
        buildingType = nextBuildingType;
    }

    public void SetAnchorLocalOffset(Vector3 nextAnchorLocalOffset)
    {
        anchorLocalOffset = nextAnchorLocalOffset;
    }

    public Vector3 GetRootPositionForAnchor(Vector3 anchorWorldPosition)
    {
        Matrix4x4 localToRoot = Matrix4x4.TRS(Vector3.zero, transform.rotation, transform.localScale);
        return anchorWorldPosition - localToRoot.MultiplyPoint3x4(anchorLocalOffset);
    }

    public Vector2Int GetAnchorGrid()
    {
        ResolveReferences();
        return multiGridOccupant != null ? multiGridOccupant.AnchorGrid : Vector2Int.zero;
    }

    public Vector2Int GetSize()
    {
        ResolveReferences();
        return multiGridOccupant != null ? multiGridOccupant.Size : Vector2Int.one;
    }

    public IReadOnlyList<Vector2Int> GetOccupiedCells()
    {
        ResolveReferences();
        return multiGridOccupant != null
            ? multiGridOccupant.GetOccupiedCells()
            : System.Array.Empty<Vector2Int>();
    }

    public IReadOnlyList<Vector2Int> GetInteractionCells()
    {
        ResolveReferences();
        return multiGridOccupant != null
            ? multiGridOccupant.GetBottomOuterCells()
            : System.Array.Empty<Vector2Int>();
    }

    public bool OccupiesGrid(Vector2Int grid)
    {
        ResolveReferences();
        return multiGridOccupant != null && multiGridOccupant.OccupiesCell(grid);
    }

    public bool IsInteractionCell(Vector2Int grid)
    {
        ResolveReferences();
        return multiGridOccupant != null && multiGridOccupant.IsBottomOuterCell(grid);
    }

    public bool TryGetRenderBounds(out Bounds bounds, float padding = 0f)
    {
        EnsureRenderersCached();

        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds && padding > 0f)
            bounds.Expand(padding);

        return hasBounds;
    }

    public void RefreshRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void ResolveReferences()
    {
        if (multiGridOccupant == null)
            multiGridOccupant = GetComponent<MultiGridOccupant>();

        if (overlayController == null)
            overlayController = FindFirstObjectByType<InteractionCellOverlayController>();
    }

    private void EnsureRenderersCached()
    {
        if (cachedRenderers == null)
            RefreshRenderers();
    }

    private void RefreshInteractionOverlay()
    {
        if (!showInteractionOverlay)
        {
            ClearInteractionOverlay();
            return;
        }

        ResolveReferences();
        if (overlayController == null)
            return;

        overlayController.SetExternalCells(this, GetInteractionCells(), GetOverlayColor());
    }

    private void ClearInteractionOverlay()
    {
        if (overlayController != null)
            overlayController.ClearExternalCells(this);
    }

    private Color GetOverlayColor()
    {
        return buildingType == TutorialBuildingType.EnemyBase
            ? enemyBaseOverlayColor
            : associationOverlayColor;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 anchorWorldPosition = transform.TransformPoint(anchorLocalOffset);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(anchorWorldPosition, transform.rotation, Vector3.one);
        Gizmos.color = new Color(0.25f, 0.8f, 1f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 0.02f, 1f));
        Gizmos.matrix = previousMatrix;

        Gizmos.color = new Color(0.25f, 0.95f, 1f, 1f);
        Gizmos.DrawWireSphere(anchorWorldPosition, 0.15f);
        Gizmos.DrawLine(transform.position, anchorWorldPosition);
    }
}
