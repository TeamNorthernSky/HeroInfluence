using System;
using System.Collections.Generic;
using UnityEngine;

public class CombatEncounterManager : MonoBehaviour
{
    public event Action<PartyGridMover, EnemyGridMover> CombatStarted;

    public bool IsCombatActive { get; private set; }
    public PartyGridMover ActiveParty { get; private set; }
    public EnemyGridMover ActiveEnemy { get; private set; }

    // [JC 260513] DHScene 재진입 시 직전 전투 결과가 남아 있으면 self-clear.
    // BattleFlowManager는 전투씬에 있어 OnBattleEnded 이벤트 직접 구독 불가 → Result 폴링 방식.
    private void OnEnable()
    {
        CombatContext context = CombatContext.Instance;
        if (context != null && context.Result != CombatResult.None)
        {
            ProcessCompletedCombat(context);
            ClearCombatState();
        }
    }

    public bool BeginCombat(PartyGridMover party, EnemyGridMover enemy)
    {
        if (party == null || enemy == null)
            return false;

        // [JC 추가 260514 R-4] 중복 트리거 가드 — PartyInteractionController·EnemyTurnController 양쪽에서 같은 프레임 호출 시 CombatStarted 이중 발화 방지
        if (IsCombatActive) return false;

        PartyIdentity partyIdentity = party.GetComponent<PartyIdentity>();
        string partyId = partyIdentity != null ? partyIdentity.PartyId : party.name;
        string enemyId = enemy.EnemyId;

        if (!TryRegisterCombatParticipants(party, partyId, enemy, enemyId))
            return false;

        // [Orora 260514] IsCombatActive 활성 흐름 + true 반환 (직전 사이클 대비 변경)
        IsCombatActive = true;
        ActiveParty = party;
        ActiveEnemy = enemy;

        Debug.Log(
            $"Combat encounter requested between party '{partyId}' and enemy '{enemyId}'. " +
            "Combat flow is not implemented yet.",
            this);
        CombatStarted?.Invoke(party, enemy);
        return true;
    }

    public void ClearCombatState()
    {
        IsCombatActive = false;
        ActiveParty = null;
        ActiveEnemy = null;
    }

    private bool TryRegisterCombatParticipants(PartyGridMover party, string partyId, EnemyGridMover enemy, string enemyId)
    {
        CombatContext combatContext = CombatContext.Instance;
        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        PartyPersistentRepository partyRepository = PartyPersistentRepository.Instance;
        EnemyGroupPersistentRepository enemyGroupRepository = EnemyGroupPersistentRepository.Instance;

        if (combatContext == null)
        {
            Debug.LogWarning("CombatContext is missing, so combat participants could not be registered.", this);
            return false;
        }

        IReadOnlyList<int> partyUnitIndices = ResolvePartyUnitIndices(partyRepository, party, partyId);
        IReadOnlyList<int> enemyUnitIndices = ResolveEnemyUnitIndices(enemyGroupRepository, enemy, enemyId);
        if (partyUnitIndices.Count == 0 || enemyUnitIndices.Count == 0)
        {
            Debug.LogWarning(
                $"Combat participant registration failed. partyId='{partyId}' units={partyUnitIndices.Count}, enemyId='{enemyId}' units={enemyUnitIndices.Count}.",
                this);
            combatContext.Clear();
            return false;
        }

        combatContext.RegisterCombatParty(partyId, partyUnitIndices);
        combatContext.RegisterCombatEnemy(enemyId, ResolveEnemyPlacementKey(enemy), enemyUnitIndices);
        combatContext.SetCombatResult(CombatResult.None);
        return true;
    }

    private void ProcessCompletedCombat(CombatContext context)
    {
        if (context == null)
            return;

        if (context.Result == CombatResult.Defeat)
        {
            ReturnDefeatedPartyToCastle(context.CombatParty);
            return;
        }

        if (context.Result != CombatResult.Victory)
            return;

        CombatEnemyPersistentData combatEnemy = context.CombatEnemy;
        if (combatEnemy == null)
            return;

        string placementKey = combatEnemy.PlacementKey;
        if (string.IsNullOrWhiteSpace(placementKey))
            placementKey = FindPlacementKeyByEnemyId(combatEnemy.EnemyId);

        if (string.IsNullOrWhiteSpace(placementKey))
            return;

        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        progressRepository?.MarkEnemyDefeated(placementKey);
        RemoveDefeatedEnemyGroup(combatEnemy.EnemyId);
        DestroyMatchingSceneEnemy(placementKey, combatEnemy.EnemyId);
    }

    private static void ReturnDefeatedPartyToCastle(CombatPartyPersistentData combatParty)
    {
        if (combatParty == null || string.IsNullOrWhiteSpace(combatParty.PartyId))
            return;

        if (!TryFindParty(combatParty.PartyId, out PartyGridMover party))
            return;

        CastleUnit castle = FindFirstObjectByType<CastleUnit>();
        if (castle == null)
            return;

        IReadOnlyList<Vector2Int> interactionCells = castle.GetInteractionCells();
        if (interactionCells == null || interactionCells.Count == 0)
            return;

        Vector2Int leftCell = GetLeftmostCell(interactionCells);
        Vector2Int targetCell = leftCell;
        if (IsOccupiedByOtherParty(leftCell, party) &&
            TryGetAlternativeCell(interactionCells, leftCell, party, out Vector2Int alternativeCell))
        {
            targetCell = alternativeCell;
        }

        party.SnapToGridPosition(targetCell, notifyMoveCompleted: false);
    }

