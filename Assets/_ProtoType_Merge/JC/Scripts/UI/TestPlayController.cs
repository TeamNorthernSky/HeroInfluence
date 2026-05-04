using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TestPlayController : MonoBehaviour
{
    private const string LobbyScene = "LobbyScene";

    [SerializeField] private Button _backButton;

    private void Awake()
    {
        if (_backButton != null) _backButton.onClick.AddListener(OnClickBackToLobby);
    }

    public void OnClickBackToLobby()
    {
        Debug.Log($"[TEST_Play] → {LobbyScene}");
        SceneManager.LoadScene(LobbyScene);
    }
}
