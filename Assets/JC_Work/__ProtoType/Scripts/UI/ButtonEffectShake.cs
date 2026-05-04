using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ButtonEffectShake : ButtonEffectModule
{
    [Header("Triggers")]
    [SerializeField] private bool shakeOnHover = false;
    [SerializeField] private bool shakeOnClick = true;

    [Header("Settings")]
    [SerializeField] private float duration = 0.2f;
    [SerializeField] private float intensity = 5f;

    private RectTransform rt;
    private Vector2 baseAnchoredPos;
    private Coroutine routine;

    private void Awake()
    {
        rt = transform as RectTransform;
        if (rt != null) baseAnchoredPos = rt.anchoredPosition;
    }

    public override void OnHoverEnter()
    {
        if (shakeOnHover) StartShake();
    }

    public override void OnClick()
    {
        if (shakeOnClick) StartShake();
    }

    private void StartShake()
    {
        if (rt == null) return;
        if (routine != null)
        {
            StopCoroutine(routine);
            rt.anchoredPosition = baseAnchoredPos;
        }
        baseAnchoredPos = rt.anchoredPosition;
        routine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float falloff = 1f - (elapsed / duration);
            float dx = Random.Range(-1f, 1f) * intensity * falloff;
            float dy = Random.Range(-1f, 1f) * intensity * falloff;
            rt.anchoredPosition = baseAnchoredPos + new Vector2(dx, dy);
            yield return null;
        }
        rt.anchoredPosition = baseAnchoredPos;
        routine = null;
    }
}
