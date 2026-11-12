using UnityEngine;

[DisallowMultipleComponent]
public class CreditSceneController : MonoBehaviour
{
    [SerializeField] private GameObject firstPage;
    [SerializeField] private GameObject secondPage;
    [SerializeField] private string titleSceneName = "TitleScene";

    private bool showingSecondPage;
    private bool leaving;

    private void Awake()
    {
        firstPage.SetActive(true);
        secondPage.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            Advance();
    }

    public void Advance()
    {
        if (leaving || (SceneFadeController.Instance != null &&
                        SceneFadeController.Instance.IsTransitioning))
            return;

        if (!showingSecondPage)
        {
            firstPage.SetActive(false);
            secondPage.SetActive(true);
            showingSecondPage = true;
            return;
        }

        leaving = true;
        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene(titleSceneName);
        else
            SceneFadeController.LoadSceneWithFadeIfNeeded(titleSceneName);
    }
}
