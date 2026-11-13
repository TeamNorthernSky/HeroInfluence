using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Uses the NPC camera movement and zoom before opening a source's chat.</summary>
public sealed class ExplorationChatCameraFocus : MonoBehaviour
{
    private static ExplorationChatCameraFocus active;

    public static bool BlocksSystemMenuEscape =>
        active != null && !active.finished && ModalManager.Top == active.gameObject;
    private Transform source;
    private Transform party;
    private QuarterViewCameraFollower cameraFollower;
    private Func<bool> openChat;
    private Action onCancelled;
    private bool opened;
    private bool finished;
    private bool suspendedForBattle;
    private bool waitingForChatClose;
    private bool returningToParty;
    private bool notifyCancellationAfterReturn;
    private int followupPresentations;
    private bool finishRequested;
    private string returnSceneName;
    private QuarterViewCameraFollower.EventFocusSnapshot battleFocus;

    public static bool TryBegin(Transform source, Transform party, Func<bool> openChat,
        Action onCancelled, out ExplorationChatCameraFocus focus, bool focusOnParty = false)
    {
        focus = null;
        if (source == null || !source.gameObject.activeInHierarchy || active != null ||
            ModalManager.HasAny || SceneFadeController.IsInputBlocked)
            return false;

        Camera mainCamera = Camera.main;
        QuarterViewCameraFollower follower = mainCamera != null
            ? mainCamera.GetComponent<QuarterViewCameraFollower>() : null;
        if (follower == null) follower = FindFirstObjectByType<QuarterViewCameraFollower>();
        if (follower != null && (follower.IsExternalInputLocked || follower.IsTemporaryFocusActive || follower.IsEventFocusMoving))
            return false;

        var root = new GameObject("[KJ_ExplorationChatCameraFocus]");
        SceneManager.MoveGameObjectToScene(root, source.gameObject.scene);
        var created = root.AddComponent<ExplorationChatCameraFocus>();
        focus = created;
        created.source = source;
        created.party = party;
        created.cameraFollower = follower;
        created.openChat = openChat;
        created.onCancelled = onCancelled;
        active = created;
        // This invisible modal blocks world interaction/turn advance during the camera move.
        ModalManager.Register(root);
        SceneManager.activeSceneChanged += created.HandleActiveSceneChanged;
        SceneFadeController.TransitionFailed += created.HandleTransitionFailed;
        if (follower != null)
            follower.LockExternalFocusWorldPosition(ResolveFocusPosition(source, party, focusOnParty), true, created.HandleArrived);
        else
            created.HandleArrived();
        return !created.finished;
    }

    private static Vector3 ResolveFocusPosition(Transform source, Transform party, bool focusOnParty)
    {
        if (party == null) return source.position;
        HeroUnionUnit heroUnion = source.GetComponent<HeroUnionUnit>();
        if (heroUnion != null)
        {
            string zone = MapProgressKey.NormalizeSegment(heroUnion.ZoneId);
            if (zone.StartsWith("zone_", StringComparison.Ordinal)) zone = zone.Substring(5);
            focusOnParty |= int.TryParse(zone, out int zoneNumber) && (zoneNumber == 1 || zoneNumber == 2);
        }

        return focusOnParty ? party.position : source.position;
    }

    private void Update()
    {
        if (finished) return;
        if (returningToParty)
        {
            if (cameraFollower == null || !cameraFollower.isActiveAndEnabled || !cameraFollower.IsEventFocusMoving)
                CompleteRelease();
            return;
        }
        if (suspendedForBattle)
        {
            if (DHGameEndState.IsEnding) Release(false, false);
            return;
        }
        if (source == null || source.gameObject.scene != SceneManager.GetActiveScene() ||
            (!opened && (!source.gameObject.activeInHierarchy || cameraFollower == null ||
                !cameraFollower.isActiveAndEnabled || !cameraFollower.IsEventFocusMoving)) || DHGameEndState.IsEnding)
            Cancel();
    }

    private bool IsSourceAvailable()
    {
        return source != null && source.gameObject.activeInHierarchy &&
            source.gameObject.scene == SceneManager.GetActiveScene() && !SceneFadeController.IsInputBlocked;
    }

