using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Plays one hover sound when the pointer enters a usable button, including runtime UI.</summary>
public sealed class ButtonHoverSfx : MonoBehaviour
{
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private EventSystem pointerEventSystem;
    private PointerEventData pointerData;
    private Button hoveredButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var host = new GameObject("[KJ_ButtonHoverSfx]");
        DontDestroyOnLoad(host);
        host.AddComponent<ButtonHoverSfx>();
    }

    private void LateUpdate()
    {
        var currentEventSystem = EventSystem.current;
        if (currentEventSystem == null || !JcPointerInput.CanControl || !JcPointerInput.Inside)
        {
            hoveredButton = null;
            return;
        }

        if (pointerEventSystem != currentEventSystem)
        {
            pointerEventSystem = currentEventSystem;
            pointerData = new PointerEventData(currentEventSystem);
            hoveredButton = null;
        }

        pointerData.Reset();
        pointerData.pointerId = -1;
        pointerData.position = JcPointerInput.ScreenPosition;
        hits.Clear();
        currentEventSystem.RaycastAll(pointerData, hits);

        // Only the frontmost hit counts: modal backdrops must block buttons underneath.
        var target = hits.Count > 0 && hits[0].gameObject != null
            ? hits[0].gameObject.GetComponentInParent<Button>()
            : null;
        if (target == hoveredButton) return;
        hoveredButton = target;
        if (target == null || !target.IsActive() || !target.IsInteractable()) return;

        AudioManager.Instance?.PlaySfx("Button_Hover");
    }

    private void OnDisable()
    {
        hoveredButton = null;
        hits.Clear();
    }
}
