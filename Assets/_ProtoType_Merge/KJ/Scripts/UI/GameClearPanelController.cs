using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>클리어 화면 클릭 시 영속 게임 상태를 초기화한 뒤 엔딩으로 이동한다.</summary>
[DisallowMultipleComponent]
public sealed class GameClearPanelController : MonoBehaviour
{
    [SerializeField] private string endingSceneName = "Ending CutScene";

    private int openedFrame;
    private bool transitioning;

    private void OnEnable()
    {
        openedFrame = Time.frameCount;
        transitioning = false;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) ContinueToEnding();
    }

    public void ContinueToEnding()
    {
        if (!isActiveAndEnabled || transitioning || Time.frameCount <= openedFrame) return;

        // 이동할 수 없는 씬이면 현재 게임 상태를 먼저 지우지 않는다.
        if (string.IsNullOrWhiteSpace(endingSceneName) ||
            !Application.CanStreamedLevelBeLoaded(endingSceneName))
        {
            Debug.LogError($"[GameClearPanel] 엔딩씬 빌드 등록을 확인해주세요: {endingSceneName}", this);
            return;
        }

        transitioning = true;
        try
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ResetForNewGame();
            else
                DHGameProgressResetService.ResetDHProgress();

            Time.timeScale = 1f;
            if (GameSceneManager.Instance != null)
                GameSceneManager.Instance.LoadScene(endingSceneName);
            else
                SceneManager.LoadScene(endingSceneName);
        }
        catch (Exception exception)
        {
            transitioning = false;
            Debug.LogException(exception, this);
        }
    }
}
