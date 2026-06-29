using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class DHGameEndConditionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private EnemyRegistry enemyRegistry;
    [SerializeField] private CastleRegistry castleRegistry;
    [SerializeField] private VillainUnionBaseRegistry villainUnionBaseRegistry;
    [SerializeField] private DHGameEndUIController uiController;

    [Header("Options")]
    [SerializeField] private bool evaluateCurrentPositionsOnStart = true;

    private PartyGridMover subscribedParty;
    private readonly List<EnemyGridMover> subscribedEnemies = new List<EnemyGridMover>();
    private bool isEnding;
    private Coroutine pendingGameEndCoroutine;

    private void Start()
    {
        ResolveReferences();
        SubscribeParties();
        SubscribeEnemies();

        if (enemyRegistry != null)
            enemyRegistry.EnemyRegistered += HandleEnemyRegistered;

        if (evaluateCurrentPositionsOnStart)
            EvaluateCurrentPositions();
    }

    private void OnDestroy()
    {
        UnsubscribeParties();
        UnsubscribeEnemies();

        if (enemyRegistry != null)
            enemyRegistry.EnemyRegistered -= HandleEnemyRegistered;
    }

    private void HandlePartyGridEntered(Vector2Int grid)
    {
        // VillainUnion clear is handled only after final defender combat victory.
    }

    private void HandleEnemyGridChanged(EnemyGridMover enemy, Vector2Int grid)
    {
        if (isEnding)
            return;

        if (IsCastleInteractionCell(grid))
            BeginGameEnd(DHGameEndResult.GameOver);
    }

    private void HandleEnemyRegistered(EnemyGridMover enemy)
    {
        SubscribeEnemy(enemy);

        if (!isEnding && enemy != null && IsCastleInteractionCell(enemy.GetCurrentGrid()))
            BeginGameEnd(DHGameEndResult.GameOver);
    }

    private void BeginGameEnd(DHGameEndResult result)
    {
        if (isEnding)
            return;

        isEnding = true;
        DHGameEndState.BeginEnding();

        if (uiController != null)
            uiController.ShowResult(result);
        else
            DHGameProgressResetService.ResetDHProgress();
    }

    public void BeginGameClearAfterDelay(float delaySeconds)
    {
        BeginGameEndAfterDelay(DHGameEndResult.Clear, delaySeconds);
    }

    private void BeginGameEndAfterDelay(DHGameEndResult result, float delaySeconds)
    {
        if (isEnding)
            return;

        if (delaySeconds <= 0f)
        {
            BeginGameEnd(result);
            return;
        }

        isEnding = true;
        DHGameEndState.BeginEnding();

        pendingGameEndCoroutine = StartCoroutine(ShowGameEndAfterDelay(result, delaySeconds));
    }

    private IEnumerator ShowGameEndAfterDelay(DHGameEndResult result, float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delaySeconds));

        pendingGameEndCoroutine = null;

        if (uiController != null)
            uiController.ShowResult(result);
        else
            DHGameProgressResetService.ResetDHProgress();
    }

    private bool IsVillainUnionInteractionCell(Vector2Int grid)
    {
        IReadOnlyList<VillainUnionBase> bases = villainUnionBaseRegistry != null
            ? villainUnionBaseRegistry.VillainUnionBases
            : FindObjectsByType<VillainUnionBase>(FindObjectsSortMode.None);

        for (int i = 0; i < bases.Count; i++)
        {
            VillainUnionBase villainUnionBase = bases[i];
            if (villainUnionBase == null)
                continue;

            IReadOnlyList<Vector2Int> interactionCells = villainUnionBase.GetInteractionCells();
            if (ContainsGrid(interactionCells, grid))
                return true;
        }

        return false;
    }

    private bool IsCastleInteractionCell(Vector2Int grid)
    {
        IReadOnlyList<CastleUnit> castles = castleRegistry != null
            ? castleRegistry.Castles
            : FindObjectsByType<CastleUnit>(FindObjectsSortMode.None);

        for (int i = 0; i < castles.Count; i++)
        {
            CastleUnit castle = castles[i];
            if (castle != null && castle.IsInteractionCell(grid))
                return true;
        }

        return false;
    }

    private void EvaluateCurrentPositions()
    {
        PartyGridMover party = ResolveParty();
        if (party != null)
            HandlePartyGridEntered(party.GetCurrentGrid());

        if (isEnding)
            return;

        IReadOnlyList<EnemyGridMover> enemies = enemyRegistry != null
            ? enemyRegistry.Enemies
            : FindObjectsByType<EnemyGridMover>(FindObjectsSortMode.None);

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyGridMover enemy = enemies[i];
            if (enemy != null)
                HandleEnemyGridChanged(enemy, enemy.GetCurrentGrid());
        }
    }

    private void SubscribeParties()
    {
        SubscribeParty(ResolveParty());
    }

    private void SubscribeParty(PartyGridMover party)
    {
        if (party == null || subscribedParty == party)
            return;

        UnsubscribeParties();
        party.GridEntered += HandlePartyGridEntered;
        subscribedParty = party;
    }

    private void UnsubscribeParties()
    {
        if (subscribedParty != null)
            subscribedParty.GridEntered -= HandlePartyGridEntered;

        subscribedParty = null;
    }

    private void SubscribeEnemies()
    {
        IReadOnlyList<EnemyGridMover> enemies = enemyRegistry != null
            ? enemyRegistry.Enemies
            : FindObjectsByType<EnemyGridMover>(FindObjectsSortMode.None);

        for (int i = 0; i < enemies.Count; i++)
            SubscribeEnemy(enemies[i]);
    }

    private void SubscribeEnemy(EnemyGridMover enemy)
    {
        if (enemy == null || subscribedEnemies.Contains(enemy))
            return;

        enemy.GridChanged += HandleEnemyGridChanged;
        subscribedEnemies.Add(enemy);
    }

    private void UnsubscribeEnemies()
    {
        for (int i = 0; i < subscribedEnemies.Count; i++)
        {
            EnemyGridMover enemy = subscribedEnemies[i];
            if (enemy != null)
                enemy.GridChanged -= HandleEnemyGridChanged;
        }

        subscribedEnemies.Clear();
    }

    private PartyGridMover ResolveParty()
    {
        return partyRegistry != null ? partyRegistry.PlayerParty : null;
    }

    private void ResolveReferences()
    {
        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();

        if (enemyRegistry == null)
            enemyRegistry = FindFirstObjectByType<EnemyRegistry>();

        if (castleRegistry == null)
            castleRegistry = FindFirstObjectByType<CastleRegistry>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();

        if (uiController == null)
            uiController = FindFirstObjectByType<DHGameEndUIController>();
    }

    private static bool ContainsGrid(IReadOnlyList<Vector2Int> cells, Vector2Int grid)
    {
        if (cells == null)
            return false;

        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] == grid)
                return true;
        }

        return false;
    }
}
