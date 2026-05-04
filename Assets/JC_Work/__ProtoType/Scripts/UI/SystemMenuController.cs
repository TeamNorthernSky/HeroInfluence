using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SystemMenuController : MonoBehaviour
{
    private const string TitleScene = "TitleScene";

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
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (ModalRegistry.HasAny)
        {
            ModalRegistry.CloseTop();
        }
        else
        {
            OpenModal();
        }
    }

    private void OpenModal()
    {
        if (_modal == null) return;
        transform.SetAsLastSibling();
        _modal.SetActive(true);
    }

    public void OnClickResume()
    {
        if (_modal != null) _modal.SetActive(false);
    }

    public void OnClickToTitle()
    {
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
