using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialExitButton : MonoBehaviour
{
    [SerializeField] private Button exitButton;
    private bool isLoading;
    private string openingCutSceneName = "Opening CutScene";

    private void Awake()
    {
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
        // 튜토리얼에서 사용한 협회/강화 상태를 본 게임에 넘기지 않는다.
        if (GameManager.Instance != null) GameManager.Instance.ResetForNewGame();
        else DHGameProgressResetService.ResetDHProgress();
        JC.Tutorial.JcTutorialExitTransition.Begin(openingCutSceneName);
    }
}