    private static bool TryFindParty(string partyId, out PartyGridMover party)
    {
        party = null;

        PartyRegistry partyRegistry = FindFirstObjectByType<PartyRegistry>();
        if (partyRegistry != null && partyRegistry.TryGetPartyById(partyId, out party))
            return true;

        PartyGridMover[] parties = FindObjectsByType<PartyGridMover>(FindObjectsSortMode.None);
        for (int i = 0; i < parties.Length; i++)
        {
            PartyGridMover candidate = parties[i];
            if (candidate == null)
                continue;

            PartyIdentity identity = candidate.GetComponent<PartyIdentity>();
            if (identity == null)
                continue;

            if (!string.Equals(identity.PartyId, partyId, StringComparison.Ordinal))
                continue;

            party = candidate;
            return true;
        }

        return false;
    }

    private static Vector2Int GetLeftmostCell(IReadOnlyList<Vector2Int> cells)
    {
        Vector2Int bestCell = cells[0];
        for (int i = 1; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            if (cell.x < bestCell.x || cell.x == bestCell.x && cell.y < bestCell.y)
                bestCell = cell;
        }

        return bestCell;
    }

    private static bool TryGetAlternativeCell(
        IReadOnlyList<Vector2Int> cells,
        Vector2Int excludedCell,
        PartyGridMover movingParty,
        out Vector2Int alternativeCell)
    {
        alternativeCell = default;
        bool hasCandidate = false;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            if (cell == excludedCell)
                continue;

            if (IsOccupiedByOtherParty(cell, movingParty))
                continue;

            if (!hasCandidate || cell.x < alternativeCell.x || cell.x == alternativeCell.x && cell.y < alternativeCell.y)
            {
                alternativeCell = cell;
                hasCandidate = true;
            }
        }

        return hasCandidate;
    }

    private static bool IsOccupiedByOtherParty(Vector2Int grid, PartyGridMover movingParty)
    {
        PartyGridMover[] parties = FindObjectsByType<PartyGridMover>(FindObjectsSortMode.None);
        for (int i = 0; i < parties.Length; i++)
        {
            PartyGridMover party = parties[i];
            if (party == null || party == movingParty)
                continue;

            if (party.GetCurrentGrid() == grid)
                return true;
        }

        return false;
    }

    private static IReadOnlyList<int> ResolvePartyUnitIndices(PartyPersistentRepository repository, PartyGridMover party, string partyId)
    {
        if (repository != null && repository.TryGetParty(partyId, out PartyPersistentData partyData))
        {
            IReadOnlyList<int> filteredRepositoryIndices = FilterValidUnitIndices(partyData.UnitIndices);
            if (filteredRepositoryIndices.Count > 0)
                return filteredRepositoryIndices;
        }

        PartyComposition composition = party != null ? party.GetComponent<PartyComposition>() : null;
        return FilterValidUnitIndices(composition != null ? composition.UnitIndices : Array.Empty<int>());
    }

    private static IReadOnlyList<int> ResolveEnemyUnitIndices(EnemyGroupPersistentRepository repository, EnemyGridMover enemy, string enemyId)
    {
        if (repository != null && repository.TryGetEnemy(enemyId, out EnemyPersistentData enemyData))
        {
            IReadOnlyList<int> filteredRepositoryIndices = FilterValidUnitIndices(enemyData.UnitIndices);
            if (filteredRepositoryIndices.Count > 0)
                return filteredRepositoryIndices;
        }

        EnemyComposition composition = enemy != null ? enemy.GetComponent<EnemyComposition>() : null;
        return FilterValidUnitIndices(composition != null ? composition.UnitIndices : Array.Empty<int>());
    }

    private static string ResolveEnemyPlacementKey(EnemyGridMover enemy)
    {
        EnemyIdentity identity = enemy != null ? enemy.GetComponent<EnemyIdentity>() : null;
        return identity != null ? identity.PlacementKey : string.Empty;
    }

    private static void RemoveDefeatedEnemyGroup(string enemyId)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            return;

        EnemyGroupPersistentRepository enemyGroupRepository = EnemyGroupPersistentRepository.Instance;
        enemyGroupRepository?.RemoveEnemy(enemyId);
    }

    private static string FindPlacementKeyByEnemyId(string enemyId)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            return string.Empty;

        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        if (progressRepository == null)
            return string.Empty;

        IReadOnlyList<EnemyWorldState> enemyStates = progressRepository.EnemyWorldStates;
        for (int i = 0; i < enemyStates.Count; i++)
        {
            EnemyWorldState state = enemyStates[i];
            if (state == null)
                continue;

            if (string.Equals(state.EnemyId, enemyId, StringComparison.Ordinal))
                return state.PlacementKey;
        }

        return string.Empty;
    }

    private static void DestroyMatchingSceneEnemy(string placementKey, string enemyId)
    {
        EnemyGridMover[] enemies = FindObjectsByType<EnemyGridMover>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyGridMover enemy = enemies[i];
            if (enemy == null)
                continue;

            EnemyIdentity identity = enemy.GetComponent<EnemyIdentity>();
            if (identity == null)
                continue;

            bool placementMatches = !string.IsNullOrWhiteSpace(placementKey) &&
                string.Equals(identity.PlacementKey, placementKey, StringComparison.Ordinal);
            bool enemyIdMatches = !string.IsNullOrWhiteSpace(enemyId) &&
                string.Equals(identity.EnemyId, enemyId, StringComparison.Ordinal);

            if (!placementMatches && !enemyIdMatches)
                continue;

            Destroy(enemy.gameObject);
        }
    }

    private static IReadOnlyList<int> FilterValidUnitIndices(IReadOnlyList<int> source)
    {
        List<int> validUnitIndices = new List<int>();
        if (source == null)
            return validUnitIndices;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] > 0)
                validUnitIndices.Add(source[i]);
        }

        return validUnitIndices;
    }
}
