using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatEncounterManager : MonoBehaviour
{
    private const float FinalCombatClearDelaySeconds = 0.5f;
    private const int MinPartyCombatSlot = 1;
    private const int MaxPartyCombatSlot = 6;

    public event Action<PartyGridMover, EnemyGridMover> CombatStarted;

    public bool IsCombatActive { get; private set; }
    public PartyGridMover ActiveParty { get; private set; }
    public EnemyGridMover ActiveEnemy { get; private set; }
    private Coroutine pendingCombatResultCoroutine;

    // [JC 260513] DHScene ??? ? ?? ?? ??? ?? ??? self-clear.
    // BattleFlowManager? ???? ?? OnBattleEnded ??? ?? ?? ?? ? Result ?? ??.
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

        // [JC ?? 260514 R-4] ?? ??? ?? ? PartyInteractionController?EnemyTurnController ???? ?? ??? ?? ? CombatStarted ?? ?? ??
        if (IsCombatActive) return false;

        PartyIdentity partyIdentity = party.GetComponent<PartyIdentity>();
        string partyId = partyIdentity != null ? partyIdentity.PartyId : party.name;
        string enemyId = enemy.EnemyId;

        if (!TryRegisterCombatParticipants(party, partyId, enemy, enemyId))
            return false;

        // [Orora 260514] IsCombatActive ?? ?? + true ?? (?? ??? ?? ??)
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
        string enemyGroupKey = outpost.EnemyDefenderGroupKey;
        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        string outpostKey = outpost.GetProgressKey(gridManager);

        if (!TryRegisterCombatParticipants(
                party,
                partyId,
                enemyId,
                outpostKey,
                enemyGroupKey,
                outpost.ResolvedEnemyLevel,
                CombatEnemySourceType.OutpostDefender))
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
        string enemyGroupKey = outpost.EnemyDefenderGroupKey;
        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        string outpostKey = outpost.GetProgressKey(gridManager);

        if (!TryRegisterCombatParticipants(
                party,
                partyId,
                enemyId,
                outpostKey,
                enemyGroupKey,
                outpost.ResolvedEnemyLevel,
                CombatEnemySourceType.OutpostDefender))
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
        string enemyGroupKey = villainUnionBase.DefenderEnemyGroupKey;
        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        string villainUnionKey = villainUnionBase.GetProgressKey(gridManager);

        if (!TryRegisterCombatParticipants(
                party,
                partyId,
                enemyId,
                villainUnionKey,
                enemyGroupKey,
                villainUnionBase.ResolvedEnemyLevel,
                CombatEnemySourceType.VillainUnionDefender))
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
        string enemyGroupKey = villainUnionBase.DefenderEnemyGroupKey;
        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        string villainUnionKey = villainUnionBase.GetProgressKey(gridManager);

        if (!TryRegisterCombatParticipants(
                party,
                partyId,
                enemyId,
                villainUnionKey,
                enemyGroupKey,
                villainUnionBase.ResolvedEnemyLevel,
                CombatEnemySourceType.VillainUnionDefender))
            return false;

        IsCombatActive = true;
        ActiveParty = party;
        ActiveEnemy = null;
        return true;
    }

    public bool BeginEventBattleCombat(PartyGridMover party, CombatEventBattleData eventBattle)
    {
        if (DHGameEndState.IsEnding)
            return false;

        if (HasPendingCombatResult())
            return false;

        if (party == null || eventBattle == null || string.IsNullOrWhiteSpace(eventBattle.BattleKey))
            return false;

        if (IsCombatActive)
            return false;

        CombatContext combatContext = CombatContext.Instance;
        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        PartyPersistentRepository partyRepository = PartyPersistentRepository.Instance;
        if (combatContext == null)
        {
            Debug.LogWarning("CombatContext is missing, so event battle participants could not be registered.", this);
            return false;
        }

        if (combatContext.Result != CombatResult.None)
            return false;

        PartyIdentity partyIdentity = party.GetComponent<PartyIdentity>();
        string partyId = partyIdentity != null ? partyIdentity.PartyId : party.name;
        IReadOnlyList<int> partyUnitIndices = FilterCombatReadyPartyUnits(
            ResolvePartyUnitIndices(partyRepository, party, partyId),
            unitRepository);

        int partyUnitCount = CountValidUnitIndices(partyUnitIndices);
        if (partyUnitCount == 0)
        {
            Debug.LogWarning($"Event battle participant registration failed. partyId='{partyId}' units={partyUnitCount}.", this);
            combatContext.Clear();
            return false;
        }

        combatContext.RegisterCombatParty(partyId, partyUnitIndices);
        combatContext.RegisterEventBattle(eventBattle);
        combatContext.SetCombatResult(CombatResult.None);

        IsCombatActive = true;
        ActiveParty = party;
        ActiveEnemy = null;

        CombatStarted?.Invoke(party, null);
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
        return TryRegisterCombatParticipants(
            party,
            partyId,
            enemyId,
            ResolveEnemyPlacementKey(enemy),
            ResolveEnemyGroupKey(enemy),
            ResolveEnemyLevel(enemy),
            CombatEnemySourceType.Field,
            enemy);
    }

    private bool TryRegisterCombatParticipants(
        PartyGridMover party,
        string partyId,
        string enemyId,
        string enemyPlacementKey,
        string enemyGroupKey,
        int enemyLevel,
        CombatEnemySourceType enemySourceType,
        EnemyGridMover enemy = null)
    {
        CombatContext combatContext = CombatContext.Instance;
        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        PartyPersistentRepository partyRepository = PartyPersistentRepository.Instance;
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
        int partyUnitCount = CountValidUnitIndices(partyUnitIndices);
        bool hasEnemyGroup = CombatEnemyTemplatePreviewBuilder.TryBuild(
            enemyGroupKey,
            enemyLevel,
            out IReadOnlyList<CombatEnemyTemplatePreviewUnit> enemyPreviewUnits);
        int enemyUnitCount = CountValidEnemyPreviewUnits(enemyPreviewUnits);
        if (partyUnitCount == 0 || enemyUnitCount == 0)
        {
            Debug.LogWarning(
                $"Combat participant registration failed. partyId='{partyId}' units={partyUnitCount}, enemyId='{enemyId}', group='{enemyGroupKey}', groupResolved={hasEnemyGroup}, units={enemyUnitCount}.",
                this);
            combatContext.Clear();
            return false;
        }

        combatContext.RegisterCombatParty(partyId, partyUnitIndices);
        combatContext.RegisterCombatEnemy(enemyId, enemyPlacementKey, enemyGroupKey, enemyLevel, enemySourceType);
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

        if (context.CombatEnemy != null &&
            context.CombatEnemy.SourceType == CombatEnemySourceType.Simulation)
        {
            context.ClearSimulation();
            return;
        }

        if (context.HasEventBattle)
        {
            DHEventBattleRuntimeManager.HandleCompletedEventBattle(context);
            if (context.Result == CombatResult.Defeat)
                ReturnDefeatedPartyToHeroUnion(context.CombatParty);

            return;
        }

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

        // TODO(remove fallback): multiple match paths cover older repository combat ids; keep until defender combat uses a single event/group key.
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

        Vector2Int centerCell = GetCenterCell(interactionCells);
        Vector2Int targetCell = centerCell;
        if (IsOccupiedByOtherParty(centerCell, party) &&
            TryGetAlternativeCell(interactionCells, centerCell, party, out Vector2Int alternativeCell))
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

    private static Vector2Int GetCenterCell(IReadOnlyList<Vector2Int> cells)
    {
        Vector2Int bestCell = cells[0];
        int minX = cells[0].x;
        int maxX = cells[0].x;
        int minY = cells[0].y;
        int maxY = cells[0].y;

        for (int i = 1; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;
        float bestDistance = SquaredDistanceToCenter(bestCell, centerX, centerY);
        for (int i = 1; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            float distance = SquaredDistanceToCenter(cell, centerX, centerY);
            if (distance < bestDistance ||
                Mathf.Approximately(distance, bestDistance) &&
                (cell.x < bestCell.x || cell.x == bestCell.x && cell.y < bestCell.y))
            {
                bestCell = cell;
                bestDistance = distance;
            }
        }

        return bestCell;
    }

    private static float SquaredDistanceToCenter(Vector2Int cell, float centerX, float centerY)
    {
        float dx = cell.x - centerX;
        float dy = cell.y - centerY;
        return dx * dx + dy * dy;
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

    private static string ResolveEnemyPlacementKey(EnemyGridMover enemy)
    {
        EnemyIdentity identity = enemy != null ? enemy.GetComponent<EnemyIdentity>() : null;
        return identity != null ? identity.PlacementKey : string.Empty;
    }

    private static string ResolveEnemyGroupKey(EnemyGridMover enemy)
    {
        EnemyIdentity identity = enemy != null ? enemy.GetComponent<EnemyIdentity>() : null;
        return identity != null ? identity.EnemyGroupKey : string.Empty;
    }

    private static int ResolveEnemyLevel(EnemyGridMover enemy)
    {
        if (enemy != null && TryResolveZoneId(enemy.GetCurrentGrid(), out string gridZoneId))
        {
            MapProgressRepository repository = MapProgressRepository.Instance;
            if (repository != null && repository.TryGetZoneEnemyLevel(gridZoneId, out int gridZoneLevel))
                return gridZoneLevel;
        }

        string placementKey = ResolveEnemyPlacementKey(enemy);
        if (!string.IsNullOrWhiteSpace(placementKey))
        {
            MapProgressRepository repository = MapProgressRepository.Instance;
            IReadOnlyList<EnemyWorldState> enemyStates = repository != null ? repository.EnemyWorldStates : null;
            if (enemyStates != null)
            {
                for (int i = 0; i < enemyStates.Count; i++)
                {
                    EnemyWorldState state = enemyStates[i];
                    if (state == null ||
                        !string.Equals(state.PlacementKey, placementKey, StringComparison.Ordinal))
                        continue;

                    if (repository.TryGetZoneEnemyLevel(state.ZoneId, out int stateZoneLevel))
                        return stateZoneLevel;

                    break;
                }
            }
        }

        return 1;
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
        bool hasPlacementKey = !string.IsNullOrWhiteSpace(placementKey);
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

            bool matches = hasPlacementKey ? placementMatches : enemyIdMatches;
            if (!matches)
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

    private static int CountValidEnemyPreviewUnits(IReadOnlyList<CombatEnemyTemplatePreviewUnit> source)
    {
        if (source == null)
            return 0;

        int count = 0;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i].IsValid)
                count++;
        }

        return count;
    }
}
