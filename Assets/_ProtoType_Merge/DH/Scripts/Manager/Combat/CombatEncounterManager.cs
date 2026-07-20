using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatEncounterManager : MonoBehaviour
{
    private const float FinalCombatClearDelaySeconds = 0.5f;
    private const int MinPartyCombatSlot = 1;
    private const int MaxPartyCombatSlot = 6;
    private const int MinEnemyCombatSlot = 1;
    private const int MaxEnemyCombatSlot = 6;

    public event Action<PartyGridMover, EnemyGridMover> CombatStarted;

    public bool IsCombatActive { get; private set; }
    public PartyGridMover ActiveParty { get; private set; }
    public EnemyGridMover ActiveEnemy { get; private set; }
    private Coroutine pendingCombatResultCoroutine;

    // [JC 260513] DHScene 재진입 시 직전 전투 결과가 남아 있으면 self-clear.
    // BattleFlowManager는 전투씬에 있어 OnBattleEnded 이벤트 직접 구독 불가 → Result 폴링 방식.
    private void OnEnable()
    {
        CombatContext context = CombatContext.Instance;
        if (context != null && context.Result != CombatResult.None)
        {
            pendingCombatResultCoroutine = StartCoroutine(ProcessCompletedCombatAfterSceneReady());
        }
    }

    private void OnDisable()
    {
        if (pendingCombatResultCoroutine == null)
            return;

        StopCoroutine(pendingCombatResultCoroutine);
        pendingCombatResultCoroutine = null;
    }

    public bool BeginCombat(PartyGridMover party, EnemyGridMover enemy)
    {
        if (DHGameEndState.IsEnding)
            return false;

        if (HasPendingCombatResult())
            return false;

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

        CombatStarted?.Invoke(party, enemy);
        return true;
    }

    public bool PrepareEnemyCombatContext(PartyGridMover party, EnemyGridMover enemy)
    {
        if (DHGameEndState.IsEnding)
            return false;

        if (HasPendingCombatResult())
            return false;

        if (party == null || enemy == null)
            return false;

        if (IsCombatActive)
            return false;

        PartyIdentity partyIdentity = party.GetComponent<PartyIdentity>();
        string partyId = partyIdentity != null ? partyIdentity.PartyId : party.name;
        string enemyId = enemy.EnemyId;

        if (!TryRegisterCombatParticipants(party, partyId, enemy, enemyId))
            return false;

        IsCombatActive = true;
        ActiveParty = party;
        ActiveEnemy = enemy;
        return true;
    }

    public bool BeginOutpostDefenderCombat(PartyGridMover party, Outpost outpost)
    {
        if (DHGameEndState.IsEnding)
            return false;

        if (HasPendingCombatResult())
            return false;

        if (party == null || outpost == null || !outpost.RequiresDefenderCombat)
            return false;

        if (IsCombatActive)
            return false;

        if (!outpost.EnsureDefenderParty())
            return false;

        PartyIdentity partyIdentity = party.GetComponent<PartyIdentity>();
        string partyId = partyIdentity != null ? partyIdentity.PartyId : party.name;
        string enemyId = outpost.DefenderEnemyId;
        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        string outpostKey = outpost.GetProgressKey(gridManager);

        if (!TryRegisterCombatParticipants(party, partyId, enemyId, outpostKey))
            return false;

        IsCombatActive = true;
        ActiveParty = party;
        ActiveEnemy = null;

        CombatStarted?.Invoke(party, null);
        return true;
    }

    public bool PrepareOutpostDefenderCombatContext(PartyGridMover party, Outpost outpost)
    {
        if (DHGameEndState.IsEnding)
            return false;

        if (HasPendingCombatResult())
            return false;

        if (party == null || outpost == null || !outpost.RequiresDefenderCombat)
            return false;

        if (IsCombatActive)
            return false;

        if (!outpost.EnsureDefenderParty())
            return false;

        PartyIdentity partyIdentity = party.GetComponent<PartyIdentity>();
        string partyId = partyIdentity != null ? partyIdentity.PartyId : party.name;
        string enemyId = outpost.DefenderEnemyId;
        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        string outpostKey = outpost.GetProgressKey(gridManager);

        if (!TryRegisterCombatParticipants(party, partyId, enemyId, outpostKey))
            return false;

        IsCombatActive = true;
        ActiveParty = party;
        ActiveEnemy = null;
        return true;
    }

    public bool BeginVillainUnionDefenderCombat(PartyGridMover party, VillainUnionBase villainUnionBase)
    {
        if (DHGameEndState.IsEnding)
            return false;

        if (HasPendingCombatResult())
            return false;

        if (party == null || villainUnionBase == null)
            return false;

        if (IsCombatActive)
            return false;

        if (!villainUnionBase.EnsureDefenderParty())
            return false;

        PartyIdentity partyIdentity = party.GetComponent<PartyIdentity>();
        string partyId = partyIdentity != null ? partyIdentity.PartyId : party.name;
        string enemyId = villainUnionBase.DefenderEnemyId;
        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        string villainUnionKey = villainUnionBase.GetProgressKey(gridManager);

        if (!TryRegisterCombatParticipants(party, partyId, enemyId, villainUnionKey))
            return false;

        IsCombatActive = true;
        ActiveParty = party;
        ActiveEnemy = null;

        CombatStarted?.Invoke(party, null);
        return true;
    }

    public bool PrepareVillainUnionDefenderCombatContext(PartyGridMover party, VillainUnionBase villainUnionBase)
    {
        if (DHGameEndState.IsEnding)
            return false;

        if (HasPendingCombatResult())
            return false;

        if (party == null || villainUnionBase == null)
            return false;

        if (IsCombatActive)
            return false;

        if (!villainUnionBase.EnsureDefenderParty())
            return false;

        PartyIdentity partyIdentity = party.GetComponent<PartyIdentity>();
        string partyId = partyIdentity != null ? partyIdentity.PartyId : party.name;
        string enemyId = villainUnionBase.DefenderEnemyId;
        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        string villainUnionKey = villainUnionBase.GetProgressKey(gridManager);

        if (!TryRegisterCombatParticipants(party, partyId, enemyId, villainUnionKey))
            return false;

        IsCombatActive = true;
        ActiveParty = party;
        ActiveEnemy = null;
        return true;
    }

    public void ApplyCurrentCombatResultAndClear()
    {
        CombatContext context = CombatContext.Instance;
        if (context == null || context.Result == CombatResult.None)
            return;

        ProcessCompletedCombat(context);
        context.Clear();
        ClearCombatState();
    }

    public void ClearCombatState()
    {
        IsCombatActive = false;
        ActiveParty = null;
        ActiveEnemy = null;
    }

    private bool TryRegisterCombatParticipants(PartyGridMover party, string partyId, EnemyGridMover enemy, string enemyId)
    {
        return TryRegisterCombatParticipants(party, partyId, enemyId, ResolveEnemyPlacementKey(enemy), enemy);
    }

    private bool TryRegisterCombatParticipants(
        PartyGridMover party,
        string partyId,
        string enemyId,
        string enemyPlacementKey,
        EnemyGridMover enemy = null)
    {
        CombatContext combatContext = CombatContext.Instance;
        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        PartyPersistentRepository partyRepository = PartyPersistentRepository.Instance;
        PersistentEnemyRepository enemyUnitRepository = PersistentEnemyRepository.Instance;
        EnemyGroupPersistentRepository enemyGroupRepository = EnemyGroupPersistentRepository.Instance;

        if (combatContext == null)
        {
            Debug.LogWarning("CombatContext is missing, so combat participants could not be registered.", this);
            return false;
        }

        if (combatContext.Result != CombatResult.None)
            return false;

        IReadOnlyList<int> partyUnitIndices = FilterCombatReadyPartyUnits(
            ResolvePartyUnitIndices(partyRepository, party, partyId),
            unitRepository);
        IReadOnlyList<int> enemyUnitIndices = FilterCombatReadyEnemyUnits(
            ResolveEnemyUnitIndices(enemyGroupRepository, enemy, enemyId),
            enemyUnitRepository);
        int partyUnitCount = CountValidUnitIndices(partyUnitIndices);
        int enemyUnitCount = CountValidUnitIndices(enemyUnitIndices);
        if (partyUnitCount == 0 || enemyUnitCount == 0)
        {
            Debug.LogWarning(
                $"Combat participant registration failed. partyId='{partyId}' units={partyUnitCount}, enemyId='{enemyId}' units={enemyUnitCount}.",
                this);
            combatContext.Clear();
            return false;
        }

        combatContext.RegisterCombatParty(partyId, partyUnitIndices);
        combatContext.RegisterCombatEnemy(enemyId, enemyPlacementKey, enemyUnitIndices);
        combatContext.SetCombatResult(CombatResult.None);
        return true;
    }

    private static bool HasPendingCombatResult()
    {
        CombatContext context = CombatContext.Instance;
        return context != null && context.Result != CombatResult.None;
    }

    private void ProcessCompletedCombat(CombatContext context)
    {
        if (context == null)
            return;

        if (context.Result == CombatResult.Defeat)
        {
            ReturnDefeatedPartyToHeroUnion(context.CombatParty);
            return;
        }

        if (context.Result != CombatResult.Victory)
            return;

        CombatEnemyPersistentData combatEnemy = context.CombatEnemy;
        if (combatEnemy == null)
            return;

        if (TryCompleteOutpostDefenderCombat(combatEnemy))
            return;

        if (TryCompleteVillainUnionDefenderCombat(combatEnemy))
            return;

        string placementKey = combatEnemy.PlacementKey;
        if (string.IsNullOrWhiteSpace(placementKey))
            placementKey = FindPlacementKeyByEnemyId(combatEnemy.EnemyId);

        if (string.IsNullOrWhiteSpace(placementKey))
            return;

        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        progressRepository?.MarkEnemyDefeated(placementKey);
        GateThreatController.NotifyEnemyDefeated(placementKey);
        RemoveDefeatedEnemyGroup(combatEnemy.EnemyId);
        DestroyMatchingSceneEnemy(placementKey, combatEnemy.EnemyId);
    }

    private IEnumerator ProcessCompletedCombatAfterSceneReady()
    {
        yield return null;
        yield return null;

        pendingCombatResultCoroutine = null;

        CombatContext context = CombatContext.Instance;
        if (context == null || context.Result == CombatResult.None)
            yield break;

        ProcessCompletedCombat(context);
        context.Clear();
        ClearCombatState();
    }

    private static bool TryCompleteOutpostDefenderCombat(CombatEnemyPersistentData combatEnemy)
    {
        if (combatEnemy == null || string.IsNullOrWhiteSpace(combatEnemy.EnemyId))
            return false;

        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        if (progressRepository == null ||
            !progressRepository.TryFindOutpostByDefenderEnemyId(combatEnemy.EnemyId, out OutpostProgressState progressState) ||
            progressState == null)
            return false;

        string outpostKey = progressState.OutpostKey;
        if (!ForceClaimMatchingOutposts(outpostKey))
            progressRepository.SetOutpostState(outpostKey, OutpostState.Claimed, string.Empty, string.Empty);

        OutpostDefenderService.RemoveDefenderParty(combatEnemy.EnemyId);
        return true;
    }

    private static bool TryCompleteVillainUnionDefenderCombat(CombatEnemyPersistentData combatEnemy)
    {
        if (combatEnemy == null || string.IsNullOrWhiteSpace(combatEnemy.EnemyId))
            return false;

        string normalizedCombatPlacementKey = MapProgressKey.NormalizeSegment(combatEnemy.PlacementKey);
        string normalizedCombatEnemyId = MapProgressKey.NormalizeSegment(combatEnemy.EnemyId);
        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        VillainUnionBase[] bases = FindObjectsByType<VillainUnionBase>(FindObjectsSortMode.None);
        for (int i = 0; i < bases.Length; i++)
        {
            VillainUnionBase villainUnionBase = bases[i];
            if (villainUnionBase == null)
                continue;

            string villainUnionKey = villainUnionBase.GetProgressKey(gridManager);
            string normalizedVillainUnionKey = MapProgressKey.NormalizeSegment(villainUnionKey);
            string expectedDefenderId = VillainUnionDefenderService.CreateDefenderEnemyId(villainUnionKey);
            bool defenderIdMatches = !string.IsNullOrWhiteSpace(villainUnionBase.DefenderEnemyId) &&
                string.Equals(villainUnionBase.DefenderEnemyId, combatEnemy.EnemyId, StringComparison.Ordinal);
            bool expectedIdMatches = string.Equals(expectedDefenderId, combatEnemy.EnemyId, StringComparison.Ordinal);
            bool placementKeyMatches = !string.IsNullOrWhiteSpace(normalizedCombatPlacementKey) &&
                string.Equals(normalizedVillainUnionKey, normalizedCombatPlacementKey, StringComparison.Ordinal);

            if (!defenderIdMatches && !expectedIdMatches && !placementKeyMatches)
                continue;

            CompleteVillainUnionVictory(combatEnemy.EnemyId);
            return true;
        }

        if (normalizedCombatPlacementKey.StartsWith("villain_union_", StringComparison.Ordinal) ||
            normalizedCombatEnemyId.StartsWith("villain_union_defender_", StringComparison.Ordinal))
        {
            CompleteVillainUnionVictory(combatEnemy.EnemyId);
            return true;
        }

        return false;
    }

    private static void CompleteVillainUnionVictory(string enemyId)
    {
        VillainUnionDefenderService.RemoveDefenderParty(enemyId);

        DHGameEndConditionController gameEndConditionController = FindFirstObjectByType<DHGameEndConditionController>();
        if (gameEndConditionController != null)
        {
            gameEndConditionController.BeginGameClearAfterDelay(FinalCombatClearDelaySeconds);
            return;
        }

        DHGameProgressResetService.ResetDHProgress();
    }

    private static bool ForceClaimMatchingOutposts(string outpostKey)
    {
        if (string.IsNullOrWhiteSpace(outpostKey))
            return false;

        bool claimedAny = false;
        string normalizedOutpostKey = MapProgressKey.NormalizeSegment(outpostKey);
        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        Outpost[] outposts = FindObjectsByType<Outpost>(FindObjectsSortMode.None);
        for (int i = 0; i < outposts.Length; i++)
        {
            Outpost candidate = outposts[i];
            if (candidate == null)
                continue;

            string candidateKey = MapProgressKey.NormalizeSegment(candidate.GetProgressKey(gridManager));
            if (!string.Equals(candidateKey, normalizedOutpostKey, StringComparison.Ordinal))
                continue;

            candidate.ForceClaimFromCombatResult();
            claimedAny = true;
        }

        return claimedAny;
    }

    private static void ReturnDefeatedPartyToHeroUnion(CombatPartyPersistentData combatParty)
    {
        if (combatParty == null || string.IsNullOrWhiteSpace(combatParty.PartyId))
            return;

        if (!TryFindParty(combatParty.PartyId, out PartyGridMover party))
            return;

        HeroUnionUnit heroUnion = ResolveReturnHeroUnion(party);
        if (heroUnion == null)
            return;

        IReadOnlyList<Vector2Int> interactionCells = heroUnion.GetInteractionCells();
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
        DefeatedPartyReturnController.ScheduleReturn(party);
    }

    private static HeroUnionUnit ResolveReturnHeroUnion(PartyGridMover party)
    {
        if (party == null)
            return null;

        Vector2Int partyGrid = party.GetCurrentGrid();
        HeroUnionRegistry registry = FindFirstObjectByType<HeroUnionRegistry>();
        if (registry != null)
        {
            if (TryResolveZoneId(partyGrid, out string zoneId) &&
                registry.TryGetClaimedByZoneId(zoneId, out HeroUnionUnit zoneHeroUnion))
            {
                return zoneHeroUnion;
            }

            HeroUnionUnit closestClaimedHeroUnion = registry.GetClosestClaimedHeroUnion(partyGrid);
            if (closestClaimedHeroUnion != null)
                return closestClaimedHeroUnion;

            if (registry.TryGetFirstClaimed(out HeroUnionUnit firstClaimedHeroUnion))
                return firstClaimedHeroUnion;

            HeroUnionUnit closestHeroUnion = registry.GetClosestHeroUnion(partyGrid);
            if (closestHeroUnion != null)
                return closestHeroUnion;
        }

        return FindFirstObjectByType<HeroUnionUnit>();
    }

    private static bool TryResolveZoneId(Vector2Int grid, out string zoneId)
    {
        zoneId = string.Empty;

        LevelZoneLayoutLoader layoutLoader = FindFirstObjectByType<LevelZoneLayoutLoader>();
        if (layoutLoader == null)
            return false;

        IReadOnlyList<LoadedLevelZoneData> zones = layoutLoader.LoadedZones;
        for (int i = 0; i < zones.Count; i++)
        {
            LoadedLevelZoneData zone = zones[i];
            Vector2Int min = zone.Anchor;
            Vector2Int max = zone.Anchor + zone.Size - Vector2Int.one;
            if (grid.x < min.x || grid.x > max.x || grid.y < min.y || grid.y > max.y)
                continue;

            zoneId = MapProgressKey.NormalizeSegment(zone.ZoneId);
            return !string.IsNullOrWhiteSpace(zoneId);
        }

        return false;
    }

    private static bool TryFindParty(string partyId, out PartyGridMover party)
    {
        party = null;

        PartyRegistry partyRegistry = FindFirstObjectByType<PartyRegistry>();
        if (partyRegistry != null && partyRegistry.TryGetPartyById(partyId, out party))
            return true;

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
        PartyRegistry partyRegistry = FindFirstObjectByType<PartyRegistry>();
        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        return party != null && party != movingParty && party.GetCurrentGrid() == grid;
    }

    private static IReadOnlyList<int> ResolvePartyUnitIndices(PartyPersistentRepository repository, PartyGridMover party, string partyId)
    {
        if (repository != null && repository.TryGetParty(partyId, out PartyPersistentData partyData))
        {
            IReadOnlyList<int> slotOrderedRepositoryIndices = BuildPartyCombatSlotUnitIndices(
                partyData.UnitIndices,
                partyData.UnitSlots);
            if (CountValidUnitIndices(slotOrderedRepositoryIndices) > 0)
                return slotOrderedRepositoryIndices;
        }

        PartyComposition composition = party != null ? party.GetComponent<PartyComposition>() : null;
        return BuildPartyCombatSlotUnitIndices(
            composition != null ? composition.UnitIndices : Array.Empty<int>(),
            null);
    }

    private static IReadOnlyList<int> ResolveEnemyUnitIndices(EnemyGroupPersistentRepository repository, EnemyGridMover enemy, string enemyId)
    {
        if (repository != null && repository.TryGetEnemy(enemyId, out EnemyPersistentData enemyData))
        {
            IReadOnlyList<int> slotOrderedRepositoryIndices = BuildEnemyCombatSlotUnitIndices(
                enemyData.UnitIndices,
                enemyData.UnitSlots);
            if (CountValidUnitIndices(slotOrderedRepositoryIndices) > 0)
                return slotOrderedRepositoryIndices;
        }

        EnemyComposition composition = enemy != null ? enemy.GetComponent<EnemyComposition>() : null;
        return BuildEnemyCombatSlotUnitIndices(
            composition != null ? composition.UnitIndices : Array.Empty<int>(),
            null);
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

    private static IReadOnlyList<int> BuildPartyCombatSlotUnitIndices(
        IReadOnlyList<int> unitIndices,
        IReadOnlyList<int> unitSlots)
    {
        int[] slotUnitIndices = new int[MaxPartyCombatSlot];
        if (unitIndices == null)
            return slotUnitIndices;

        bool[] occupiedSlots = new bool[MaxPartyCombatSlot];
        for (int i = 0; i < unitIndices.Count; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex <= 0)
                continue;

            int slot = ResolvePartyCombatSlot(unitSlots, i);
            if (slot < MinPartyCombatSlot || slot > MaxPartyCombatSlot)
            {
                Debug.LogWarning($"Party combat slot '{slot}' is out of range. unitIndex={unitIndex}");
                return Array.Empty<int>();
            }

            int slotArrayIndex = slot - 1;
            if (occupiedSlots[slotArrayIndex])
            {
                Debug.LogWarning($"Party combat slot '{slot}' is duplicated.");
                return Array.Empty<int>();
            }

            slotUnitIndices[slotArrayIndex] = unitIndex;
            occupiedSlots[slotArrayIndex] = true;
        }

        return slotUnitIndices;
    }

    private static int ResolvePartyCombatSlot(IReadOnlyList<int> unitSlots, int unitIndexPosition)
    {
        if (unitSlots != null && unitIndexPosition >= 0 && unitIndexPosition < unitSlots.Count)
            return unitSlots[unitIndexPosition];

        return unitIndexPosition + 1;
    }

    private static IReadOnlyList<int> BuildEnemyCombatSlotUnitIndices(
        IReadOnlyList<int> unitIndices,
        IReadOnlyList<int> unitSlots)
    {
        int[] slotUnitIndices = new int[MaxEnemyCombatSlot];
        if (unitIndices == null)
            return slotUnitIndices;

        bool[] occupiedSlots = new bool[MaxEnemyCombatSlot];
        for (int i = 0; i < unitIndices.Count; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex <= 0)
                continue;

            int slot = ResolveEnemyCombatSlot(unitSlots, i);
            if (slot < MinEnemyCombatSlot || slot > MaxEnemyCombatSlot)
            {
                Debug.LogWarning($"Enemy combat slot '{slot}' is out of range. unitIndex={unitIndex}");
                return Array.Empty<int>();
            }

            int slotArrayIndex = slot - 1;
            if (occupiedSlots[slotArrayIndex])
            {
                Debug.LogWarning($"Enemy combat slot '{slot}' is duplicated.");
                return Array.Empty<int>();
            }

            slotUnitIndices[slotArrayIndex] = unitIndex;
            occupiedSlots[slotArrayIndex] = true;
        }

        return slotUnitIndices;
    }

    private static int ResolveEnemyCombatSlot(IReadOnlyList<int> unitSlots, int unitIndexPosition)
    {
        if (unitSlots != null && unitIndexPosition >= 0 && unitIndexPosition < unitSlots.Count)
            return unitSlots[unitIndexPosition];

        return unitIndexPosition + 1;
    }

    private static int CountValidUnitIndices(IReadOnlyList<int> source)
    {
        if (source == null)
            return 0;

        int count = 0;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] > 0)
                count++;
        }

        return count;
    }

    private static IReadOnlyList<int> FilterCombatReadyPartyUnits(
        IReadOnlyList<int> source,
        PersistentUnitRepository repository)
    {
        if (source == null)
            return Array.Empty<int>();

        if (repository == null)
            return source;

        int[] filtered = new int[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            int unitIndex = source[i];
            if (unitIndex <= 0)
                continue;

            if (repository.TryGetUnit(unitIndex, out UnitPersistentData data) &&
                data != null &&
                data.IsIncapacitated)
            {
                continue;
            }

            filtered[i] = unitIndex;
        }

        return filtered;
    }

    private static IReadOnlyList<int> FilterCombatReadyEnemyUnits(
        IReadOnlyList<int> source,
        PersistentEnemyRepository repository)
    {
        if (source == null)
            return Array.Empty<int>();

        if (repository == null)
            return source;

        int[] filtered = new int[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            int unitIndex = source[i];
            if (unitIndex <= 0)
                continue;

            if (repository.TryGetUnit(unitIndex, out EnemyUnitPersistentData data) &&
                data != null &&
                data.IsIncapacitated)
            {
                continue;
            }

            filtered[i] = unitIndex;
        }

        return filtered;
    }
}
