using UnityEngine;

public sealed class WorldEventCameraFocusController : MonoBehaviour
{
    private const string RootName = "[DH_WorldEventCameraFocus]";

    private WorldEventObject pendingSource;
    private WorldEventObject focusedSource;
    private PartyGridMover focusedParty;
    private QuarterViewCameraFollower cameraFollower;
    private UnityEngine.SceneManagement.Scene hiddenScene;
    private bool hasHiddenScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindFirstObjectByType<WorldEventCameraFocusController>() != null)
            return;

        GameObject root = new GameObject(RootName);
        root.AddComponent<WorldEventCameraFocusController>();
        DontDestroyOnLoad(root);
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += HandleSceneUnloaded;
        WorldEventObject.EventInteracted += HandleWorldEventInteracted;

        DHWorldEventRuntimeManager runtimeManager = DHWorldEventRuntimeManager.EnsureInstance();
        if (runtimeManager != null)
        {
            runtimeManager.PresentationChanged += HandlePresentationChanged;
            runtimeManager.PresentationClosed += HandlePresentationClosed;
        }
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        WorldEventObject.EventInteracted -= HandleWorldEventInteracted;

        if (DHWorldEventRuntimeManager.Instance != null)
        {
            DHWorldEventRuntimeManager.Instance.PresentationChanged -= HandlePresentationChanged;
            DHWorldEventRuntimeManager.Instance.PresentationClosed -= HandlePresentationClosed;
        }

        ReleaseCameraLock(false);
    }

    private void HandleWorldEventInteracted(WorldEventObject source, PartyGridMover party)
    {
        pendingSource = source;
        focusedParty = party;
    }

    private void HandlePresentationChanged(DHWorldEventPresentationRequest request)
    {
        if (request == null || request.SourceType != DHWorldEventSourceType.Npc)
            return;

        if (pendingSource == null || pendingSource.WorldEventId != request.WorldEventId)
            return;

        hiddenScene = pendingSource.gameObject.scene;
        hasHiddenScene = true;
        PartyOcclusionFadeController.SetWorldEventRenderHidden(hiddenScene, true);
        FocusCameraOnNpc(pendingSource);
        focusedSource = pendingSource;
        pendingSource = null;
    }

    private void HandlePresentationClosed(DHWorldEventPresentationRequest request)
    {
        if (request == null || request.SourceType != DHWorldEventSourceType.Npc)
            return;

        ReleaseCameraLock(true);
    }

    private void FocusCameraOnNpc(WorldEventObject source)
    {
        if (source == null)
            return;

        cameraFollower = ResolveCameraFollower();
        if (cameraFollower == null)
            return;

        cameraFollower.LockExternalFocusWorldPosition(source.transform.position);
    }

    private void ReleaseCameraLock(bool recenterOnParty)
    {
        // Restore normal fading before the camera starts its return/zoom-out movement.
        if (hasHiddenScene)
        {
            PartyOcclusionFadeController.SetWorldEventRenderHidden(hiddenScene, false);
            hasHiddenScene = false;
        }

        if (cameraFollower == null)
            cameraFollower = ResolveCameraFollower();

        if (cameraFollower != null)
        {
            cameraFollower.UnlockExternalFocus();
            if (recenterOnParty && focusedParty != null)
            {
                cameraFollower.SetFollowTarget(focusedParty.transform);
                cameraFollower.ReturnToFollowTargetSmooth();
            }
        }

        pendingSource = null;
        focusedSource = null;
        focusedParty = null;
    }

    private void HandleSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
    {
        if (!hasHiddenScene || hiddenScene != scene)
            return;

        hasHiddenScene = false;
        pendingSource = null;
        focusedSource = null;
        focusedParty = null;
        cameraFollower = null;
    }

    private static QuarterViewCameraFollower ResolveCameraFollower()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.TryGetComponent(out QuarterViewCameraFollower follower))
            return follower;

        return FindFirstObjectByType<QuarterViewCameraFollower>();
    }
}
