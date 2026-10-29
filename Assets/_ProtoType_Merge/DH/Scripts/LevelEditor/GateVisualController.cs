using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GateVisualController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string openState = "Open";
    [SerializeField] private string closedState = "Close";

    private Coroutine animationRoutine;
    private bool hasState;
    private bool targetOpen;

    public bool IsAnimating { get; private set; }

    public void SetOpen(bool open, bool immediate)
    {
        if (!immediate && hasState && targetOpen == open)
            return;

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = null;
        IsAnimating = false;
        targetOpen = open;
        hasState = true;

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
            return;

        int state = Animator.StringToHash("Base Layer." + (open ? openState : closedState));
        if (!animator.HasState(0, state))
        {
            Debug.LogWarning($"[GateVisualController] Missing animation state on '{name}'.", this);
            return;
        }

        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.speed = 1f;
        animator.Play(state, 0, immediate ? 1f : 0f);
        animator.Update(0f);

        if (immediate || !Application.isPlaying || !isActiveAndEnabled)
        {
            FinishAnimation(state);
            return;
        }

        IsAnimating = true;
        animationRoutine = StartCoroutine(WaitForAnimation(state));
    }

    private IEnumerator WaitForAnimation(int state)
    {
        // Offscreen gates must also complete, so keep a bounded unscaled wait.
        float timeout = Mathf.Max(0.1f, animator.GetCurrentAnimatorStateInfo(0).length) + 0.5f;
        float elapsed = 0f;
        do
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        while (animator != null && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f && elapsed < timeout);

        FinishAnimation(state);
        animationRoutine = null;
        IsAnimating = false;
    }

    private void FinishAnimation(int state)
    {
        if (animator == null)
            return;
        animator.Play(state, 0, 1f);
        animator.Update(0f);
        animator.speed = 0f;
    }

    private void OnDisable()
    {
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = null;
        IsAnimating = false;
    }
}
