using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum DHGameEndResult
{
    Clear,
    GameOver
}

public class DHGameEndUIController : MonoBehaviour
{
    [Header("Result UI")]
    [SerializeField] private GameObject clearImageObject;
    [SerializeField] private GameObject gameOverImageObject;
    [SerializeField] private CanvasGroup inputBlocker;
    [SerializeField] private Graphic inputBlockerGraphic;

    [Header("Flow")]
    [SerializeField] private float titleDelaySeconds = 10f;
    [SerializeField] private bool pauseTimeDuringDisplay = true;
    [SerializeField] private string fallbackTitleSceneName = "TitleScene";

    private Coroutine runningSequence;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        ResolveInputBlockerGraphic();
        HideAll();
    }

    public void ShowResult(DHGameEndResult result)
    {
        if (runningSequence != null)
            return;

        runningSequence = StartCoroutine(RunResultSequence(result));
    }

    private IEnumerator RunResultSequence(DHGameEndResult result)
    {
        ShowResultImage(result);
        SetInputBlocked(true);

        previousTimeScale = Time.timeScale;
        if (pauseTimeDuringDisplay)
            Time.timeScale = 0f;

        float waitUntil = Time.unscaledTime + Mathf.Max(0f, titleDelaySeconds);
        while (Time.unscaledTime < waitUntil)
            yield return null;

        // [JC 260617] 엔딩→타이틀 복귀 시에도 전체 영속 초기화(JC 매니저 포함). GameManager 없으면 DH만 리셋 폴백.
        if (GameManager.Instance != null) GameManager.Instance.ResetForNewGame();
        else DHGameProgressResetService.ResetDHProgress();

        if (pauseTimeDuringDisplay)
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;

        LoadTitleScene();
    }

    private void ShowResultImage(DHGameEndResult result)
    {
        if (clearImageObject != null)
            clearImageObject.SetActive(result == DHGameEndResult.Clear);

        if (gameOverImageObject != null)
            gameOverImageObject.SetActive(result == DHGameEndResult.GameOver);
    }

    private void HideAll()
    {
        if (clearImageObject != null)
            clearImageObject.SetActive(false);

        if (gameOverImageObject != null)
            gameOverImageObject.SetActive(false);

        SetInputBlocked(false);
    }

    private void SetInputBlocked(bool blocked)
    {
        if (inputBlocker == null)
            return;

        inputBlocker.alpha = blocked ? 1f : 0f;
        inputBlocker.interactable = blocked;
        inputBlocker.blocksRaycasts = blocked;

        if (inputBlockerGraphic != null)
        {
            inputBlockerGraphic.raycastTarget = blocked;
            inputBlockerGraphic.gameObject.SetActive(blocked);
        }
    }

    private void ResolveInputBlockerGraphic()
    {
        if (inputBlockerGraphic != null || inputBlocker == null)
            return;

        inputBlockerGraphic = inputBlocker.GetComponent<Graphic>();
        if (inputBlockerGraphic == null)
            inputBlockerGraphic = inputBlocker.GetComponentInChildren<Graphic>(true);
    }

    private void LoadTitleScene()
    {
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadTitle();
            return;
        }

        if (!string.IsNullOrWhiteSpace(fallbackTitleSceneName))
            SceneManager.LoadScene(fallbackTitleSceneName);
    }
}
