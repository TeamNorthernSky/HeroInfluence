using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(TutorialOutpostObject))]
public sealed class TutorialOutpostClickEntry : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private bool requireDoubleClick;
    [SerializeField] private float doubleClickThreshold = 0.3f;
    [SerializeField] private float rayDistance = 1000f;

    private TutorialOutpostObject outpost;
    private float lastClickTime = -1f;

    private void Awake()
    {
        outpost = GetComponent<TutorialOutpostObject>();
        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void Update()
    {
        if (DHGameEndState.IsEnding)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (WorldInputGate.IsBlocked)
            return;

        if (ExplorationModalEvents.MapEventModalActive)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (!TryHitThisOutpost())
            return;

        if (outpost == null)
            return;

        outpost.RefreshStateVisuals();
        if (outpost.CurrentClaimState != TutorialOutpostClaimState.HeroClaimed)
            return;

        if (requireDoubleClick && !ConsumeDoubleClick())
            return;

        outpost.TryEnterTutorialLobbyScene();
    }

    private bool TryHitThisOutpost()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera == null)
            return false;

        Ray ray = worldCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance))
            return false;

        TutorialOutpostObject hitOutpost = hit.collider != null
            ? hit.collider.GetComponentInParent<TutorialOutpostObject>()
            : null;

        return hitOutpost != null && hitOutpost == outpost;
    }

    private bool ConsumeDoubleClick()
    {
        float now = Time.unscaledTime;
        if (lastClickTime > 0f && now - lastClickTime <= doubleClickThreshold)
        {
            lastClickTime = -1f;
            return true;
        }

        lastClickTime = now;
        return false;
    }
}
