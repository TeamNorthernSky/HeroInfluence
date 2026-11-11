using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// [JC 신설 260512] 전체 화면 페이드 + 씬 전환 통합 컨트롤러.
/// GameManager 영속 자식으로 운영. 검정 Image 풀스크린 오버레이. sortOrder 9999.
/// API:
///   FadeOut(duration) — 알파 0→1
///   FadeIn(duration) — 알파 1→0
///   FadeToScene(name, outDur, inDur) — 로딩 화면 출력 → 준비 작업 → 비동기 로드 → 준비 후 In
/// </summary>
[DisallowMultipleComponent]
public class SceneFadeController : MonoBehaviour
{
    public static SceneFadeController Instance { get; private set; }

    private SceneLoadingLabel loadingLabel;
    private const float LoadingLeadInDuration = 2.5f;
    private const float LoadingLeadOutDuration = 2.5f;
    private bool loadingLeadInShown, loadingLeadOutShown;
    private bool suppressCursor, cursorWasVisible, showingLoading;
    // JC | 2026-11-10 | 입력 잠금은 로드 코루틴과 분리하여 중간 로딩씬·복원 대기에도 유지합니다.
    private bool inputBlocked;
    private int inputReleasedFrame = -1;
    // JC | 2026-11-10 | 실패 콜백에서 새 전환을 요청한 경우 이전 복구가 새 잠금을 풀지 않게 구분합니다.
    private int transitionVersion;
    public static bool IsInputBlocked => Instance != null &&
        (Instance.inputBlocked || Time.frameCount == Instance.inputReleasedFrame);
    public static bool HideCursorDuringTransition => Instance != null && Instance.suppressCursor;
    private void SetLoading(bool value)
    {
        if (value && !inputBlocked)
        {
            loadingLeadInShown = loadingLeadOutShown = false;
            if (loadingLabel != null) loadingLabel.BeginPresentation();
        }
        // JC | 2026-11-10 | 페이드인 완료 프레임의 클릭까지 버리고 다음 프레임부터 새 입력을 받습니다.
        if (inputBlocked && !value) inputReleasedFrame = Time.frameCount;
        inputBlocked = value;
        if (value && !suppressCursor) cursorWasVisible = Cursor.visible || FakeMouseCursor.HasActivePresenter;
        suppressCursor = value;
        showingLoading = value;
        if (!value)
        {
            if (loadingLabel != null) loadingLabel.gameObject.SetActive(false);
            Cursor.visible = cursorWasVisible;
        }
    }
    private void Update()
    {
        if (loadingLabel != null) loadingLabel.gameObject.SetActive(showingLoading && overlay != null && overlay.color.a > .98f);
        // The final input-protection frame has ended. Notify once instead of making chat poll.
        if (!inputBlocked && inputReleasedFrame >= 0 && Time.frameCount != inputReleasedFrame)
        {
            inputReleasedFrame = -1;
            TryNotifySceneEntryCompleted();
        }
    }
    private void BeforeCanvasRender()
    {
        if (suppressCursor && Application.isFocused && JcPointerInput.Inside) Cursor.visible = false;
    }
    private Canvas canvas;
    private Image overlay;
    private Coroutine activeRoutine;
    private float pendingFadeInDuration;
    private bool pendingFadeInOnSceneLoad;
    private int waitingForActivationHandle;
    private bool transitioning;
    private AsyncOperation loadingOperation;
    private string loadingSceneName;
    private Coroutine entryRoutine;
    private SceneLoadRequest loadRequest;
    private GameObject selectionBeforeLoad;
    public static event Action<Scene> TransitionFailed;
    public static event Action<Scene> SceneEntryCompleted;
    private Scene pendingCompletedEntry;
    private Scene completedEntry;

    public bool IsSceneEntryCompleted(Scene scene)
    {
        return scene.IsValid() && scene.isLoaded && scene == SceneManager.GetActiveScene() &&
            scene == completedEntry && !IsTransitioning;
    }

    private void ResetSceneEntryCompletion()
    {
        pendingCompletedEntry = default;
        completedEntry = default;
    }

