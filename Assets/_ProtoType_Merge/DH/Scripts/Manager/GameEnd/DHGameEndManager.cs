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

    private bool returnToTitleStarted;

    private void Awake()
    {
        HidePanels();
    }

    public void ShowResult(DHGameEndResult result)
    {
        returnToTitleStarted = false;
        DHGameEndState.BeginEnding();

        if (result == DHGameEndResult.Clear)
            ShowPanel(clearPanel);
        else
            ShowPanel(gameOverPanel);
    }

    public void ReturnToTitleNow()
    {
        if (returnToTitleStarted)
            return;

        returnToTitleStarted = true;
        Time.timeScale = 1f;

        ResetProgressForTitle();
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
