using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// DHScene 우하단 로비 진입 버튼.
/// 입력 대기 상태(모달 미오픈 + 어떤 파티도 이동 중이 아님)에서만 interactable.
/// 시스템 메뉴(ESC)와는 분리된 별도 경로.
/// 부착 위치: DHScene 우하단 Canvas의 Button GameObject.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class DHLobbyEntryButton : MonoBehaviour
{
    // [JC 260610] 중앙값(GameSceneManager.LobbyScene) 참조. 폴백 HQLobbyScene.
    private static string LobbyScene => GameSceneManager.Instance != null
        ? GameSceneManager.Instance.LobbyScene
        : "HQLobbyScene";

    [Tooltip("PartyGridMover 캐시 재탐색 주기(초). 파티 생성/소멸 빈도 따라 조정")]
    [SerializeField] private float moverRefreshInterval = 0.5f;

    private Button button;
    private PartyGridMover[] cachedMovers;
    private float nextMoverRefreshTime;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnEnable()
    {
        RefreshMovers();
        UpdateInteractable();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextMoverRefreshTime)
            RefreshMovers();

        UpdateInteractable();
    }

    private void UpdateInteractable()
    {
        bool can = CanEnter();
        if (button.interactable != can)
            button.interactable = can;
    }

    private bool CanEnter()
    {
        if (ModalManager.HasAny) return false;

        // 본부 앞 그리드에 파티가 있을 때만 활성화 (사용자 명세)
        HQVisitState state = HQVisitState.Instance;
        if (state == null || !state.HasVisitingParty) return false;

        if (cachedMovers != null)
        {
            for (int i = 0; i < cachedMovers.Length; i++)
            {
                PartyGridMover mover = cachedMovers[i];
                if (mover != null && mover.IsMoving)
                    return false;
            }
        }

        return true;
    }

    private void RefreshMovers()
    {
        cachedMovers = FindObjectsByType<PartyGridMover>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        nextMoverRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, moverRefreshInterval);
    }

    private void OnClick()
    {
        // 가드: Update 사이 클릭됐을 가능성 대비 재확인
        if (!CanEnter())
        {
            Debug.Log("[DHLobbyEntryButton] 진입 조건 불만족 — 무시");
            return;
        }

        Debug.Log($"[DHLobbyEntryButton] → {LobbyScene}");
        SceneManager.LoadScene(LobbyScene);
    }
}
