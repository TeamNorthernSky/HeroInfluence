using UnityEngine;

public class CastleUnit : MonoBehaviour
{
    [SerializeField] private string castleId = "castle_001";
    [SerializeField] private GridManager gridManager;
    private CastleRegistry castleRegistry;

    public string CastleId => castleId;

    private void Awake()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
    }

    private void OnEnable()
    {
        ResolveReferences();
        castleRegistry?.Register(this);
    }

    private void OnDisable()
    {
        castleRegistry?.Unregister(this);
    }

    public Vector2Int GetCurrentGrid()
    {
        return gridManager != null ? gridManager.WorldToGrid(transform.position) : Vector2Int.zero;
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (castleRegistry == null)
            castleRegistry = FindFirstObjectByType<CastleRegistry>();
    }
}
