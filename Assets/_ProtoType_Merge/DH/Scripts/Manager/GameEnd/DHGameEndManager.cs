using UnityEngine;
using UnityEngine.SceneManagement;

public enum DHGameEndResult
{
    Clear,
    GameOver
}

public class DHGameEndManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject clearPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Scene")]
    [SerializeField] private string fallbackTitleSceneName = "TitleScene";
    [SerializeField] private string clearEndingSceneName = "Ending CutScene";

    private bool returnToTitleStarted;
    private bool clearEndingStarted;

    private void Awake()
    {
        HidePanels();
    }

    public void ShowResult(DHGameEndResult result)
    {
        returnToTitleStarted = false;
        DHGameEndState.BeginEnding();

        if (result == DHGameEndResult.Clear)
            LoadClearEndingNow();
        else
            ShowPanel(gameOverPanel);
    }

    public void ReturnToTitleNow()
    {
        if (returnToTitleStarted)
            return;

        returnToTitleStarted = true;
        Time.timeScale = 1f;

        LoadTitleScene();
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

    private void ResetProgressForTitle()
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
            ResetProgressForTitle();
            return true;
        }, onFailed: () => { if (this != null) { clearEndingStarted = false; ShowPanel(clearPanel); } });
        if (request == null) clearEndingStarted = false;
    }

    private void LoadTitleScene()
    {
        string destination = GameSceneManager.Instance != null ? GameSceneManager.Instance.TitleScene : fallbackTitleSceneName;
        var request = SceneFadeController.LoadSceneAsyncWithFade(destination, prepare: () =>
        {
            ResetProgressForTitle();
            return true;
        }, onFailed: () => { if (this != null) returnToTitleStarted = false; });
        if (request == null) returnToTitleStarted = false;
    }
}
