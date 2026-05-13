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

    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

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

    public Coroutine FadeIn(float duration, Action onComplete = null)
    {
        EnsureOverlay();
        return StartFade(1f, 0f, duration, false, onComplete);
    }

    public void FadeToScene(string sceneName, float fadeOutDuration, float fadeInDuration)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;
        EnsureOverlay();
        StartCoroutine(FadeToSceneRoutine(sceneName, fadeOutDuration, fadeInDuration));
    }

    private IEnumerator FadeToSceneRoutine(string sceneName, float outDur, float inDur)
    {
        yield return FadeOut(outDur);
        pendingFadeInDuration = inDur;
        pendingFadeInOnSceneLoad = true;
        GameSceneManager.LoadScene(sceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!pendingFadeInOnSceneLoad) return;
        pendingFadeInOnSceneLoad = false;
        FadeIn(pendingFadeInDuration);
    }

    private Coroutine StartFade(float from, float to, float duration, bool block, Action onComplete)
    {
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(FadeRoutine(from, to, Mathf.Max(0.01f, duration), block, onComplete));
        return activeRoutine;
    }

    private IEnumerator FadeRoutine(float from, float to, float duration, bool blockRaycast, Action onComplete)
    {
        overlay.raycastTarget = blockRaycast || to > 0.5f;
        overlay.color = new Color(0f, 0f, 0f, from);
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
