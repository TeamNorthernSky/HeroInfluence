using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>Defers default click audio so popup/bubble sounds or confirmed movement can suppress it.</summary>
[DefaultExecutionOrder(-31000)]
public sealed class MouseClickSfx : MonoBehaviour
{
    private const string ClickSfxKey = "Button_Click";
    private const string PublicityDragSfxKey = "Popup_Drag";
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private static int chatBubbleFrame = -1;
    private static int popupSoundFrame = -1;
    private static int moveStartedFrame = -1;
    private static int clickFailFrame = -1;
    private int pendingClickFrame = -1;
    private string pendingClickKey;
    private bool pendingButtonRelease;
    private int pendingSceneHandle;
#if UNITY_EDITOR
    internal static System.Action<string> SinkOverride;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        chatBubbleFrame = -1;
        popupSoundFrame = -1;
        moveStartedFrame = -1;
        clickFailFrame = -1;
#if UNITY_EDITOR
        SinkOverride = null;
#endif
    }

    public static void NotifyChatBubbleCreated() => chatBubbleFrame = Time.frameCount;
    public static void NotifyPopupSoundPlayed() => popupSoundFrame = Time.frameCount;
    public static void NotifyMoveStarted() => moveStartedFrame = Time.frameCount;
    public static void NotifyClickFailPlayed() => clickFailFrame = Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var host = new GameObject("[KJ_MouseClickSfx]");
        DontDestroyOnLoad(host);
        host.AddComponent<MouseClickSfx>();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        // Capture the scene before this click can advance or exit the cutscene.
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "Opening CutScene" || sceneName == "Ending CutScene") return;

        string key = ClickSfxKey;
        bool waitForRelease = false;
        var events = EventSystem.current;
        if (events != null)
        {
            var pointer = new PointerEventData(events) { position = Input.mousePosition };
            hits.Clear();
            events.RaycastAll(pointer, hits);
            if (hits.Count > 0)
            {
                key = IsDisabledLobbyButton(hits[0].gameObject, sceneName)
                    ? "Button_Click_Fail"
                    : ResolveClickSfx(hits[0], pointer.position);
                waitForRelease = key == ClickSfxKey && hits[0].gameObject != null &&
                    hits[0].gameObject.GetComponentInParent<Button>() != null;
            }
        }
        pendingClickKey = key;
        pendingClickFrame = Time.frameCount;
        pendingButtonRelease = waitForRelease;
        pendingSceneHandle = SceneManager.GetActiveScene().handle;
    }

    private void LateUpdate()
    {
        FlushPendingClick(Input.GetMouseButton(0), Input.GetMouseButtonUp(0));
    }

    private void FlushPendingClick(bool held, bool released)
    {
        if (pendingClickFrame < 0) return;
        int frame = pendingClickFrame;
        string key = pendingClickKey;
        if (key == ClickSfxKey && (chatBubbleFrame >= frame || popupSoundFrame >= frame ||
                                   moveStartedFrame >= frame || clickFailFrame >= frame))
        {
            ClearPendingClick();
            return;
        }
        if (pendingButtonRelease)
        {
            if (pendingSceneHandle != SceneManager.GetActiveScene().handle)
            {
                ClearPendingClick();
                return;
            }
            if (held && !released) return;
            if (!released) { ClearPendingClick(); return; }
        }
        else if (frame != Time.frameCount) { ClearPendingClick(); return; }
        ClearPendingClick();
#if UNITY_EDITOR
        if (SinkOverride != null) { SinkOverride(key); return; }
#endif
        AudioManager.Instance?.PlaySfx(key);
    }

    private void OnDisable() => ClearPendingClick();

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) ClearPendingClick();
    }

    private void ClearPendingClick()
    {
        pendingClickFrame = -1;
        pendingClickKey = null;
        pendingButtonRelease = false;
    }

    private static string ResolveClickSfx(RaycastResult hit, Vector2 position)
    {
        if (hit.gameObject == null) return ClickSfxKey;
        var slider = hit.gameObject.GetComponentInParent<Slider>();
        if (slider == null || slider.name != "ProgressSlider" || !slider.IsActive() || !slider.IsInteractable())
            return ClickSfxKey;
        bool publicity = false;
        for (var parent = slider.transform.parent; parent != null; parent = parent.parent)
            if (parent.name == "Modal_Publicity" || parent.name == "Modal_Publicity(Clone)")
            { publicity = true; break; }
        if (!publicity) return ClickSfxKey;
        var area = slider.transform.Find("Handle Slide Area") as RectTransform;
        var camera = hit.module != null ? hit.module.eventCamera : null;
        return area != null && RectTransformUtility.RectangleContainsScreenPoint(area, position, camera)
            ? PublicityDragSfxKey : ClickSfxKey;
    }

    private static bool IsDisabledLobbyButton(GameObject hit, string sceneName)
    {
        if (sceneName != "HQLobbyScene" || hit == null) return false;
        var button = hit.GetComponentInParent<Button>();
        return button != null && button.gameObject.activeInHierarchy &&
            (!button.IsActive() || !button.IsInteractable());
    }
}
