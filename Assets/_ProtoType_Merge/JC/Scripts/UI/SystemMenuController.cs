using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SystemMenuController : MonoBehaviour
{
    private const string TitleScene = "TitleScene";
    private const string LobbyScene = "LobbyScene";

    [SerializeField] private GameObject _modal;
    [SerializeField] private Button _btnResume;
    [SerializeField] private Button _btnToTitle;
    [SerializeField] private Button _btnOptions;
    [SerializeField] private Button _btnQuit;

    private void Awake()
    {
        if (_btnResume != null) _btnResume.onClick.AddListener(OnClickResume);
        if (_btnToTitle != null) _btnToTitle.onClick.AddListener(OnClickToTitle);
        if (_btnQuit != null) _btnQuit.onClick.AddListener(OnClickQuit);
    }

    private void Update()
    {
        var sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == TitleScene) return;

        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (sceneName == LobbyScene)
        {
            if (ModalRegistry.HasAny) ModalRegistry.CloseTop();
            else OpenModal();
        }
        else
        {
            TogglePause();
        }
    }

    private void OpenModal()
    {
        if (_modal == null) return;
        transform.SetAsLastSibling();
        _modal.SetActive(true);
    }

    private void TogglePause()
    {
        if (_modal == null) return;

        if (_modal.activeSelf)
        {
            _modal.SetActive(false);
            Time.timeScale = 1f;
        }
        else
        {
            transform.SetAsLastSibling();
            _modal.SetActive(true);
            Time.timeScale = 0f;
        }
    }

    public void OnClickResume()
    {
        if (_modal != null) _modal.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OnClickToTitle()
    {
        if (_modal != null) _modal.SetActive(false);
        Time.timeScale = 1f;
        Debug.Log($"[SystemMenu] → {TitleScene}");
        SceneManager.LoadScene(TitleScene);
    }

    public void OnClickQuit()
    {
        Debug.Log("[SystemMenu] Quit");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
