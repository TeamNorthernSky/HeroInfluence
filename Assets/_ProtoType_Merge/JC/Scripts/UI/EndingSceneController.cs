using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 신설 260512] Ending_Victory / Ending_Defeat 공통 컨트롤러.
/// 흐름: 페이드 인(2초) → 2초 대기 → pressLeftBtn 활성 + 좌클릭 감지 시작
///       → 좌클릭 → 페이드 아웃(2초) + TitleScene 로드 → 페이드 인(1초)
/// 배경: backgroundSprite 인스펙터 할당 시 Canvas 첫 자식으로 풀스크린 Image 자동 생성.
/// </summary>
[DisallowMultipleComponent]
public class EndingSceneController : MonoBehaviour
{
    [Header("Background (auto-created child)")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private bool backgroundPreserveAspect = false;

    [Header("Press to title")]
    [SerializeField] private GameObject pressLeftBtnSprite;

    [Header("Timing")]
    [SerializeField] private float fadeInDuration = 2f;
    [SerializeField] private float waitBeforePressDuration = 2f;
    [SerializeField] private float fadeOutDuration = 2f;
    [SerializeField] private float titleFadeInDuration = 1f;
    [SerializeField] private string titleSceneName = "TitleScene";

    private bool inputEnabled;
    private bool exitTriggered;

    private void Start()
    {
        EnsureBackgroundImage();
        if (pressLeftBtnSprite != null) pressLeftBtnSprite.SetActive(false);
        StartCoroutine(EnterRoutine());
    }

    private void EnsureBackgroundImage()
    {
        if (backgroundSprite == null) return;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        Transform existing = null;
        foreach (Transform c in canvas.transform)
            if (c.gameObject.name == "EndingBackground") { existing = c; break; }
        if (existing != null) return;

        var bgGo = new GameObject("EndingBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(canvas.transform, false);
        bgGo.transform.SetAsFirstSibling();

        var img = bgGo.GetComponent<Image>();
        img.sprite = backgroundSprite;
        img.preserveAspect = backgroundPreserveAspect;
        img.raycastTarget = false;

        var rt = bgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private IEnumerator EnterRoutine()
    {
        var fade = SceneFadeController.Instance;
        if (fade != null) yield return fade.FadeIn(fadeInDuration);

        yield return new WaitForSecondsRealtime(waitBeforePressDuration);

        if (pressLeftBtnSprite != null) pressLeftBtnSprite.SetActive(true);
        inputEnabled = true;
    }

    private void Update()
    {
        if (!inputEnabled || exitTriggered) return;
        if (Input.GetMouseButtonDown(0))
        {
            exitTriggered = true;
            inputEnabled = false;

            var fade = SceneFadeController.Instance;
            if (fade != null)
                fade.FadeToScene(titleSceneName, fadeOutDuration, titleFadeInDuration);
            else
                // [JC 260514] GameSceneManager 컴포넌트 격상 — Instance 경유 호출.
                if (GameSceneManager.Instance != null)
                    GameSceneManager.Instance.LoadScene(titleSceneName);
                else
                    UnityEngine.SceneManagement.SceneManager.LoadScene(titleSceneName);
        }
    }
}
