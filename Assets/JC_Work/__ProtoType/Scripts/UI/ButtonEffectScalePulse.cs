using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ButtonEffectScalePulse : ButtonEffectModule
{
    [Header("Triggers")]
    [SerializeField] private bool pulseOnHover = true;
    [SerializeField] private bool pulseOnClick = false;

    [Header("Scales")]
    [SerializeField] private Vector3 hoverScale = new Vector3(1.05f, 1.05f, 1f);
    [SerializeField] private Vector3 clickScale = new Vector3(0.95f, 0.95f, 1f);
    [SerializeField] private float duration = 0.12f;

    private Vector3 baseScale;
    private Coroutine routine;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    public override void OnHoverEnter()
    {
        if (!pulseOnHover) return;
        StartTween(hoverScale, false);
    }

    public override void OnHoverExit()
    {
        if (!pulseOnHover) return;
        StartTween(baseScale, false);
    }

    public override void OnClick()
    {
        if (!pulseOnClick) return;
        StartTween(clickScale, true);
    }

    private void StartTween(Vector3 target, bool returnAfter)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(TweenScale(target, returnAfter));
    }

    private IEnumerator TweenScale(Vector3 target, bool returnAfter)
    {
        Vector3 from = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(from, target, elapsed / duration);
            yield return null;
        }
        transform.localScale = target;

        if (returnAfter)
        {
            elapsed = 0f;
            from = transform.localScale;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(from, baseScale, elapsed / duration);
                yield return null;
            }
            transform.localScale = baseScale;
        }
        routine = null;
    }
}
