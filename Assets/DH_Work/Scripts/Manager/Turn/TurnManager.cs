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
    [SerializeField] private ResourceManager resourceManager;
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

        UpdateTurnStateText("Player Turn");
    }

    public void EndPlayerTurn()
    {
        StartEnemyTurn();
    }

    private void StartEnemyTurn()
    {
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
        AdvanceDay();
        ProduceClaimedOutposts();
        StartPlayerTurn();
    }

    private void StartPlayerTurn()
    {
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

            outpost.ProduceForTurn(resourceManager);
        }
    }

    public int GetDay()
    {
        return day;
    }

    private IEnumerator RunEnemyTurn()
    {
        yield return enemyTurnController.ExecuteEnemyTurn();
        enemyTurnRunning = false;
        EnemyTurnStateChanged?.Invoke(false);
        EndEnemyTurn();
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
