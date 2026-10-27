using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class UIInputBlocker : MonoBehaviour
{
    [SerializeField] private CanvasGroup targetCanvasGroup;
    [SerializeField] private bool blockRaycastsWhenLocked = true;

    private bool isLocked;
    private readonly HashSet<object> additionalLocks = new HashSet<object>();

    public bool Controls(CanvasGroup group) => targetCanvasGroup == group;

    // 기본 이동 잠금과 연출 잠금을 독립적으로 유지해 오래된 UI 상태를 복원하지 않습니다.
    public void SetAdditionalLock(object owner, bool locked)
    {
        if (owner == null) return;
        bool changed = locked ? additionalLocks.Add(owner) : additionalLocks.Remove(owner);
        if (changed) ApplyState();
    }

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

        bool locked = isLocked || additionalLocks.Count > 0;
        targetCanvasGroup.interactable = !locked;
        targetCanvasGroup.blocksRaycasts = locked ? blockRaycastsWhenLocked : true;
    }
}
