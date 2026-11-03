using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Observes named UI popups and CommonUI modals without modifying shared controllers.</summary>
public sealed class PopupSoundService : MonoBehaviour
{
    private static PopupSoundService instance;
    private static readonly Dictionary<GameObject, int> playedFrame = new Dictionary<GameObject, int>();
    private static bool suppressInstallationSound;
#if UNITY_EDITOR
    internal static Action<string> SinkOverride;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        instance = null;
        playedFrame.Clear();
        suppressInstallationSound = false;
#if UNITY_EDITOR
        SinkOverride = null;
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var host = new GameObject("[KJ_PopupSound]");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<PopupSoundService>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    private void OnEnable()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ModalManager.OnStackChanged += HandleStackChanged;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        ModalManager.OnStackChanged -= HandleStackChanged;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects()) InstallHierarchy(root, false);
        if (CommonUIManager.Instance != null)
            InstallHierarchy(CommonUIManager.Instance.gameObject, false);
    }

    private void HandleStackChanged()
    {
        // Closing an overlay exposes an already-enabled modal. Existing hooks do not replay.
        if (ModalManager.Top != null) InstallHierarchy(ModalManager.Top, true);
    }

    public static void InstallHierarchy(GameObject root, bool playAlreadyVisible = false)
    {
        if (root == null) return;
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            var target = child.gameObject;
            if (!ShouldObserve(target) || target.GetComponent<PopupEnableSfx>() != null) continue;
            bool previous = suppressInstallationSound;
            suppressInstallationSound = !playAlreadyVisible;
            try { target.AddComponent<PopupEnableSfx>(); }
            finally { suppressInstallationSound = previous; }
        }
    }

    private static bool ShouldObserve(GameObject target)
    {
        string name = target.name.Replace("(Clone)", string.Empty).Trim();
        if (string.Equals(name, "CommonUI", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "CommonUIManager", StringComparison.OrdinalIgnoreCase)) return true;
        if (target.GetComponent<SettingsModalController>() != null) return true;
        if (target.GetComponent<SaveSlotPanel>() != null) return true;
        if (target.GetComponent<Modal>() != null &&
            target.GetComponentInParent<CommonUIManager>(true) != null) return true;
        if (target.GetComponent<RectTransform>() == null || target.GetComponentInParent<Canvas>(true) == null)
            return false;
        if (IsUpgradeResult(target)) return true;
        if (string.Equals(name, "Popup", StringComparison.OrdinalIgnoreCase)) return true;
        // Interactive named popups, not floating damage numbers or individual popup buttons.
        return name.IndexOf("popup", StringComparison.OrdinalIgnoreCase) >= 0 &&
            target.GetComponent<Button>() == null &&
            target.GetComponentInChildren<Button>(true) != null;
    }

    public static void NotifyEnabled(GameObject source)
    {
        if (!suppressInstallationSound) PlayPopup(source);
    }

    public static void PlayPopup(GameObject source)
    {
        if (source == null || !source.activeInHierarchy) return;
        if (playedFrame.TryGetValue(source, out int frame) && frame == Time.frameCount) return;
        // Enabling a parent window also enables its children; that is one window opening.
        for (var parent = source.transform.parent; parent != null; parent = parent.parent)
            if (playedFrame.TryGetValue(parent.gameObject, out int parentFrame) && parentFrame == Time.frameCount)
                return;
#if UNITY_EDITOR
        if (SinkOverride == null && AudioManager.Instance == null) return;
#else
        if (AudioManager.Instance == null) return;
#endif
        playedFrame[source] = Time.frameCount;
        MouseClickSfx.NotifyPopupSoundPlayed();
        Emit(IsUpgradeResult(source) ? "Association_Build" : "Popup");
    }

    private static bool IsUpgradeResult(GameObject source)
    {
        return string.Equals(source.name.Replace("(Clone)", string.Empty).Trim(),
            "Modal_HQLobby_UpgradeResult", StringComparison.OrdinalIgnoreCase);
    }

    public static void PlayChatBubble()
    {
        MouseClickSfx.NotifyChatBubbleCreated();
        Emit("Explore_TextBubbleNPC");
    }
    public static void Forget(GameObject source)
    {
        if (source != null) playedFrame.Remove(source);
    }

    private static void Emit(string key)
    {
#if UNITY_EDITOR
        if (SinkOverride != null) { SinkOverride(key); return; }
#endif
        AudioManager.Instance?.PlaySfx(key);
    }
}
