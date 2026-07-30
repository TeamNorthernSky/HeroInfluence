using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [KJ 260730] 파티 전멸 대기 중 턴 종료를 강제하는 차단 패널 컨트롤러.
/// 전투 패배로 DefeatedPartyReturnController에 복귀 대기가 등록되면 패널 프리팹을 Instantiate하고,
/// 턴 종료 버튼을 누르면 파괴한다.
///
/// 차단 원리는 렌더 순서다. 패널은 턴 종료 버튼과 같은 부모 안에서 첫 번째 자식으로 들어가므로,
/// 같은 부모의 나머지(NextTurn·TurnBar·Text_TurnWeekDay)가 패널 위에 남아 클릭이 통한다.
/// 패널 프리팹의 Modal은 keepSiblingOrder=true(첫 자식 위치 유지)여야 한다.
///
/// [KJ 260730] 카메라 정지는 Time.timeScale=0(pausesGame)이 아니라 호버 판정으로 처리한다.
/// 패널 루트의 CameraEdgeScrollBlocker를 QuarterViewCameraFollower가 버튼과 동일하게 취급해
/// 마우스가 패널 위에 있는 동안 엣지 스크롤을 멈춘다. 패널이 전체화면이므로 결과적으로 상시 정지다.
/// </summary>
[DisallowMultipleComponent]
public class ExplorationDefeatBlockPanelController : MonoBehaviour
{
    [Header("차단 패널 프리팹 (Modal: keepSiblingOrder=true + CameraEdgeScrollBlocker)")]
    [SerializeField] private GameObject panelPrefab;

    [Header("생성 위치 (비우면 턴 종료 버튼의 부모)")]
    [SerializeField] private Transform spawnParent;

    [Header("턴 종료 버튼 (비우면 이름 자동 탐색)")]
    [SerializeField] private Button nextTurnButton;

    private const string NextTurnObjectName = "BTN_Explor_NextTurn";

    private GameObject spawnedPanel;
    private bool listenerHooked;
    private Coroutine pendingSpawnCoroutine;
    private TurnManager turnManager;

    private void OnEnable()
    {
        DefeatedPartyReturnController.WaitingStateChanged += OnWaitingStateChanged;
        HookButton();
        // 전투 패배 처리가 이 컨트롤러의 구독보다 먼저 끝났을 수 있으므로 현재 상태로 한 번 맞춘다.
        ApplyWaitingState(DefeatedPartyReturnController.IsAnyPartyWaiting);
    }

    private void OnDisable()
    {
        DefeatedPartyReturnController.WaitingStateChanged -= OnWaitingStateChanged;
        UnhookButton();
        DespawnPanel();
    }

    private void ResolveMissing()
    {
        if (nextTurnButton == null)
        {
            var go = GameObject.Find(NextTurnObjectName);
            if (go != null) nextTurnButton = go.GetComponentInChildren<Button>(true);
        }
    }

    private void HookButton()
    {
        if (listenerHooked) return;
        if (nextTurnButton == null) ResolveMissing();
        if (nextTurnButton == null) return;

        nextTurnButton.onClick.AddListener(OnClickNextTurn);
        listenerHooked = true;
    }

    private void UnhookButton()
    {
        if (listenerHooked && nextTurnButton != null)
            nextTurnButton.onClick.RemoveListener(OnClickNextTurn);

        listenerHooked = false;
    }

    private void OnWaitingStateChanged(bool anyWaiting) => ApplyWaitingState(anyWaiting);

    private void ApplyWaitingState(bool anyWaiting)
    {
        if (anyWaiting) RequestSpawn();
        else DespawnPanel();
    }

    /// <summary>
    /// [KJ 260730] 채팅 모달이 재생 중이면 끝날 때까지 생성을 미룬다.
    /// 전투 패배 직후 대사가 흐르는 경우 차단 패널이 그 위를 덮지 않도록 하기 위함.
    /// </summary>
    private void RequestSpawn()
    {
        if (spawnedPanel != null || pendingSpawnCoroutine != null) return;
        if (IsEnemyTurnInProgress()) return;

        if (IsChatRunning())
        {
            pendingSpawnCoroutine = StartCoroutine(SpawnAfterChatEnds());
            return;
        }

        SpawnPanel();
    }

