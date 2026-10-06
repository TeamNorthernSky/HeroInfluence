using UnityEngine;

public class BootSceneController : MonoBehaviour
{
    public enum FirstSceneTarget { TestScene, TitleScene }

    [SerializeField] private FirstSceneTarget firstScene = FirstSceneTarget.TitleScene;

    private void Start()
    {
        // [JC 수정 260512] SceneLoader 폐기 → GameSceneManager 사용
        // [JC 260514] GameSceneManager 컴포넌트 격상 — Instance 경유 호출. Boot 시점에 GameManager 자식 Awake 완료 가정.
        if (GameSceneManager.Instance != null)
            GameSceneManager.Instance.LoadScene(firstScene.ToString());
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(firstScene.ToString());
    }
}
