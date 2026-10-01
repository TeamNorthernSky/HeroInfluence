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

    private bool isEnding;

    private void Start()
    {
        ResolveReferences();
        SubscribeTurnManager();

        if (evaluateCurrentTurnOnStart)
            EvaluateTurnLimit(ResolveCurrentTurn());
    }

    private void OnDestroy()
    {
        if (turnManager != null)
            turnManager.DayAdvanced -= HandleDayAdvanced;
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
