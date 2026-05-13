using UnityEngine;

public class BootSceneController : MonoBehaviour
{
    public enum FirstSceneTarget { TestScene, TitleScene }

    [SerializeField] private FirstSceneTarget firstScene = FirstSceneTarget.TitleScene;

    private void Start()
    {
        // [JC 수정 260512] SceneLoader 폐기 → GameSceneManager 단순 래퍼 사용
        GameSceneManager.LoadScene(firstScene.ToString());
    }
}
