using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SystemMenuController : MonoBehaviour
{
    private const string TitleScene = "TitleScene";
    // [JC 260610] 중앙값(GameSceneManager.LobbyScene) 참조. ESC가 로비에서 메뉴 모달을 열도록 분기.
    private static string LobbyScene => GameSceneManager.Instance != null
        ? GameSceneManager.Instance.LobbyScene
        : "HQLobbyScene";

    [SerializeField] private GameObject _modal;
    [SerializeField] private Button _btnResume;
    [SerializeField] private Button _btnToTitle;
    [SerializeField] private Button _btnOptions;
    [SerializeField] private Button _btnQuit;
    [SerializeField] private Button _btnSave;

    private void Awake()
    {
        if (_btnResume != null) _btnResume.onClick.AddListener(OnClickResume);
        if (_btnToTitle != null) _btnToTitle.onClick.AddListener(OnClickToTitle);
        if (_btnQuit != null) _btnQuit.onClick.AddListener(OnClickQuit);
        if (_btnOptions != null) _btnOptions.onClick.AddListener(OnClickOptions);
        if (_btnSave != null) _btnSave.onClick.AddListener(OnClickSave);
    }

    private void Update()
    {
        var sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == TitleScene) return;

        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // [JC 260619] DH 엔딩 시퀀스(대형 아이콘 출력) 진행 중에는 ESC로 시스템 메뉴를 열지 않는다.
        if (DHGameEndState.IsEnding) return;

        // [KJ 260729] ChatModal이 Top이면 ESC = 스킵 확인 팝업 토글(대화는 ESC로 닫히지 않음).
        if (ChatModalController.HandleEscape()) return;

        // [JC 260513] ESC 정책 통일 — 모든 씬 공통: Top 모달이 있으면 그것 닫고, 없으면 시스템 메뉴 토글.
        // 기존 정책(LobbyScene만 Top닫기, 그 외 무조건 TogglePause) 폐기.
        // SystemMenuModal 자신이 Top일 때는 자기를 닫는 ModalManager.CloseTop이 작동 — 별도 가드 불필요.
        if (ModalManager.HasAny)
        {
            ModalManager.CloseTop();
            // SystemMenuModal이 닫힌 경우 timeScale 복원은 ModalPauseGate.Refresh가 처리 (Modal.OnDisable에서 자동 호출).
        }
        else
        {
            // [JC 260629] 본부 빌드모드 중이면 ESC = 빌드모드 나가기(시스템 메뉴/일시정지보다 우선). 로비 외에서는 BuildMode=null이라 무영향.
            if (LobbyUIRegistry.BuildMode != null && LobbyUIRegistry.BuildMode.HandleEscape())
                return;

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

    /// <summary>[JC 260617] 외부(로비 옵션 버튼 등)에서 시스템 메뉴 모달을 연다.</summary>
    public void OpenMenu() => OpenModal();

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

    // [KJ 260708] SystemMenu는 닫지 않고 Settings를 그 위에 스택으로 연다(ModalManager 재사용).
    public void OnClickOptions()
    {
        CommonUIManager.Instance.SettingsModal.Open();
    }

    public void OnClickResume()
    {
        if (_modal != null) _modal.SetActive(false);
        Time.timeScale = 1f;
    }

    // [KJ 260715] B 수동 저장 — 현재 슬롯에 현재 진행(마지막 턴 시작 스냅샷)을 기록.
    // GameSaveService가 스냅샷 부재/IO 실패를 자체 처리(스냅샷 없으면 저장 스킵, false). 메뉴는 열어둔 채 저장만 수행.
    public void OnClickSave()
    {
        bool saved = GameSaveService.SaveToSlot(SaveSlotRepository.CurrentSlot);
        Debug.Log(saved
            ? $"[SystemMenu] 저장 완료 — 슬롯 {SaveSlotRepository.CurrentSlot}."
            : "[SystemMenu] 저장 실패/건너뜀 — 턴 시작 스냅샷 없음 또는 IO 오류.");
    }

    public void OnClickToTitle()
    {
        if (_modal != null) _modal.SetActive(false);
        Time.timeScale = 1f;
        // [JC 260617] 타이틀 복귀 시 전체 영속 상태 초기화(새 게임이 첫 실행과 동일하도록).
        if (GameManager.Instance != null) GameManager.Instance.ResetForNewGame();
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
