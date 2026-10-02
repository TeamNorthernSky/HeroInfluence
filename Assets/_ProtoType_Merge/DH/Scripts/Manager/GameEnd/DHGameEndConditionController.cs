using System.Collections.Generic;
using UnityEngine;

public class DHGameEndConditionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private DHGameEndManager gameEndManager;

    [Header("Turn Limit")]
    [SerializeField] private bool enableTurnLimitGameOver = true;
    [SerializeField, Min(1)] private int gameOverTurnLimit = 35;
    [SerializeField] private bool evaluateCurrentTurnOnStart = true;

    [Header("Clear Flags")]
    [SerializeField] private bool enableFlagGameClear = true;
    [Tooltip("Any enabled event flag in this list triggers game clear. Prefixes like Flag_ or Set_Flag_ are allowed.")]
    [SerializeField] private List<string> gameClearFlagNames = new List<string>();
    [SerializeField] private bool evaluateClearFlagsOnStart = true;

    private bool isEnding;
    private DHEventStateRepository eventStateRepository;

    private void Start()
    {
        ResolveReferences();
        SubscribeTurnManager();
        SubscribeEventStateRepository();

        if (evaluateCurrentTurnOnStart)
            EvaluateTurnLimit(ResolveCurrentTurn());

        if (evaluateClearFlagsOnStart)
            EvaluateGameClearFlags();
    }

    private void OnDestroy()
    {
        if (turnManager != null)
            turnManager.DayAdvanced -= HandleDayAdvanced;

        if (eventStateRepository != null)
            eventStateRepository.FlagChanged -= HandleEventFlagChanged;
    }

    private void HandleDayAdvanced(int currentTurn)
    {
        EvaluateTurnLimit(currentTurn);
    }

    private void EvaluateTurnLimit(int currentTurn)
    {
        if (!enableTurnLimitGameOver || isEnding)
            return;

        if (currentTurn >= gameOverTurnLimit)
            BeginGameEnd(DHGameEndResult.GameOver);
    }

    private void HandleEventFlagChanged(string flagName, bool value)
    {
        if (!value)
            return;

        EvaluateGameClearFlag(flagName);
    }

    private void EvaluateGameClearFlags()
    {
        if (!enableFlagGameClear || isEnding || eventStateRepository == null || gameClearFlagNames == null)
            return;

        for (int i = 0; i < gameClearFlagNames.Count; i++)
        {
            string flagName = gameClearFlagNames[i];
            if (!string.IsNullOrWhiteSpace(flagName) && eventStateRepository.GetFlag(flagName))
            {
                BeginGameEnd(DHGameEndResult.Clear);
                return;
            }
        }
    }

    private void EvaluateGameClearFlag(string changedFlagName)
    {
        if (!enableFlagGameClear || isEnding || string.IsNullOrWhiteSpace(changedFlagName) || gameClearFlagNames == null)
            return;

        for (int i = 0; i < gameClearFlagNames.Count; i++)
        {
            string flagName = gameClearFlagNames[i];
            if (string.IsNullOrWhiteSpace(flagName))
                continue;

            if (DHEventStateRepository.NormalizeFlagName(flagName) == changedFlagName)
            {
                BeginGameEnd(DHGameEndResult.Clear);
                return;
            }
        }
    }

    private void BeginGameEnd(DHGameEndResult result)
    {
        if (isEnding)
            return;

        isEnding = true;
        DHGameEndState.BeginEnding();

        ShowResult(result);
    }

    public void BeginGameClearAfterDelay(float delaySeconds)
    {
        BeginGameEnd(DHGameEndResult.Clear);
    }

    private int ResolveCurrentTurn()
    {
        if (turnManager != null)
            return Mathf.Max(1, turnManager.GetDay());

        return GameManager.Instance != null ? Mathf.Max(1, GameManager.Instance.CurrentDay) : 1;
    }

    private void ResolveReferences()
    {
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();

        if (gameEndManager == null)
            gameEndManager = FindFirstObjectByType<DHGameEndManager>();
    }

    private void SubscribeTurnManager()
    {
        if (turnManager == null)
            return;

        turnManager.DayAdvanced -= HandleDayAdvanced;
        turnManager.DayAdvanced += HandleDayAdvanced;
    }

    private void SubscribeEventStateRepository()
    {
        if (!enableFlagGameClear)
            return;

        eventStateRepository = DHEventStateRepository.EnsureInstance();
        if (eventStateRepository == null)
            return;

        eventStateRepository.FlagChanged -= HandleEventFlagChanged;
        eventStateRepository.FlagChanged += HandleEventFlagChanged;
    }

    private void ShowResult(DHGameEndResult result)
    {
        if (gameEndManager != null)
        {
            gameEndManager.ShowResult(result);
            return;
        }

        DHGameProgressResetService.ResetDHProgress();
    }
}
