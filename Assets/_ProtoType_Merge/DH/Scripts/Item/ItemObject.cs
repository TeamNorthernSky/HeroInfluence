using PrimeTween;
using UnityEngine;

public interface IGridItemObject
{
    bool OccupiesGrid(Vector2Int grid, GridManager gridManager);
    float CollectFromInteraction(Transform flyTarget);
}

public class ItemObject : MonoBehaviour, IGridItemObject
{
    public ResourceType resourceType;
    public int amount;

    private ItemRegistry itemRegistry;
    private bool isCollecting;
    private Transform activeMotionRoot;
    private GameObject activeMotionContainer;
    private Vector3 motionStartPosition;
    private Vector3 motionEndPosition;
    private Vector3 motionStartScale;
    private ItemRegistry.PickupMotionSettings activeMotion;
    private bool activeUsesFlight;

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
        return GetItem(null);
    }

    public float GetItem(Transform flyTarget)
    {
        if (isCollecting)
            return 0f;

        isCollecting = true;
        Game.Economy?.Add(resourceType, amount);

        ItemRegistry.PickupMotionSettings motion = ResolvePickupMotion();

        if (TryPlayPickupMotion(flyTarget, motion, out float duration))
            return duration;

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

    public float CollectFromInteraction(Transform flyTarget)
    {
        return GetItem(flyTarget);
    }

    private void ResolveRegistry()
    {
        if (itemRegistry == null)
            itemRegistry = FindFirstObjectByType<ItemRegistry>();
    }

    private ItemRegistry.PickupMotionSettings ResolvePickupMotion()
    {
        ResolveRegistry();
        if (itemRegistry != null)
            return itemRegistry.PickupMotion;

        return new ItemRegistry.PickupMotionSettings
        {
            flyVisualToTargetOnGet = true,
            destroyDelay = 0.45f,
            flyDuration = 0.65f,
            arcHeight = 0.45f,
            targetYOffset = 0.35f
        };
    }

    private bool TryPlayPickupMotion(
        Transform flyTarget,
        ItemRegistry.PickupMotionSettings motion,
        out float duration)
    {
        activeUsesFlight = motion.flyVisualToTargetOnGet && flyTarget != null && motion.flyDuration > 0f;
        duration = activeUsesFlight ? motion.flyDuration : motion.destroyDelay;

        if (duration <= 0f)
            return false;

        activeMotionRoot = activeUsesFlight ? CreateFlyMotionRoot() : transform;
        if (activeMotionRoot == null)
            return false;

        activeMotion = motion;
        motionStartPosition = activeMotionRoot.position;
        motionEndPosition = activeUsesFlight
            ? flyTarget.position + Vector3.up * motion.targetYOffset
            : motionStartPosition;
        motionStartScale = activeMotionRoot.localScale;

        Tween.Custom(
                this,
                0f,
                1f,
                duration,
                static (item, progress) => item.ApplyPickupProgress(progress),
                Ease.OutCubic)
            .OnComplete(this, static item => item.DestroyAfterPickupMotion());

        return true;
    }

    private Transform CreateFlyMotionRoot()
    {
        GameObject container = new GameObject($"{name}_PickupFlyRoot");
        Transform containerTransform = container.transform;
        Transform originalParent = transform.parent;

        containerTransform.SetParent(originalParent, true);
        containerTransform.position = transform.position;
        containerTransform.rotation = Quaternion.identity;
        containerTransform.localScale = Vector3.one;

        transform.SetParent(containerTransform, true);
        activeMotionContainer = container;

        return containerTransform;
    }

    private void ApplyPickupProgress(float progress)
    {
        if (activeMotionRoot == null)
            return;

        if (activeUsesFlight)
        {
            Vector3 position = Vector3.Lerp(motionStartPosition, motionEndPosition, progress);
            position.y += activeMotion.arcHeight * 4f * progress * (1f - progress);
            activeMotionRoot.position = position;
        }

        activeMotionRoot.localScale = Vector3.Lerp(motionStartScale, Vector3.zero, progress);
    }

    private void DestroyAfterPickupMotion()
    {
        if (activeMotionContainer != null)
            Destroy(activeMotionContainer);
        else
            Destroy(gameObject);
    }
}
