using UnityEngine;

public class BootSceneController : MonoBehaviour
{
    public enum FirstSceneTarget { TestScene, TitleScene }

    [SerializeField] private FirstSceneTarget firstScene = FirstSceneTarget.TitleScene;

    private void Start()
    {
        var sceneLoader = GameManager.Instance.SceneLoader;
        if (sceneLoader != null)
        {
            sceneLoader.LoadScene(firstScene.ToString());
        }
        else
        {
            Debug.LogError("[BootSceneController] SceneLoader를 찾을 수 없습니다");
        }
    }
}