    private void HandleArrived()
    {
        if (finished || opened) return;
        if (!IsSourceAvailable())
        {
            Cancel();
            return;
        }

        try
        {
            // A first-row battle effect can close the chat before Show returns.
            opened = true;
            bool didOpen = openChat != null && openChat();
            if (finished || suspendedForBattle) return;
            if (!didOpen)
            {
                opened = false;
                Cancel();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            opened = false;
            Cancel();
        }
    }

    /// <summary>Keeps the chat camera through a notice and its follow-up dialogue.</summary>
    public static Action HoldForFollowupPresentation()
    {
        ExplorationChatCameraFocus focus = active;
        if (focus == null || focus.finished || focus.returningToParty || focus.suspendedForBattle)
            return () => { };

        focus.followupPresentations++;
        bool released = false;
        return () =>
        {
            if (released) return;
            released = true;
            if (focus == null || focus.finished) return;
            focus.followupPresentations--;
            if (focus.followupPresentations == 0 && focus.finishRequested) focus.Finish();
        };
    }

    public void Finish()
    {
        if (finished || returningToParty) return;
        if (suspendedForBattle || TrySuspendForBattle()) return;
        finishRequested = true;
        if (followupPresentations > 0) return;
        if (waitingForChatClose) return;
        if (ChatModalController.TryDeferUntilClosed(HandleChatWindowClosed))
        {
            waitingForChatClose = true;
            return;
        }
        Release(true, false);
    }

    private void HandleChatWindowClosed()
    {
        if (this == null) return;
        waitingForChatClose = false;
        Finish();
    }

    public void Cancel()
    {
        if (finished) return;
        if (returningToParty)
        {
            CompleteRelease();
            return;
        }
        // Source objects are destroyed when entering battle; the retained focus outlives them.
        if (suspendedForBattle || TrySuspendForBattle()) return;
        bool recenter = party != null && party.gameObject.scene == SceneManager.GetActiveScene() &&
            !SceneFadeController.IsInputBlocked && !DHGameEndState.IsEnding;
        Release(recenter, !opened);
    }

    private void HandleActiveSceneChanged(Scene previous, Scene current)
    {
        if (suspendedForBattle || TrySuspendForBattle())
        {
            string loadingScene = GameSceneManager.Instance != null ? GameSceneManager.Instance.GameLoadScene : "GameLoadScene";
            if (current.name != returnSceneName && current.name != TmpBattleSceneFade.SceneName && current.name != loadingScene)
                Release(false, false);
            return;
        }
        if (source == null || source.gameObject.scene != current) Cancel();
    }

    private bool TrySuspendForBattle()
    {
        CombatContext context = CombatContext.Instance;
        if (!opened || cameraFollower == null || !cameraFollower.IsEventZoomApplied ||
            context == null || !context.HasEventBattle || context.Result != CombatResult.None)
            return false;

        battleFocus = cameraFollower.CaptureEventFocus();
        returnSceneName = source != null ? source.gameObject.scene.name : SceneManager.GetActiveScene().name;
        suspendedForBattle = true;
        openChat = null;
        onCancelled = null;
        ModalManager.Unregister(gameObject);
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        return true;
    }

    public static ExplorationChatCameraFocus RestoreAfterBattle(bool hasFollowupChat)
    {
        ExplorationChatCameraFocus focus = active;
        if (focus == null || !focus.suspendedForBattle || SceneManager.GetActiveScene().name != focus.returnSceneName)
            return null;

        Camera mainCamera = Camera.main;
        QuarterViewCameraFollower follower = mainCamera != null ? mainCamera.GetComponent<QuarterViewCameraFollower>() : null;
        if (follower == null) follower = FindFirstObjectByType<QuarterViewCameraFollower>();
        PartyRegistry parties = FindFirstObjectByType<PartyRegistry>();
        Transform playerParty = parties != null && parties.PlayerParty != null ? parties.PlayerParty.transform : null;
        return focus.ResumeWithCamera(follower, playerParty, hasFollowupChat) ? focus : null;
    }

    private bool ResumeWithCamera(QuarterViewCameraFollower follower, Transform playerParty, bool hasFollowupChat)
    {
        suspendedForBattle = false;
        cameraFollower = follower;
        if (cameraFollower == null)
        {
            Release(false, false);
            return false;
        }
        party = playerParty;
        source = party != null ? party : cameraFollower.transform;
        cameraFollower.RestoreEventFocus(battleFocus);
        opened = true;
        if (!hasFollowupChat)
        {
            cameraFollower.UnlockExternalFocus();
            if (party != null)
            {
                cameraFollower.SetFollowTarget(party);
                cameraFollower.SnapToFollowTarget();
            }
            else cameraFollower.SnapFocusWorldPosition(battleFocus.FocusPosition);
            Release(false, false);
            return false;
        }

        ModalManager.Register(gameObject);
        return true;
    }

    private void HandleTransitionFailed(Scene scene)
    {
        if (suspendedForBattle && scene.name == returnSceneName)
            Release(true, false);
    }

    private void OnDisable()
    {
        if (suspendedForBattle) Release(false, false);
        else Cancel();
    }

    private void Release(bool recenterOnParty, bool notifyOwner)
    {
        if (finished || returningToParty) return;
        suspendedForBattle = false;
        notifyCancellationAfterReturn = notifyOwner;
        if (cameraFollower != null)
        {
            cameraFollower.UnlockExternalFocus();
            if (recenterOnParty && party != null && party.gameObject.scene == SceneManager.GetActiveScene())
            {
                // Gate presentation already waits for ModalManager: retain ownership until zoom-out ends.
                returningToParty = true;
                ModalManager.Register(gameObject);
                cameraFollower.SetFollowTarget(party);
                cameraFollower.ReturnToFollowTargetSmooth(CompleteRelease);
                return;
            }
        }

        CompleteRelease();
    }

    private void CompleteRelease()
    {
        if (finished) return;
        finished = true;
        returningToParty = false;
        if (active == this) active = null;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneFadeController.TransitionFailed -= HandleTransitionFailed;
        ModalManager.Unregister(gameObject);

        Action cancelled = onCancelled;
        onCancelled = null;
        openChat = null;
        if (Application.isPlaying) Destroy(gameObject);
        if (notifyCancellationAfterReturn) cancelled?.Invoke();
    }
}
