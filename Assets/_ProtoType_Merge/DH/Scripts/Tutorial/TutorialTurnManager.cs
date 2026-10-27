using System;
using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialTurnManager : MonoBehaviour
{
    public event Action<int> TutorialTurnAdvanced;

    [Header("State")]
    [SerializeField] private bool turnControlEnabled = true;
    [SerializeField] private TMP_Text turnStateText;

    [Header("Party")]
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private bool resetMovePointsOnTurnAdvanced = true;

    [Header("Options")]
    [SerializeField] private bool resetTurnOnStart;

    private TutorialProgressRepository repository;
    private bool turnAdvancing;
    private int readyFrames;
    public const string AutomaticTurnLessonKey = "jc.explore.guide.v2.automatic-turn";
    private void Start() => JC.Environment.JcDayNightController.Ensure(gameObject.scene);

    // 수동 휴식 잠금과 자동 전환 대기를 분리합니다. 소진 상태는 복귀 저장 데이터에서도 복원됩니다.
    private void LateUpdate()
    {
        ResolveReferences();
        var party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (party == null || party.RemainingMovePoints > 0 || !CanAdvance() ||
            !ResolveRepository().IsMessageSeen(AutomaticTurnLessonKey)) { readyFrames = 0; return; }
        if (++readyFrames >= 2) BeginAdvance();
    }

    private bool CanAdvance()
    {
        if (turnAdvancing || !isActiveAndEnabled ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene() != gameObject.scene ||
            ModalManager.HasAny || ChatModalController.IsOpen || WorldInputGate.IsTurnResolving || JC.Environment.JcDayNightController.IsTransitioning || Input.GetMouseButton(0) ||
            (SceneFadeController.Instance != null && SceneFadeController.Instance.IsTransitioning) ||
            (ChatManager.Instance != null && ChatManager.Instance.IsRunning)) return false;
        var progress = ResolveRepository();
        if (progress == null || progress.TutorialCompleted || progress.PendingCombatSourceType != TutorialCombatSourceType.None) return false;
        var party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (party == null || party.IsMoving) return false;
        var input = party.GetComponent<PartyRuntime>();
        if (input != null && input.IsInputLocked) return false;
        var runtime = party.GetComponent<TutorialPartyRuntime>();
        if (runtime != null && !runtime.SceneEntryReady) return false;
        foreach (var ui in FindObjectsByType<TutorialExploreUIController>(FindObjectsSortMode.None))
            if (ui.gameObject.scene == gameObject.scene && (!ui.SceneEntryReady || ui.IsShowing)) return false;
        return true;
    }

    private void BeginAdvance()
    {
        if (turnAdvancing) return;
        turnAdvancing = true;
        readyFrames = 0;
        StartCoroutine(AdvanceRoutine());
    }

    private IEnumerator AdvanceRoutine()
    {
        try
        {
            var cycle = JC.Environment.JcDayNightController.Ensure(gameObject.scene);
            if (cycle != null) yield return cycle.Transition(CurrentTurn, CurrentTurn + 1);
            int nextTurn = ResolveRepository().AdvanceTurn();
            ResetPartyMovePoints();
            TutorialTurnAdvanced?.Invoke(nextTurn);
            FindFirstObjectByType<MoveRangeController>()?.RefreshForCurrentParty();
            RefreshTurnText();
        }
        finally { turnAdvancing = false; }
    }
    private void OnDisable()
    {
        StopAllCoroutines(); turnAdvancing = false; readyFrames = 0;
        JC.Environment.JcDayNightController.CancelForScene(gameObject.scene);
    }

    public int CurrentTurn => ResolveRepository() != null ? repository.CurrentTurn : 1;
    public bool IsTurnAdvancing => turnAdvancing;
    public bool TurnControlEnabled
    {
        get => turnControlEnabled;
        set => turnControlEnabled = value;
    }

    private void Awake()
    {
        ResolveReferences();
        ResolveRepository();
        if (resetTurnOnStart)
            repository?.ResetTurn();

        RefreshTurnText();
    }

    public void EndTutorialTurn()
    {
        ResolveReferences();
        if (!turnControlEnabled || !CanAdvance()) return;
        var party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (party == null || party.RemainingMovePoints <= 0) return;
        BeginAdvance();
    }

    public void SetTutorialTurn(int turn)
    {
        ResolveRepository()?.SetCurrentTurn(turn);
        RefreshTurnText();
    }

    public void ResetTutorialTurn()
    {
        ResolveRepository()?.ResetTurn();
        RefreshTurnText();
    }

    private TutorialProgressRepository ResolveRepository()
    {
        if (repository == null)
            repository = TutorialProgressRepository.EnsureInstance();

        return repository;
    }

    private void ResolveReferences()
    {
        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();
    }

    private void ResetPartyMovePoints()
    {
        if (!resetMovePointsOnTurnAdvanced)
            return;

        ResolveReferences();
        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (party != null)
            party.ResetMovePointsToMax();
    }

    private void RefreshTurnText()
    {
        if (turnStateText == null)
            return;

        turnStateText.text = $"Turn {CurrentTurn}";
    }
}
