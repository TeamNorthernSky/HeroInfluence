using UnityEngine;
using UnityEngine.SceneManagement;

public enum DHGameEndResult
{
    Clear,
    GameOver
}

public class DHGameEndManager : MonoBehaviour
{
    private const string GameOverSfxKey = "Explore_Defeat";

    [Header("Panels")]
    [SerializeField] private GameObject clearPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Scene")]
    [SerializeField] private string gameOverCreditSceneName = "Credit Scene";
    [SerializeField] private string clearEndingSceneName = "Ending CutScene";

    private bool gameOverCreditsStarted;
    private bool clearEndingStarted;

    private void Awake()
    {
        HidePanels();
    }

    public void ShowResult(DHGameEndResult result)
    {
        gameOverCreditsStarted = false;
        DHGameEndState.BeginEnding();

        if (result == DHGameEndResult.Clear)
            LoadClearEndingNow();
        else
        {
            AudioManager.Instance?.StopBgm();
            AudioManager.Instance?.PlaySfx(GameOverSfxKey);
            ShowPanel(gameOverPanel);
        }
    }

    public void GoToGameOverCreditsNow()
    {
        if (gameOverCreditsStarted)
            return;

        gameOverCreditsStarted = true;
        Time.timeScale = 1f;

        LoadGameOverCreditScene();
    }

    private void ShowPanel(GameObject panel)
    {
        HidePanels();

        if (panel != null)
            panel.SetActive(true);
    }

    private void HidePanels()
    {
        if (clearPanel != null)
            clearPanel.SetActive(false);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    private void ResetProgressForGameEnd()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ResetForNewGame();
        else
            DHGameProgressResetService.ResetDHProgress();
    }

    private void LoadClearEndingNow()
    {
        if (clearEndingStarted)
            return;

        clearEndingStarted = true;
        Time.timeScale = 1f;

        var request = SceneFadeController.LoadSceneAsyncWithFade(clearEndingSceneName, prepare: () =>
        {
            HidePanels();
            ResetProgressForGameEnd();
            return true;
        }, onFailed: () => { if (this != null) { clearEndingStarted = false; ShowPanel(clearPanel); } });
        if (request == null) clearEndingStarted = false;
    }

    private void LoadGameOverCreditScene()
    {
        var request = SceneFadeController.LoadSceneAsyncWithFade(gameOverCreditSceneName, prepare: () =>
        {
            ResetProgressForGameEnd();
            return true;
        }, onFailed: () => { if (this != null) gameOverCreditsStarted = false; });
        if (request == null) gameOverCreditsStarted = false;
    }
}
