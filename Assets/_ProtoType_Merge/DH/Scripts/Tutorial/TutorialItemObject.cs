using PrimeTween;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialItemObject : MonoBehaviour
{
    [Header("Reward")]
    [SerializeField] private ResourceType resourceType = ResourceType.Money;
    [SerializeField] private int amount = 1;

    private TutorialItemRegistry registry;
    private bool isCollecting;
    private Transform activeMotionRoot;
    private GameObject activeMotionContainer;
    private Vector3 motionStartPosition;
    private Vector3 motionEndPosition;
    private Vector3 motionStartScale;
    private TutorialItemRegistry.PickupMotionSettings activeMotion;
    private bool activeUsesFlight;

    public ResourceType ResourceType => resourceType;
    public int Amount => amount;

    private void OnEnable()
    {
        ResolveRegistry();
        registry?.Register(this);
    }

    private void OnDisable()
    {
        registry?.Unregister(this);
    }

    public float Collect()
    {
        return Collect(null);
    }

    public float Collect(Transform flyTarget)
    {
        if (isCollecting)
            return 0f;

        isCollecting = true;
        TutorialProgressRepository.EnsureInstance()?.AddResource(resourceType, amount);

        TutorialItemRegistry.PickupMotionSettings motion = ResolvePickupMotion();
        if (TryPlayPickupMotion(flyTarget, motion, out float duration))
            return duration;

        RemoveWithoutReward();
        return 0f;
    }

    public void RemoveWithoutReward()
    {
        Destroy(activeMotionContainer != null ? activeMotionContainer : gameObject);
    }

    public Vector2Int GetCurrentGrid(GridManager gridManager)
    {
        return gridManager != null ? gridManager.WorldToGrid(transform.position) : Vector2Int.zero;
    }

    private void ResolveRegistry()
    {
        if (registry == null)
            registry = FindFirstObjectByType<TutorialItemRegistry>();
    }

    private TutorialItemRegistry.PickupMotionSettings ResolvePickupMotion()
    {
        ResolveRegistry();
        if (registry != null)
            return registry.PickupMotion;

        return new TutorialItemRegistry.PickupMotionSettings
        {
            flyVisualToTargetOnGet = true,
            duration = 0.45f,
            arcHeight = 0.25f,
            targetYOffset = 0.25f
        };
    }

    private bool TryPlayPickupMotion(
        Transform flyTarget,
        TutorialItemRegistry.PickupMotionSettings motion,
        out float duration)
    {
        activeUsesFlight = motion.flyVisualToTargetOnGet && flyTarget != null && motion.duration > 0f;
        duration = motion.duration;

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
            .OnComplete(this, static item => item.RemoveWithoutReward());

        return true;
    }

    private Transform CreateFlyMotionRoot()
    {
        GameObject container = new GameObject($"{name}_TutorialPickupFlyRoot");
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
}
