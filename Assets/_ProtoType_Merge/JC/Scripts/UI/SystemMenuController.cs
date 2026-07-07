using TMPro;
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
    // [KJ 260703] 저장 기능 — 로비에서만 노출
    [SerializeField] private Button _btnSave;
    [SerializeField] private TMP_Text _saveLabel;

    private void Awake()
    {
        if (_btnResume != null) _btnResume.onClick.AddListener(OnClickResume);
        if (_btnToTitle != null) _btnToTitle.onClick.AddListener(OnClickToTitle);
        if (_btnQuit != null) _btnQuit.onClick.AddListener(OnClickQuit);
        if (_btnSave != null) _btnSave.onClick.AddListener(OnClickSave);
    }

    private void Update()
    {
        var sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == TitleScene) return;

        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // [JC 260619] DH 엔딩 시퀀스(대형 아이콘 출력) 진행 중에는 ESC로 시스템 메뉴를 열지 않는다.
        if (DHGameEndState.IsEnding) return;

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
        RefreshSaveButton();
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
            RefreshSaveButton();
            _modal.SetActive(true);
            Time.timeScale = 0f;
        }
    }

    // [KJ 260706] 저장 버튼 상시 노출로 변경(DH 협업 정책). 활성 = 로비 / 탐사씬 플레이어 턴.
    // 탐사 EnemyTurn·전투 등 그 외 씬은 회색(interactable=false). 모달이 열릴 때마다 라벨 "저장" 원복.
    private void RefreshSaveButton()
    {
        if (_btnSave == null) return;
        _btnSave.gameObject.SetActive(true);
        _btnSave.interactable = IsSaveAllowed();
        if (_saveLabel != null) _saveLabel.text = "저장";
    }

    // [KJ 260706] 화이트리스트 판정 — 전투 씬 이름이 중앙화되어 있지 않아(TmpBattleScene 등)
    // 허용 씬(로비/탐사)만 명시하고 나머지는 전부 저장 불가로 처리.
    private static bool IsSaveAllowed()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene == LobbyScene) return true;

        var gsm = GameSceneManager.Instance;
        if (gsm != null && scene == gsm.ExplorationScene)
        {
            // [KJ 260706] GameManager 씬 로케이터 정리(JC 260703)로 Turn 프로퍼티 없음 — 씬에서 직접 조회(메뉴 열 때만 호출).
            var turn = FindFirstObjectByType<TurnManager>();
            return turn == null || !turn.IsEnemyTurnRunning;
        }

        return false;
    }

    private void OnClickSave()
    {
        bool ok = GameSaveService.SaveToSlot(SaveSlotRepository.CurrentSlot);
        if (_saveLabel != null) _saveLabel.text = ok ? "저장 완료" : "저장 실패";
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
