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
///   FadeToScene(name, outDur, inDur) — Out과 비동기 로드 병행 → 활성화 → 준비 후 In
/// </summary>
[DisallowMultipleComponent]
public class SceneFadeController : MonoBehaviour
{
    public static SceneFadeController Instance { get; private set; }

    private SceneLoadingLabel loadingLabel;
    private bool suppressCursor, cursorWasVisible, showingLoading;
    public static bool HideCursorDuringTransition => Instance != null && Instance.suppressCursor;
    private void SetLoading(bool value)
    {
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
    private LoadSceneMode loadingMode;
    private Coroutine entryRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneFade()
    {
        SceneManager.sceneLoaded -= EnsureSceneFade;
        SceneManager.sceneLoaded += EnsureSceneFade;
    }

    private static SceneFadeController EnsureInstance()
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

    // AsyncOperation을 유지하되, 검정 화면이 된 후에만 새 씬을 활성화한다.
    public static AsyncOperation LoadSceneAsyncWithFade(string sceneName, LoadSceneMode mode = LoadSceneMode.Single,
        float fadeOutDuration = 1f, float fadeInDuration = 1f)
    {
        return EnsureInstance().BeginSceneLoad(sceneName, mode, fadeOutDuration, fadeInDuration);
    }

    private AsyncOperation BeginSceneLoad(string sceneName, LoadSceneMode mode, float outDuration, float inDuration)
    {
        if (!CanLoad(sceneName)) return null;
        if (transitioning)
        {
            // 다른 전환의 핸들을 반환해 호출부가 성공으로 오인하지 않도록 한다.
            if (string.Equals(loadingSceneName, sceneName, StringComparison.OrdinalIgnoreCase) && loadingMode == mode)
                return loadingOperation;
            Debug.LogWarning($"Scene transition already in progress: {loadingSceneName} → request ignored: {sceneName}");
            return null;
        }
        // Boot를 거치지 않은 직접 씬 실행도 지원한다. 활성화 대기 중 새 ResourceRequest를
        // 기다리면 Unity 비동기 큐가 막힐 수 있으므로 반드시 씬 로드 시작 전에 준비한다.
        Sprites.Library.PrepareCommonUI();
        AsyncOperation operation;
        try { operation = SceneManager.LoadSceneAsync(sceneName, mode); }
        catch (Exception exception) { Debug.LogException(exception); return null; }
        if (operation == null) return null;
        operation.allowSceneActivation = false;
        if (entryRoutine != null) { StopCoroutine(entryRoutine); entryRoutine = null; }
        EnsureOverlay();
        AudioManager.Instance?.PrepareSceneBgmTransition(sceneName);
        loadingOperation = operation;
        loadingSceneName = sceneName;
        loadingMode = mode;
        pendingFadeInDuration = inDuration > 0f ? inDuration : 1f;
        transitioning = true;
        SetLoading(true);
        StartCoroutine(ActivateAfterFade(operation, outDuration > 0f ? outDuration : 1f));
        return operation;
    }

    private IEnumerator ActivateAfterFade(AsyncOperation operation, float duration)
    {
        // 읽기는 이미 시작했다. 암전 상태라면 화면을 다시 밝히지 않는다.
        if (overlay.color.a < 0.999f) yield return FadeOut(duration);
        else
        {
            if (activeRoutine != null) { StopCoroutine(activeRoutine); activeRoutine = null; }
            overlay.color = Color.black;
            overlay.raycastTarget = true;
        }
        pendingFadeInOnSceneLoad = true;
        operation.allowSceneActivation = true;
    }

    private static bool CanLoad(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;
        if (Application.CanStreamedLevelBeLoaded(sceneName)) return true;
        Debug.LogError($"Cannot load scene: {sceneName}");
        return false;
    }

    /// <summary>[KJ 261002] 페이드아웃 완료 후 씬 활성화를 기다리는 중인지. GameSceneManager 중복 페이드 방지용.</summary>
    public bool IsSceneLoadPending => pendingFadeInOnSceneLoad;
    public bool IsTransitioning => transitioning || activeRoutine != null || entryRoutine != null;

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
    }
    private void OnDisable()
    {
        Canvas.willRenderCanvases -= BeforeCanvasRender;
        if (suppressCursor) SetLoading(false);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void OnActiveSceneChanged(Scene previous, Scene current)
    {
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
        EnsureOverlay();
        SetLoading(true);
        return StartFade(overlay.color.a, 1f, duration, true, onComplete);
    }

    public Coroutine FadeIn(float duration, Action onComplete = null, float holdDuration = 0f)
    {
        // 엔딩처럼 씬이 자체 진입 연출을 요청하면 자동 진입 페이드를 대체한다.
        if (entryRoutine != null) { StopCoroutine(entryRoutine); entryRoutine = null; }
        EnsureOverlay();
        return StartFade(1f, 0f, duration, true, onComplete, holdDuration);
    }

    public void FadeToScene(string sceneName, float fadeOutDuration, float fadeInDuration)
    {
        BeginSceneLoad(sceneName, LoadSceneMode.Single, fadeOutDuration, fadeInDuration);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (loadingOperation != null &&
            !string.Equals(scene.name, loadingSceneName, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(scene.path, loadingSceneName, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(scene.path, loadingSceneName + ".unity", StringComparison.OrdinalIgnoreCase)) return;
        if (mode == LoadSceneMode.Additive)
        {
            if (!pendingFadeInOnSceneLoad) return;
            pendingFadeInOnSceneLoad = false;
            loadingOperation = null;
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
        transitioning = false;
        // GameLoadGate가 이어서 Additive 로드·저장 복원을 진행할 때는 암전을 유지한다.
        string gateScene = GameSceneManager.Instance != null ? GameSceneManager.Instance.GameLoadScene : "GameLoadScene";
        if (scene.name == gateScene)
        {
            if (activeRoutine != null) { StopCoroutine(activeRoutine); activeRoutine = null; }
            EnsureOverlay();
            overlay.color = Color.black;
            overlay.raycastTarget = true;
            return;
        }
        PlaySceneEntry(scene, duration);
    }

    private void PlaySceneEntry(Scene scene, float duration)
    {
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
            stableFrames = ready ? stableFrames + 1 : 0;
            yield return null;
        }
        Canvas.ForceUpdateCanvases();
        yield return null;
    }

    private Coroutine StartFade(float from, float to, float duration, bool block, Action onComplete, float holdDuration = 0f)
    {
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(FadeRoutine(from, to, Mathf.Max(0.01f, duration), block, onComplete, holdDuration));
        return activeRoutine;
    }

    private IEnumerator FadeRoutine(float from, float to, float duration, bool blockRaycast, Action onComplete, float holdDuration = 0f)
    {
        if (to < .5f)
        {
            SetLoading(true);
            overlay.color = Color.black;
            overlay.raycastTarget = true;
            yield return WaitForSceneReady();
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
