using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SystemMenuController : MonoBehaviour
{
    private const string TitleScene = "TitleScene";
    private const string LobbyScene = "LobbyScene_New";

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

        // [JC 260513] ESC 정책 통일 — 모든 씬 공통: Top 모달이 있으면 그것 닫고, 없으면 시스템 메뉴 토글.
        // 기존 정책(LobbyScene만 Top닫기, 그 외 무조건 TogglePause) 폐기.
        // SystemMenuModal 자신이 Top일 때는 자기를 닫는 ModalRegistry.CloseTop이 작동 — 별도 가드 불필요.
        if (ModalRegistry.HasAny)
        {
            ModalRegistry.CloseTop();
            // SystemMenuModal이 닫힌 경우 timeScale 복원은 ModalPauseGate.Refresh가 처리 (Modal.OnDisable에서 자동 호출).
        }
        else
        {
            if (sceneName == LobbyScene)
                OpenModal();
            else
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