    private static bool IsChatRunning()
        => ChatManager.Instance != null && ChatManager.Instance.IsRunning;

    /// <summary>
    /// [KJ 260730] 적 턴 중 패배면 패널을 띄우지 않는다.
    /// 이동형 적이 파티를 치면 패배는 적 턴 도중에 일어나는데, 이때는 남은 적 이동이 마저 끝나고
    /// TurnManager가 EnemyTurnStateChanged(false)를 쏘면 DefeatedPartyReturnController가
    /// CompleteScheduledReturns → LoadLobbyScene()으로 바로 이어간다. 플레이어가 조작할 틈이 없으므로
    /// 차단 패널이 필요 없고, 오히려 적 이동을 방해한다.
    ///
    /// 세션 저장소를 먼저 보는 이유: 전투에서 돌아온 직후엔 TurnManager.enemyTurnRunning이 아직 false다.
    /// (ResumeEnemyTurnAfterSceneReady가 한 프레임 + 채팅 대기 뒤에야 true로 올린다.)
    /// EnemyTurnSessionRepository는 DontDestroyOnLoad라 전투 씬 왕복에도 세션 상태를 유지한다.
    /// </summary>
    private bool IsEnemyTurnInProgress()
    {
        if (EnemyTurnSessionRepository.Instance != null && EnemyTurnSessionRepository.Instance.IsActive)
            return true;

        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();

        return turnManager != null && turnManager.IsEnemyTurnRunning;
    }

    private System.Collections.IEnumerator SpawnAfterChatEnds()
    {
        // 채팅 모달은 pausesGame일 수 있으므로 timeScale에 의존하지 않는 프레임 대기를 쓴다.
        while (IsChatRunning())
            yield return null;

        pendingSpawnCoroutine = null;

        // 대기 중 복귀가 완료됐거나 적 턴이 시작됐을 수 있으니 상태를 다시 확인한다.
        if (DefeatedPartyReturnController.IsAnyPartyWaiting && !IsEnemyTurnInProgress())
            SpawnPanel();
    }

    private void CancelPendingSpawn()
    {
        if (pendingSpawnCoroutine == null) return;

        StopCoroutine(pendingSpawnCoroutine);
        pendingSpawnCoroutine = null;
    }

    private void SpawnPanel()
    {
        if (spawnedPanel != null) return;
        if (panelPrefab == null)
        {
            Debug.LogError($"[{nameof(ExplorationDefeatBlockPanelController)}] panelPrefab이 비어 있어 차단 패널을 띄우지 못했다.", this);
            return;
        }

        Transform parent = ResolveSpawnParent();
        if (parent == null)
        {
            Debug.LogError($"[{nameof(ExplorationDefeatBlockPanelController)}] 생성 부모를 찾지 못했다.", this);
            return;
        }

        spawnedPanel = Instantiate(panelPrefab, parent, false);
        spawnedPanel.name = panelPrefab.name;

        // 같은 부모의 나머지 UI가 패널 위에 남도록 맨 앞으로 보낸다.
        // (프리팹의 Modal은 keepSiblingOrder=true여야 OnEnable에서 이 위치가 유지된다.)
        spawnedPanel.transform.SetAsFirstSibling();

        if (spawnedPanel.transform is RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        spawnedPanel.SetActive(true);
    }

    private void DespawnPanel()
    {
        CancelPendingSpawn();

        if (spawnedPanel == null) return;

        Destroy(spawnedPanel);
        spawnedPanel = null;
    }

    private Transform ResolveSpawnParent()
    {
        if (spawnParent != null) return spawnParent;
        if (nextTurnButton == null) ResolveMissing();
        return nextTurnButton != null ? nextTurnButton.transform.parent : null;
    }

    /// <summary>
    /// 턴 종료를 누르면 즉시 패널을 파괴한다.
    /// </summary>
    private void OnClickNextTurn() => DespawnPanel();
}
