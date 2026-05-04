using UnityEngine;

[DisallowMultipleComponent]
public class UIInputBlocker : MonoBehaviour
{
    [SerializeField] private CanvasGroup targetCanvasGroup;
    [SerializeField] private bool blockRaycastsWhenLocked = true;

    private bool isLocked;

    private void Awake()
    {
        if (targetCanvasGroup == null)
            targetCanvasGroup = GetComponent<CanvasGroup>();

        ApplyState();
    }

    public void SetLocked(bool locked)
    {
        if (isLocked == locked)
            return;

        isLocked = locked;
        ApplyState();
    }

    private void ApplyState()
    {
        if (targetCanvasGroup == null)
            return;

        targetCanvasGroup.interactable = !isLocked;
        targetCanvasGroup.blocksRaycasts = isLocked ? blockRaycastsWhenLocked : true;
    }
}
