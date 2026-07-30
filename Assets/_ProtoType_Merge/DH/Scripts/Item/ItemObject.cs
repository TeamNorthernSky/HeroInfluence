using UnityEngine;

public class ItemObject : MonoBehaviour
{
    private const string GetTriggerName = "Get";

    public ResourceType resourceType;
    public int amount;

    [SerializeField] private Animator animator;
    [SerializeField, Min(0f)] private float getAnimationDestroyDelay = 0.45f;

    private ItemRegistry itemRegistry;
    private bool isCollecting;

    private void OnEnable()
    {
        ResolveRegistry();
        itemRegistry?.Register(this);
    }

    private void OnDisable()
    {
        itemRegistry?.Unregister(this);
    }

    public void ApplyInitialAmount(int nextAmount)
    {
        amount = nextAmount;
    }

    public float GetItem()
    {
        if (isCollecting)
            return 0f;

        isCollecting = true;
        Game.Economy?.Add(resourceType, amount);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
        {
            animator.SetTrigger(GetTriggerName);
            Destroy(gameObject, getAnimationDestroyDelay);
            return getAnimationDestroyDelay;
        }

        Destroy(gameObject);
        return 0f;
    }

    public void RemoveWithoutReward()
    {
        Destroy(gameObject);
    }

    public Vector2Int GetCurrentGrid(GridManager gridManager)
    {
        return gridManager != null ? gridManager.WorldToGrid(transform.position) : Vector2Int.zero;
    }

    public bool OccupiesGrid(Vector2Int grid, GridManager gridManager)
    {
        return GetCurrentGrid(gridManager) == grid;
    }

    private void ResolveRegistry()
    {
        if (itemRegistry == null)
            itemRegistry = FindFirstObjectByType<ItemRegistry>();
    }
}