    // Unity AsyncOperation은 실제 로드 시작 전에 만들 수 없습니다. 대기 호출부에는
    // 준비 구간까지 포함하는 핸들을 반환하며, 씬 활성화 완료 시 기존처럼 대기가 끝납니다.
    public sealed class SceneLoadRequest : CustomYieldInstruction
    {
        internal AsyncOperation operation;
        public bool Failed { get; internal set; }
        public bool isDone => Failed || (operation != null && operation.isDone);
        public override bool keepWaiting => !isDone;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTransitionCallbacks()
    {
        TransitionFailed = null;
        SceneEntryCompleted = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneFade()
    {
        SceneManager.sceneLoaded -= EnsureSceneFade;
        SceneManager.sceneLoaded += EnsureSceneFade;
    }

    public static SceneFadeController EnsureInstance()
    {
        if (Instance != null) return Instance;
        var root = new GameObject("SceneFadeController");
        DontDestroyOnLoad(root);
        return root.AddComponent<SceneFadeController>();
    }

    private static void EnsureSceneFade(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single && Instance == null)
            EnsureInstance().OnSceneLoaded(scene, mode);
    }

    public static void LoadSceneWithFadeIfNeeded(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        LoadSceneAsyncWithFade(sceneName, mode);
    }

    // prepare는 로딩 화면을 렌더한 뒤, 기존 씬을 유지한 채 실행합니다.
    // false/예외면 씬을 로드하지 않고 onFailed와 입력/화면 복구를 수행합니다.
    public static SceneLoadRequest LoadSceneAsyncWithFade(string sceneName, LoadSceneMode mode = LoadSceneMode.Single,
        float fadeOutDuration = 1f, float fadeInDuration = 1f,
        Func<bool> prepare = null, Action onFailed = null)
    {
        return EnsureInstance().BeginSceneLoad(sceneName, mode, fadeOutDuration, fadeInDuration, prepare, onFailed);
    }

    private SceneLoadRequest BeginSceneLoad(string sceneName, LoadSceneMode mode, float outDuration, float inDuration,
        Func<bool> prepare = null, Action onFailed = null)
    {
        // 준비 콜백이 다른 요청의 컨텍스트를 덮어쓰지 않도록 중복 요청은 모두 거절합니다.
        if (transitioning)
        {
            Debug.LogWarning($"Scene transition already in progress: {loadingSceneName} → request ignored: {sceneName}");
            return null;
        }
        // JC | 2026-11-10 | 잘못된 목적지 때문에 선행 FadeOut/로딩 화면의 잠금이 남지 않게 복구합니다.
        // 진행 중인 정상 로드의 중복 요청은 위에서 거절하므로 그 잠금을 해제하지 않습니다.
        if (!CanLoad(sceneName))
        {
            RecoverFailedTransition(SceneManager.GetActiveScene(), onFailed);
            return null;
        }
        ++transitionVersion;
        ResetSceneEntryCompletion();
        if (entryRoutine != null) { StopCoroutine(entryRoutine); entryRoutine = null; }
        if (activeRoutine != null) { StopCoroutine(activeRoutine); activeRoutine = null; }
        EnsureOverlay();
        // JC | 2026-11-10 | 요청을 수락한 즉시 잠가 같은 프레임의 후속 입력도 차단합니다.
        SetLoading(true);
        AudioManager.Instance?.PrepareSceneBgmTransition(sceneName);
        var request = new SceneLoadRequest();
        loadRequest = request;
        loadingSceneName = sceneName;
        pendingFadeInDuration = inDuration > 0f ? inDuration : 1f;
        transitioning = true;
        AudioManager.Instance?.PlaySfx("ChangeScene");
        var events = UnityEngine.EventSystems.EventSystem.current;
        selectionBeforeLoad = events != null ? events.currentSelectedGameObject : null;
        if (events != null) events.SetSelectedGameObject(null);
        overlay.raycastTarget = true;
        SetLoading(true);
        StartCoroutine(LoadAfterPresentation(request, SceneManager.GetActiveScene(), sceneName, mode,
            outDuration > 0f ? outDuration : 1f, prepare, onFailed));
        return request;
    }

