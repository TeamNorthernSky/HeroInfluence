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
///   FadeToScene(name, outDur, inDur) — Out → LoadScene → 새 씬 진입 후 In
/// </summary>
[DisallowMultipleComponent]
public class SceneFadeController : MonoBehaviour
{
    public static SceneFadeController Instance { get; private set; }

    private Canvas canvas;
    private Image overlay;
    private Coroutine activeRoutine;
    private float pendingFadeInDuration;
    private bool pendingFadeInOnSceneLoad;
    private int waitingForActivationHandle;
    private bool transitioning;
    private AsyncOperation loadingOperation;

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
        if (mode == LoadSceneMode.Single)
            EnsureInstance().FadeToScene(sceneName, 1f, 1f);
        else
            SceneManager.LoadScene(sceneName, mode);
    }

    // AsyncOperation을 유지하되, 검정 화면이 된 후에만 새 씬을 활성화한다.
    public static AsyncOperation LoadSceneAsyncWithFade(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        var fade = EnsureInstance();
        if (fade.transitioning) return fade.loadingOperation;
        if (!CanLoad(sceneName)) return null;
        var operation = SceneManager.LoadSceneAsync(sceneName, mode);
        if (operation == null) return null;
        operation.allowSceneActivation = false;
        fade.loadingOperation = operation;
        fade.transitioning = true;
        fade.StartCoroutine(fade.ActivateAfterFade(operation));
        return operation;
    }

    private IEnumerator ActivateAfterFade(AsyncOperation operation)
    {
        if (activeRoutine != null) yield return activeRoutine;
        yield return FadeOut(1f);
        pendingFadeInDuration = 1f;
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

    /// <summary>[KJ 261002] FadeToScene이 페이드아웃을 마치고 씬 로드를 요청 중인지. GameSceneManager 중복 페이드 방지용.</summary>
    public bool IsSceneLoadPending => pendingFadeInOnSceneLoad;

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
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }
    private void OnDisable()
    {
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
    }

    public Coroutine FadeOut(float duration, Action onComplete = null)
    {
        EnsureOverlay();
        return StartFade(0f, 1f, duration, true, onComplete);
    }

    public Coroutine FadeIn(float duration, Action onComplete = null, float holdDuration = 0f)
    {
        EnsureOverlay();
        return StartFade(1f, 0f, duration, false, onComplete, holdDuration);
    }

    public void FadeToScene(string sceneName, float fadeOutDuration, float fadeInDuration)
    {
        if (transitioning || !CanLoad(sceneName)) return;
        if (fadeOutDuration <= 0f) fadeOutDuration = 1f;
        if (fadeInDuration <= 0f) fadeInDuration = 1f;
        transitioning = true;
        EnsureOverlay();
        StartCoroutine(FadeToSceneRoutine(sceneName, fadeOutDuration, fadeInDuration));
    }

    private IEnumerator FadeToSceneRoutine(string sceneName, float outDur, float inDur)
    {
        if (activeRoutine != null) yield return activeRoutine;
        yield return FadeOut(outDur);
        pendingFadeInDuration = inDur;
        pendingFadeInOnSceneLoad = true;
        // 페이드가 끝난 뒤에는 전환 허브를 재호출하지 않는다.
        SceneManager.LoadScene(sceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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
        PlaySceneEntry(scene, duration);
    }

    private void PlaySceneEntry(Scene scene, float duration)
    {
        if (scene.name == TmpBattleSceneFade.SceneName)
            FadeIn(1f, holdDuration: 1f);
        else
            FadeIn(duration);
    }

    private Coroutine StartFade(float from, float to, float duration, bool block, Action onComplete, float holdDuration = 0f)
    {
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(FadeRoutine(from, to, Mathf.Max(0.01f, duration), block, onComplete, holdDuration));
        return activeRoutine;
    }

    private IEnumerator FadeRoutine(float from, float to, float duration, bool blockRaycast, Action onComplete, float holdDuration = 0f)
    {
        overlay.color = new Color(0f, 0f, 0f, from);
        if (holdDuration > 0f)
        {
            overlay.raycastTarget = true;
            yield return new WaitForSecondsRealtime(holdDuration);
        }
        overlay.raycastTarget = blockRaycast || to > 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            overlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t));
            yield return null;
        }
        overlay.color = new Color(0f, 0f, 0f, to);
        overlay.raycastTarget = to > 0.5f;
        activeRoutine = null;
        onComplete?.Invoke();
    }
}
