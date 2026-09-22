using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialExitButton : MonoBehaviour
{
    [SerializeField] private Button exitButton;
    private bool isLoading;

    private void Awake()
    {
        if (exitButton == null)
            exitButton = GetComponent<Button>();

        if (exitButton != null)
            exitButton.onClick.AddListener(goNextScene);
        else
            Debug.LogWarning("TutorialExitButton에 나가기 버튼을 연결해주세요.", this);
    }

    private void OnDestroy()
    {
        if (exitButton != null)
            exitButton.onClick.RemoveListener(goNextScene);
    }

    public void goNextScene()
    {
        if (isLoading) return;
        isLoading = true;
        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadSceneAsync("DHScene_3");
            //GameSceneManager.Instance.LoadScene("DHScene_3");
        else
            SceneManager.LoadSceneAsync("DHScene_3");
            //SceneManager.LoadScene("DHScene_3");
    }
}
