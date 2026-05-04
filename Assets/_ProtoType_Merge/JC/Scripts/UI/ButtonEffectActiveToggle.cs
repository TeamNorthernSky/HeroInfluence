using UnityEngine;

[DisallowMultipleComponent]
public class ButtonEffectActiveToggle : ButtonEffectModule
{
    [Header("Hover")]
    [SerializeField] private bool toggleOnHover = true;
    [SerializeField] private GameObject[] hoverObjects;

    [Header("Click")]
    [SerializeField] private bool toggleOnClick = false;
    [SerializeField] private GameObject[] clickObjects;
    [SerializeField] private float clickActiveDuration = 0.15f;

    public override void OnHoverEnter()
    {
        if (!toggleOnHover) return;
        SetActive(hoverObjects, true);
    }

    public override void OnHoverExit()
    {
        if (!toggleOnHover) return;
        SetActive(hoverObjects, false);
    }

    public override void OnClick()
    {
        if (!toggleOnClick || clickObjects == null) return;
        SetActive(clickObjects, true);
        if (clickActiveDuration > 0f)
        {
            CancelInvoke(nameof(DisableClickObjects));
            Invoke(nameof(DisableClickObjects), clickActiveDuration);
        }
    }

    private void DisableClickObjects()
    {
        SetActive(clickObjects, false);
    }

    private static void SetActive(GameObject[] objs, bool active)
    {
        if (objs == null) return;
        for (int i = 0; i < objs.Length; i++)
            if (objs[i] != null) objs[i].SetActive(active);
    }
}