    // ForceUpdateCanvases만으로는 화면에 출력되지 않습니다. 검정 화면과 문구를 설정한 뒤
    // 두 프레임을 양보해 호출 위치와 관계없이 렌더링 기회를 보장합니다.
    // GameView 비표시 상태에서도 진행할 수 있도록 WaitForEndOfFrame 대신 프레임 대기를 사용합니다.
    public static IEnumerator PresentLoadingScreen(float duration = 0f)
    {
        var fade = EnsureInstance();
        fade.ResetSceneEntryCompletion();
        fade.EnsureOverlay();
        // JC | 2026-11-10 | 첫 yield 이전부터 잠그며 GameLoadScene 경유 중에도 해제하지 않습니다.
        fade.SetLoading(true);
        if (fade.entryRoutine != null) { fade.StopCoroutine(fade.entryRoutine); fade.entryRoutine = null; }
        if (duration > 0f && fade.overlay.color.a < .999f) yield return fade.FadeOut(duration);
        else
        {
            if (fade.activeRoutine != null) { fade.StopCoroutine(fade.activeRoutine); fade.activeRoutine = null; }
            fade.overlay.color = Color.black;
        }
        fade.overlay.raycastTarget = true;
        fade.SetLoading(true);
        fade.loadingLabel.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return null;
        // Boot and GameLoadGate may present the same loading session again; hold only once.
        if (!fade.loadingLeadInShown)
        {
            fade.loadingLeadInShown = true;
            yield return fade.HoldLoadingMotion(LoadingLeadInDuration);
        }
        // 선행 연출이 끝난 뒤 실제 리소스 준비와 씬 로드 중에는 현재 프레임을 유지한다.
        fade.loadingLabel.SetMotionPaused(true);
    }

    private IEnumerator HoldLoadingMotion(float duration)
    {
        loadingLabel.SetMotionPaused(false);
        loadingLabel.gameObject.SetActive(true);
        loadingLabel.RestartMotion();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            // Count visible animation time instead of letting a blocked frame consume the hold.
            elapsed += Mathf.Min(Time.unscaledDeltaTime, .05f);
        }
    }

    private IEnumerator LoadAfterPresentation(SceneLoadRequest request, Scene sourceScene, string sceneName,
        LoadSceneMode mode, float duration, Func<bool> prepare, Action onFailed)
    {
        yield return PresentLoadingScreen(duration);
        Exception failure = null;
        try
        {
            if (prepare != null && !prepare()) throw new InvalidOperationException("Scene preparation failed: " + sceneName);
            // 비동기 씬 큐가 시작되기 전에 리소스 준비를 마칩니다.
            Sprites.Library.PrepareCommonUI();
            AudioManager.Instance?.UpdateSceneBgmFade(0f);
            pendingFadeInOnSceneLoad = true;
            loadingOperation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (loadingOperation == null) throw new InvalidOperationException("Scene load failed: " + sceneName);
            request.operation = loadingOperation;
        }
        catch (Exception exception) { failure = exception; }
        if (failure != null)
        {
            Debug.LogException(failure);
            request.Failed = true;
            // JC | 2026-11-10 | 준비 실패와 로드 예외를 같은 복구 경로로 보내 잠금 해제를 보장합니다.
            RecoverFailedTransition(sourceScene, onFailed);
        }
    }

