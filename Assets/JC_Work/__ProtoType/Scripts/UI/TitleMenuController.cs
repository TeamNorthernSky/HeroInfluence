using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleMenuController : MonoBehaviour
{
    public void OnNewGameClicked()
    {
        Debug.Log("[TitleMenu] 새 게임 → LobbyScene");
        SceneManager.LoadScene("LobbyScene");
    }

    public void OnLoadClicked()
    {
        Debug.Log("[TitleMenu] 불러오기 클릭 (미구현)");
    }

    public void OnSettingsClicked()
    {
        Debug.Log("[TitleMenu] 설정 클릭 (미구현)");
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
