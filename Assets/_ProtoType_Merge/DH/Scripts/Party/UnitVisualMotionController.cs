using UnityEngine;

[DisallowMultipleComponent]
public class UnitVisualMotionController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator animator;

    [Header("Turn")]
    [SerializeField] private float turnSpeed = 900f;
    [SerializeField] private float yawOffset;

    [Header("Animation")]
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string moveStateName = "MoveForward";
    [SerializeField] private float animationFadeTime = 0.1f;
    [SerializeField] private bool disableRootMotion = true;
    [SerializeField] private bool logAnimationChanges;

    private Quaternion targetRotation;
    private bool hasTargetRotation;
    private bool isMoving;

    private bool ShouldLog => logAnimationChanges;

    private void Awake()
    {
        ResolveVisualRootIfNeeded();
        ResolveAnimatorIfNeeded();
        if (animator != null && disableRootMotion)
            animator.applyRootMotion = false;

        if (visualRoot != null)
        {
            targetRotation = visualRoot.rotation;
            hasTargetRotation = true;
        }

        if (ShouldLog)
        {
            RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
            Debug.Log(
                $"[UnitVisualMotionController] {name} awake. visualRoot={(visualRoot != null ? visualRoot.name : "null")} animator={(animator != null ? animator.name : "null")} controller={(controller != null ? controller.name : "null")}",
                this);
        }
    }

    private void Update()
    {
        if (visualRoot == null || !hasTargetRotation)
            return;

        visualRoot.rotation = Quaternion.RotateTowards(
            visualRoot.rotation,
            targetRotation,
            Mathf.Max(0f, turnSpeed) * Time.deltaTime);
    }

    public void SetLookDirection(Vector2Int gridDirection)
    {
        SetLookDirection(new Vector3(gridDirection.x, 0f, gridDirection.y));
    }

    public void SetLookDirection(Vector3 worldDirection)
    {
        ResolveVisualRootIfNeeded();
        if (visualRoot == null)
            return;

        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude <= 0.0001f)
            return;

        targetRotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up) *
                         Quaternion.Euler(0f, yawOffset, 0f);
        hasTargetRotation = true;
    }

    public void SetMoving(bool moving)
    {
        if (isMoving == moving)
        {
            if (ShouldLog)
                Debug.Log($"[UnitVisualMotionController] {name} SetMoving skipped. moving={moving}", this);
            return;
        }

        isMoving = moving;
        ResolveAnimatorIfNeeded();
        if (animator == null)
        {
            if (ShouldLog)
                Debug.LogWarning($"[UnitVisualMotionController] {name} has no Animator. moving={moving}", this);
            return;
        }

        if (disableRootMotion)
            animator.applyRootMotion = false;

        string stateName = moving ? moveStateName : idleStateName;
        if (string.IsNullOrWhiteSpace(stateName))
        {
            if (ShouldLog)
                Debug.LogWarning($"[UnitVisualMotionController] {name} animation state name is empty. moving={moving}", this);
            return;
        }

        if (ShouldLog)
        {
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            Debug.Log(
                $"[UnitVisualMotionController] {name} CrossFade state='{stateName}' moving={moving} animator='{animator.name}' controller='{(controller != null ? controller.name : "null")}'",
                this);
        }

        animator.CrossFade(stateName, Mathf.Max(0f, animationFadeTime));
    }

    private void ResolveVisualRootIfNeeded()
    {
        if (visualRoot != null)
            return;

        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator != null && animator.transform != transform)
        {
            visualRoot = animator.transform;
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && renderer.transform != transform)
            {
                visualRoot = renderer.transform;
                return;
            }
        }

        if (transform.childCount > 0)
        {
            visualRoot = transform.GetChild(0);
            return;
        }

        visualRoot = transform;
        Debug.LogWarning($"UnitVisualMotionController on '{name}' could not find a child visual root. Falling back to self.", this);
    }

    private void ResolveAnimatorIfNeeded()
    {
        if (animator != null)
            return;

        if (visualRoot != null)
            animator = visualRoot.GetComponentInChildren<Animator>(true);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }
}
