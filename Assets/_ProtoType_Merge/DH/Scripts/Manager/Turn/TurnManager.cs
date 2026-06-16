using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Serialization;

public class TurnManager : MonoBehaviour
{
    public event System.Action<int> DayAdvanced;
    public event System.Action<bool> EnemyTurnStateChanged;

    [SerializeField] private int day = 1;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private EnemyTurnController enemyTurnController;
    [SerializeField] private TMP_Text turnStateText;
    [FormerlySerializedAs("mineRegistry")]
    [SerializeField] private OutpostRegistry outpostRegistry;

    private bool enemyTurnRunning;

    public bool IsEnemyTurnRunning => enemyTurnRunning;
    public bool IsPlayerTurn => !enemyTurnRunning;

    private void Awake()
    {
        if (enemyTurnController == null)
            enemyTurnController = FindFirstObjectByType<EnemyTurnController>();

        // [JC 수정 260511] GameManager.CurrentDay에서 day 복원 (DHScene 재로드 시 Day 리셋 방지)
        if (GameManager.Instance != null)
            day = GameManager.Instance.CurrentDay;

        UpdateTurnStateText("Player Turn");
    }

    private void Start()
    {
        // [JC 260610] 로비에서 위임된 턴종료 요청 처리.
        // 로비 턴종료 = "나가기 + 탐사 턴종료" 이므로, 탐사 진입 직후 정상 EndPlayerTurn(적 턴 진행)을 실행한다.
        if (GameManager.Instance != null && GameManager.Instance.ConsumePendingEndTurn())
            EndPlayerTurn();

        EnemyTurnSessionRepository sessionRepository = EnemyTurnSessionRepository.Instance;
        if (sessionRepository != null && sessionRepository.ShouldResumeAfterCombat)
            StartCoroutine(ResumeEnemyTurnAfterSceneReady());
    }

    public void EndPlayerTurn()
    {
        if (DHGameEndState.IsEnding)
            return;

        // [JC 260615] 턴 전환 시작 → 월드 입력 차단(다음 턴 income 모달 확인 시 해제).
        // 적 턴 진행 중은 물론, 씬 전환 직후·모달 표시 전의 틈에도 탐사 오브젝트 인터랙션(본부 더블클릭 등)을 막는다.
        WorldInputGate.IsTurnResolving = true;

        StartEnemyTurn();
    }

    private void StartEnemyTurn()
    {
        if (DHGameEndState.IsEnding)
            return;

        if (enemyTurnRunning)
            return;

        enemyTurnRunning = true;
        EnemyTurnStateChanged?.Invoke(true);
        UpdateTurnStateText("Enemy Turn");

        if (enemyTurnController == null)
        {
            enemyTurnRunning = false;
            EndEnemyTurn();
            return;
        }

        StartCoroutine(RunEnemyTurn());
    }

    private void EndEnemyTurn()
    {
        if (DHGameEndState.IsEnding)
        {
            enemyTurnRunning = false;
            EnemyTurnStateChanged?.Invoke(false);
            WorldInputGate.IsTurnResolving = false; // [JC 260615] 게임 종료 시 게이트 해제(누수 방지)
            return;
        }

        AdvanceDay();
        ProduceClaimedOutposts();
        StartPlayerTurn();
    }

    private void StartPlayerTurn()
    {
        if (DHGameEndState.IsEnding)
            return;

        UpdateTurnStateText("Player Turn");

        if (partyRegistry == null)
            return;

        PartyGridMover[] partyMovers = partyRegistry.PartyMovers;
        for (int i = 0; i < partyMovers.Length; i++)
        {
            PartyGridMover partyMover = partyMovers[i];
            if (partyMover == null)
                continue;

            partyMover.ResetMovePointsToMax();
        }
    }

    private void AdvanceDay()
    {
        day++;
        // [JC 수정 260511] GameManager에 동기화
        if (GameManager.Instance != null)
            GameManager.Instance.CurrentDay = day;
        DayAdvanced?.Invoke(day);
    }

    private void ProduceClaimedOutposts()
    {
        if (outpostRegistry == null)
            return;

        IReadOnlyList<Outpost> outposts = outpostRegistry.Outposts;

        for (int i = 0; i < outposts.Count; i++)
        {
            Outpost outpost = outposts[i];
            if (outpost == null)
                continue;

            outpost.ProduceForTurn();
        }
    }

    public int GetDay()
    {
        return day;
    }

    private IEnumerator RunEnemyTurn()
    {
        yield return enemyTurnController.ExecuteEnemyTurn();

        if (DHGameEndState.IsEnding)
        {
            enemyTurnRunning = false;
            EnemyTurnStateChanged?.Invoke(false);
            WorldInputGate.IsTurnResolving = false; // [JC 260615] 게임 종료 시 게이트 해제(누수 방지)
            yield break;
        }

        EnemyTurnSessionRepository sessionRepository = EnemyTurnSessionRepository.Instance;
        if (sessionRepository != null && sessionRepository.ShouldResumeAfterCombat)
        {
            enemyTurnRunning = false;
            EnemyTurnStateChanged?.Invoke(false);
            yield break;
        }

        enemyTurnRunning = false;
        EnemyTurnStateChanged?.Invoke(false);
        EndEnemyTurn();
    }

    private IEnumerator ResumeEnemyTurnAfterSceneReady()
    {
        yield return null;

        while (CombatContext.Instance != null && CombatContext.Instance.Result != CombatResult.None)
            yield return null;

        if (DHGameEndState.IsEnding || enemyTurnRunning)
            yield break;

        if (enemyTurnController == null)
            enemyTurnController = FindFirstObjectByType<EnemyTurnController>();

        if (enemyTurnController == null)
            yield break;

        EnemyTurnSessionRepository sessionRepository = EnemyTurnSessionRepository.Instance;
        if (sessionRepository == null || !sessionRepository.ShouldResumeAfterCombat)
            yield break;

        enemyTurnRunning = true;
        EnemyTurnStateChanged?.Invoke(true);
        UpdateTurnStateText("Enemy Turn");
        StartCoroutine(RunEnemyTurn());
    }

    private void UpdateTurnStateText(string nextText)
    {
        if (turnStateText == null)
            return;

        turnStateText.text = nextText;
    }

    private void OnValidate()
    {
        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();
    }
}