    // JC | 2026-11-10 | 기존 실패 통지와 화면 복구를 재사용하며 실패 상태의 준비 완료를 기다리지 않습니다.
    private void RecoverFailedTransition(Scene sourceScene, Action onFailed)
    {
        ResetSceneEntryCompletion();
        int failedVersion = transitionVersion;
        if (entryRoutine != null) { StopCoroutine(entryRoutine); entryRoutine = null; }
        if (activeRoutine != null) { StopCoroutine(activeRoutine); activeRoutine = null; }
        loadingOperation = null;
        loadRequest = null;
        pendingFadeInOnSceneLoad = false;
        waitingForActivationHandle = 0;
        transitioning = false;
        try { onFailed?.Invoke(); }
        catch (Exception exception) { Debug.LogException(exception); }
        if (transitionVersion != failedVersion) return;
        try { TransitionFailed?.Invoke(sourceScene); }
        catch (Exception exception) { Debug.LogException(exception); }
        if (transitionVersion != failedVersion || !inputBlocked) return;
        // 실패한 준비 상태를 WaitForSceneReady로 기다리면 영구 암전될 수 있습니다.
        activeRoutine = StartCoroutine(FadeRoutine(overlay.color.a, 0f, .2f, true, () =>
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events != null && selectionBeforeLoad != null && selectionBeforeLoad.activeInHierarchy)
                events.SetSelectedGameObject(selectionBeforeLoad);
            selectionBeforeLoad = null;
        }, waitForReady: false));
    }

    private static bool CanLoad(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;
        if (Application.CanStreamedLevelBeLoaded(sceneName)) return true;
        Debug.LogError($"Cannot load scene: {sceneName}");
        return false;
    }

    /// <summary>[KJ 261002] 페이드아웃 완료 후 씬 활성화를 기다리는 중인지. GameSceneManager 중복 페이드 방지용.</summary>
    public bool IsSceneLoadPending => transitioning || pendingFadeInOnSceneLoad;
    // JC | 2026-11-10 | 기존 메뉴의 전환 검사에도 암전 유지 구간과 해제 프레임을 포함합니다.
    public bool IsTransitioning => IsInputBlocked || transitioning || activeRoutine != null || entryRoutine != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnsureOverlay();
    }

    private void OnEnable()
    {
        Canvas.willRenderCanvases += BeforeCanvasRender;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    private void OnDisable()
    {
        Canvas.willRenderCanvases -= BeforeCanvasRender;
        if (suppressCursor) SetLoading(false);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        ResetSceneEntryCompletion();
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void OnActiveSceneChanged(Scene previous, Scene current)
    {
        ResetSceneEntryCompletion();
        if (waitingForActivationHandle == 0 || current.handle != waitingForActivationHandle) return;
        waitingForActivationHandle = 0;
        transitioning = false;
        PlaySceneEntry(current, pendingFadeInDuration);
    }

    private void EnsureOverlay()
    {
        if (overlay != null) return;

        var canvasGo = new GameObject("SceneFadeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // 페이드 중에는 클릭 차단을 위해 raycaster는 활성. 단 오버레이 알파 0일 때는 굳이 차단 필요 없으나 단순화 위해 그대로.

        var imgGo = new GameObject("FadeImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imgGo.transform.SetParent(canvasGo.transform, false);
        overlay = imgGo.GetComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0f);
        overlay.raycastTarget = false;

        var rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var labelObject = new GameObject("Loading label", typeof(RectTransform), typeof(SceneLoadingLabel));
        loadingLabel = labelObject.GetComponent<SceneLoadingLabel>();
        loadingLabel.Initialize(canvasGo.transform);
        labelObject.SetActive(false);
    }

    public Coroutine FadeOut(float duration, Action onComplete = null)
    {
        ResetSceneEntryCompletion();
        EnsureOverlay();
        SetLoading(true);
        return StartFade(overlay.color.a, 1f, duration, true, onComplete);
    }

    public Coroutine FadeIn(float duration, Action onComplete = null, float holdDuration = 0f)
    {
        ResetSceneEntryCompletion();
        Scene scene = SceneManager.GetActiveScene();
        int version = transitionVersion;
        // 엔딩처럼 씬이 자체 진입 연출을 요청하면 자동 진입 페이드를 대체한다.
        if (entryRoutine != null) { StopCoroutine(entryRoutine); entryRoutine = null; }
        EnsureOverlay();
        return StartFade(1f, 0f, duration, true, () =>
        {
            onComplete?.Invoke();
            if (version == transitionVersion) CompleteSceneEntry(scene);
        }, holdDuration);
    }

    public void FadeToScene(string sceneName, float fadeOutDuration, float fadeInDuration)
    {
        BeginSceneLoad(sceneName, LoadSceneMode.Single, fadeOutDuration, fadeInDuration);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (loadRequest != null &&
            !string.Equals(scene.name, loadingSceneName, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(scene.path, loadingSceneName, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(scene.path, loadingSceneName + ".unity", StringComparison.OrdinalIgnoreCase)) return;
        selectionBeforeLoad = null;
        if (mode == LoadSceneMode.Additive)
        {
            if (!pendingFadeInOnSceneLoad) return;
            pendingFadeInOnSceneLoad = false;
            loadingOperation = null;
            loadRequest = null;
            if (SceneManager.GetActiveScene() != scene)
            {
                waitingForActivationHandle = scene.handle;
                return; // GameLoadGate의 복원이 끝나 active scene이 될 때까지 검정 유지.
            }
            transitioning = false;
            PlaySceneEntry(scene, pendingFadeInDuration);
            return;
        }
        float duration = pendingFadeInOnSceneLoad ? pendingFadeInDuration : 1f;
        pendingFadeInOnSceneLoad = false;
        waitingForActivationHandle = 0;
        loadingOperation = null;
        loadRequest = null;
        transitioning = false;
        // GameLoadGate가 이어서 Additive 로드·저장 복원을 진행할 때는 암전을 유지한다.
        string gateScene = GameSceneManager.Instance != null ? GameSceneManager.Instance.GameLoadScene : "GameLoadScene";
        if (scene.name == gateScene)
        {
            if (activeRoutine != null) { StopCoroutine(activeRoutine); activeRoutine = null; }
            EnsureOverlay();
            // JC | 2026-11-10 | 로딩씬을 직접 시작한 경우도 후속 Additive 로드 완료까지 입력을 막습니다.
            SetLoading(true);
            overlay.color = Color.black;
            overlay.raycastTarget = true;
            return;
        }
        PlaySceneEntry(scene, duration);
    }

    private void PlaySceneEntry(Scene scene, float duration)
    {
        ResetSceneEntryCompletion();
        if (entryRoutine != null) StopCoroutine(entryRoutine);
        entryRoutine = StartCoroutine(FadeInAfterStart(scene, duration));
    }

    private IEnumerator FadeInAfterStart(Scene scene, float duration)
    {
        EnsureOverlay();
        overlay.color = Color.black;
        overlay.raycastTarget = true;
        SetLoading(true);
        // sceneLoaded는 Start 이전이다. 초기 배치와 Additive 복원이 끝난 다음 화면을 연다.
        yield return null;
        if (scene.name == TmpBattleSceneFade.SceneName)
            yield return StartFade(1f, 0f, 1f, true, null, 1f);
        else
            yield return StartFade(1f, 0f, duration, true, null);
        entryRoutine = null;
        CompleteSceneEntry(scene);
    }

    private void CompleteSceneEntry(Scene scene)
    {
        // Keep completion pending during the release frame; Update publishes after input unlocks.
        if (!scene.IsValid() || !scene.isLoaded || scene != SceneManager.GetActiveScene() ||
            inputBlocked || transitioning || activeRoutine != null || entryRoutine != null)
            return;

        pendingCompletedEntry = scene;
        TryNotifySceneEntryCompleted();
    }

    private void OnSceneUnloaded(Scene scene) => TryNotifySceneEntryCompleted();

    private void TryNotifySceneEntryCompleted()
    {
        Scene scene = pendingCompletedEntry;
        if (!scene.IsValid() || !scene.isLoaded || scene != SceneManager.GetActiveScene() || IsTransitioning)
            return;

        string gateScene = GameSceneManager.Instance != null ? GameSceneManager.Instance.GameLoadScene : "GameLoadScene";
        if (SceneManager.GetSceneByName(gateScene).IsValid() || DHGameStateRestoreService.IsRestoring ||
            (GameManager.Instance != null && GameManager.Instance.IsExplorationEntryPending))
            return;

        // Consume before notifying so another unload cannot publish the same completion twice.
        pendingCompletedEntry = default;
        completedEntry = scene;
        SceneEntryCompleted?.Invoke(scene);
    }

    private IEnumerator WaitForSceneReady()
    {
        // Start와 씬 복귀 코루틴이 실행된 뒤 실제 복원 완료를 확인한다.
        yield return null;
        int stableFrames = 0;
        while (stableFrames < 2)
        {
            var scene = SceneManager.GetActiveScene();
            bool ready = scene.IsValid() && scene.isLoaded && waitingForActivationHandle == 0 &&
                (loadingOperation == null || loadingOperation.isDone) && !DHGameStateRestoreService.IsRestoring &&
                (GameManager.Instance == null || !GameManager.Instance.IsExplorationEntryPending);
            foreach (var party in FindObjectsByType<TutorialPartyRuntime>(FindObjectsSortMode.None))
                if (party.gameObject.scene == scene && party.isActiveAndEnabled && !party.SceneEntryReady) ready = false;
            foreach (var guide in FindObjectsByType<TutorialExploreUIController>(FindObjectsSortMode.None))
                if (guide.gameObject.scene == scene && guide.isActiveAndEnabled && !guide.SceneEntryReady) ready = false;
            // JC | 2026-11-11 | 에디터에서도 같은 준비 완료 조건을 기다려 전투 시작 전 로딩 해제를 막습니다.
            if (BattleEffectWarmup.IsPreparing) ready = false;
            foreach (var battle in FindObjectsByType<BattleSceneManager>(FindObjectsSortMode.None))
                if (battle.gameObject.scene == scene && battle.IsVisualPreparationPending) ready = false;
            stableFrames = ready ? stableFrames + 1 : 0;
            yield return null;
        }
        // JC | 2026-11-11 | GameLoadGate를 거치지 않는 튜토리얼도 두 실행 환경에서 같은 로딩 중 준비를 수행합니다.
        if (SceneManager.GetActiveScene().name == "TutorialExploreScene")
            yield return BattleEffectWarmup.PrepareParty();
        Canvas.ForceUpdateCanvases();
        yield return null;
    }

    private Coroutine StartFade(float from, float to, float duration, bool block, Action onComplete, float holdDuration = 0f)
    {
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(FadeRoutine(from, to, Mathf.Max(0.01f, duration), block, onComplete, holdDuration));
        return activeRoutine;
    }

    private IEnumerator FadeRoutine(float from, float to, float duration, bool blockRaycast, Action onComplete, float holdDuration = 0f, bool waitForReady = true)
    {
        if (to < .5f)
        {
            SetLoading(true);
            overlay.color = Color.black;
            overlay.raycastTarget = true;
            if (waitForReady)
            {
                loadingLabel.SetMotionPaused(true);
                yield return WaitForSceneReady();
                if (!loadingLeadOutShown)
                {
                    loadingLeadOutShown = true;
                    yield return HoldLoadingMotion(Mathf.Max(LoadingLeadOutDuration, holdDuration));
                    holdDuration = 0f;
                }
            }
        }
        overlay.color = new Color(0f, 0f, 0f, from);
        AudioManager.Instance?.UpdateSceneBgmFade(1f - from);
        if (holdDuration > 0f)
        {
            overlay.raycastTarget = true;
            yield return new WaitForSecondsRealtime(holdDuration);
        }
        if (to < .5f)
        {
            showingLoading = false;
            if (loadingLabel != null) loadingLabel.gameObject.SetActive(false);
        }
        overlay.raycastTarget = blockRaycast || to > 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            overlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t));
            AudioManager.Instance?.UpdateSceneBgmFade(1f - overlay.color.a);
            yield return null;
        }
        overlay.color = new Color(0f, 0f, 0f, to);
        AudioManager.Instance?.UpdateSceneBgmFade(1f - to, completed: to < 0.5f);
        overlay.raycastTarget = to > 0.5f;
        activeRoutine = null;
        if (to < .5f) SetLoading(false);
        onComplete?.Invoke();
    }
}
