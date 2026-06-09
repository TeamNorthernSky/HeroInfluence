using System.Collections.Generic;
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
    [SerializeField] private bool logGameEnd;

    private readonly List<PartyGridMover> subscribedParties = new List<PartyGridMover>();
    private readonly List<EnemyGridMover> subscribedEnemies = new List<EnemyGridMover>();
    private bool isEnding;

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
        if (isEnding)
            return;

        if (IsVillainUnionInteractionCell(grid))
            BeginGameEnd(DHGameEndResult.Clear);
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

        if (logGameEnd)
            Debug.Log($"[DHGameEndConditionController] Game end result: {result}", this);

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
        PartyGridMover[] parties = ResolveParties();
        for (int i = 0; i < parties.Length; i++)
        {
            PartyGridMover party = parties[i];
            if (party != null)
                HandlePartyGridEntered(party.GetCurrentGrid());
        }

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
        PartyGridMover[] parties = ResolveParties();
        for (int i = 0; i < parties.Length; i++)
            SubscribeParty(parties[i]);
    }

    private void SubscribeParty(PartyGridMover party)
    {
        if (party == null || subscribedParties.Contains(party))
            return;

        party.GridEntered += HandlePartyGridEntered;
        subscribedParties.Add(party);
    }

    private void UnsubscribeParties()
    {
        for (int i = 0; i < subscribedParties.Count; i++)
        {
            PartyGridMover party = subscribedParties[i];
            if (party != null)
                party.GridEntered -= HandlePartyGridEntered;
        }

        subscribedParties.Clear();
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

    private PartyGridMover[] ResolveParties()
    {
        if (partyRegistry != null && partyRegistry.PartyMovers.Length > 0)
            return partyRegistry.PartyMovers;

        return FindObjectsByType<PartyGridMover>(FindObjectsSortMode.None);
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
