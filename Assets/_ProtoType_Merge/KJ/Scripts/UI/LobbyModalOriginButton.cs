using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Records the button centre before its existing click handler opens a lobby modal.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class LobbyModalOriginButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private static int originFrame = -1;
    private static int originScene;
    private static Vector2 originScreen;
    private Button button;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOrigin() => originFrame = -1;

    private void Awake() => button = GetComponent<Button>();

    public void OnPointerDown(PointerEventData eventData) => Record(eventData);
    public void OnPointerUp(PointerEventData eventData) => Record(eventData);

    private void Record(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (button == null) button = GetComponent<Button>();
        if (!button.IsActive() || !button.IsInteractable()) return;
        originScreen = ButtonCentre(button);
        originScene = gameObject.scene.handle;
        originFrame = Time.frameCount;
    }

    private static Vector2 ButtonCentre(Button source)
    {
        var rect = (RectTransform)source.transform;
        var canvas = source.GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
    }

    public static bool TryGetOrigin(int sceneHandle, out Vector2 screen, bool allowCrossScene = false)
    {
        if (originFrame == Time.frameCount && (originScene == sceneHandle || allowCrossScene))
        {
            screen = originScreen;
            return true;
        }
        // Submit opens the window during Button's handler, before any later submit handlers run.
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
             Input.GetKeyDown(KeyCode.Space)) && EventSystem.current != null)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            var source = selected != null ? selected.GetComponentInParent<Button>() : null;
            if (source != null && (source.gameObject.scene.handle == sceneHandle || allowCrossScene) &&
                source.GetComponent<LobbyModalOriginButton>() != null)
            {
                screen = ButtonCentre(source);
                return true;
            }
        }
        screen = default;
        return false;
    }
}
