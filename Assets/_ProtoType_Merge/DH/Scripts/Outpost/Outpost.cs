using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class Outpost : MonoBehaviour
{
    public static event Action<Outpost> OutpostClaimed;
    private const int RuntimeEnemyClaimDefenderGroupIndex = 30002;

    [Header("Data")]
    [FormerlySerializedAs("mineType")]
    [SerializeField] private OutpostType outpostType = OutpostType.Bank;
    public int resourcePerTurn;
    [FormerlySerializedAs("mineState")]
    public OutpostState outpostState = OutpostState.Unclaimed;
    [SerializeField, Min(0)] private int enemyDefenderGroupIndex = 30001;
    [SerializeField] private string defenderEnemyId;

    [Header("Visual")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material unclaimedMaterial;
    [SerializeField] private Material enemyClaimedMaterial;
    [SerializeField] private Material claimedMaterial;
    private MultiGridOccupant multiGridOccupant;
    private OutpostRegistry outpostRegistry;

    public bool IsClaimableByPlayer => outpostState == OutpostState.Unclaimed || outpostState == OutpostState.EnemyClaimed;
    public bool CanClaimDirectlyByPlayer => outpostState == OutpostState.Unclaimed;
    public bool RequiresDefenderCombat => outpostState == OutpostState.EnemyClaimed;
    public bool IsPlayerClaimed => outpostState == OutpostState.Claimed;
    public bool IsEnemyClaimed => outpostState == OutpostState.EnemyClaimed;
    public OutpostType OutpostType => outpostType;
    public int EnemyDefenderGroupIndex => enemyDefenderGroupIndex;
    public string DefenderEnemyId => defenderEnemyId;

    private void OnValidate()
    {
        outpostType = OutpostTypeUtility.Normalize(outpostType);
    }

    private void Awake()
    {
        multiGridOccupant = GetComponent<MultiGridOccupant>();

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        ApplyStateMaterial();
    }

    private void OnEnable()
    {
        ResolveOutpostRegistry();
        outpostRegistry?.Register(this);
    }

    private void OnDisable()
    {
        outpostRegistry?.Unregister(this);
    }

    public void Claim()
    {
        if (IsClaimableByPlayer)
            ApplyPlayerClaimedState();
    }

    public void ForceClaimFromCombatResult()
    {
        ApplyPlayerClaimedState();
    }

    public void EnemyClaim()
    {
        EnemyClaim(RuntimeEnemyClaimDefenderGroupIndex);
    }

    public void EnemyClaim(int defenderGroupIndex)
    {
        if (outpostState == OutpostState.EnemyClaimed)
        {
            if (defenderGroupIndex > 0)
                enemyDefenderGroupIndex = defenderGroupIndex;

            EnsureDefenderParty();
            return;
        }

        if (defenderGroupIndex > 0)
            enemyDefenderGroupIndex = defenderGroupIndex;

        outpostState = OutpostState.EnemyClaimed;
        EnsureDefenderId();
        ApplyStateMaterial();
        SaveProgressState();
        EnsureDefenderParty();
    }

    // [JC 260514 머지후처리] GameManager 통합 (a 방식)으로 ResourceManager 직접 인자 폐기. Game.Economy 단축 접근자 사용.
    public void ProduceForTurn()
    {
        if (!IsPlayerClaimed)
            return;

        if (resourcePerTurn <= 0)
            return;

        switch (outpostType)
        {
            case OutpostType.Bank:
                Game.Economy?.Add(ResourceType.Money, resourcePerTurn);
                break;
            case OutpostType.Composite:
                Game.Economy?.Add(ResourceType.Chip, resourcePerTurn);
                Game.Economy?.Add(ResourceType.Crystal, resourcePerTurn);
                Game.Economy?.Add(ResourceType.Supply, resourcePerTurn);
                break;
            case OutpostType.Library:
                Game.Economy?.Add(ResourceType.Chip, resourcePerTurn);
                break;
            case OutpostType.JewelryShop:
                Game.Economy?.Add(ResourceType.Crystal, resourcePerTurn);
                break;
            case OutpostType.BlockStore:
                Game.Economy?.Add(ResourceType.Supply, resourcePerTurn);
                break;
        }
    }

    public void ApplyInitialData(int nextResourcePerTurn, OutpostState nextState)
    {
        ApplyInitialData(outpostType, nextResourcePerTurn, nextState);
    }

    public void ApplyInitialData(OutpostType nextOutpostType, int nextResourcePerTurn, OutpostState nextState)
    {
        outpostType = OutpostTypeUtility.Normalize(nextOutpostType);
        resourcePerTurn = nextResourcePerTurn;
        outpostState = nextState;
        if (outpostState != OutpostState.EnemyClaimed)
            ClearEnemyDefender();
        else
            EnsureDefenderId();

        ApplyStateMaterial();
    }

    public void ApplyProgressData(OutpostState nextState, int nextEnemyDefenderGroupIndex, string nextDefenderEnemyId)
    {
        outpostState = nextState;

        if (nextEnemyDefenderGroupIndex > 0)
            enemyDefenderGroupIndex = nextEnemyDefenderGroupIndex;

        defenderEnemyId = string.IsNullOrWhiteSpace(nextDefenderEnemyId)
            ? string.Empty
            : nextDefenderEnemyId;

        if (outpostState != OutpostState.EnemyClaimed)
            ClearEnemyDefender();
        else
            EnsureDefenderId();

        ApplyStateMaterial();
    }

    public bool EnsureDefenderParty()
    {
        if (!Application.isPlaying || !IsEnemyClaimed)
            return false;

        EnsureDefenderId();
        bool ensured = OutpostDefenderService.EnsureDefenderParty(this);
        SaveProgressState();
        return ensured;
    }

    public string GetProgressKey(GridManager gridManager)
    {
        return MapProgressKey.ForOutpost(GetAnchorGrid(gridManager));
    }

    public void SetDefenderBinding(int groupIndex, string enemyId)
    {
        if (groupIndex > 0)
            enemyDefenderGroupIndex = groupIndex;

        defenderEnemyId = string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId;
    }

    public void ClearEnemyDefender()
    {
        enemyDefenderGroupIndex = 0;
        defenderEnemyId = string.Empty;
    }

    private void ApplyPlayerClaimedState()
    {
        outpostState = OutpostState.Claimed;
        ClearEnemyDefender();
        ApplyStateMaterial();
        SaveProgressState();
        OutpostClaimed?.Invoke(this);
    }

    private void ApplyStateMaterial()
    {
        if (targetRenderer == null)
            return;

        Material nextMaterial = outpostState switch
        {
            OutpostState.Claimed => claimedMaterial,
            OutpostState.EnemyClaimed => enemyClaimedMaterial,
            _ => unclaimedMaterial
        };
        if (nextMaterial == null)
            return;

        Material[] materials = targetRenderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            targetRenderer.sharedMaterials = new[] { nextMaterial };
            return;
        }

        for (int i = 0; i < materials.Length; i++)
            materials[i] = nextMaterial;

        targetRenderer.sharedMaterials = materials;
    }

    private void SaveProgressState()
    {
        if (!Application.isPlaying)
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return;

        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        string outpostKey = GetProgressKey(gridManager);
        repository.SetOutpostState(
            outpostKey,
            outpostState,
            IsEnemyClaimed ? enemyDefenderGroupIndex : 0,
            IsEnemyClaimed ? defenderEnemyId : string.Empty);
    }

    private void EnsureDefenderId()
    {
        if (!string.IsNullOrWhiteSpace(defenderEnemyId))
            return;

        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        defenderEnemyId = OutpostDefenderService.CreateDefenderEnemyId(GetProgressKey(gridManager));
    }

    private void ResolveOutpostRegistry()
    {
        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();
    }

    public Vector2Int GetAnchorGrid(GridManager gridManager)
    {
        if (multiGridOccupant != null)
            return multiGridOccupant.AnchorGrid;

        return gridManager != null
            ? gridManager.WorldToGrid(transform.position)
            : Vector2Int.zero;
    }

    public bool OccupiesGrid(Vector2Int grid, GridManager gridManager)
    {
        if (multiGridOccupant != null)
            return multiGridOccupant.OccupiesCell(grid);

        return GetAnchorGrid(gridManager) == grid;
    }

    public IReadOnlyList<Vector2Int> GetAdjacentInteractionCells(GridManager gridManager)
    {
        if (multiGridOccupant != null)
        {
            if (multiGridOccupant.IsTwoByTwo())
                return multiGridOccupant.GetBottomOuterCells();

            return multiGridOccupant.GetAdjacentOuterCells();
        }

        Vector2Int anchorGrid = GetAnchorGrid(gridManager);
        List<Vector2Int> adjacentCells = new List<Vector2Int>(GridManager.Directions8.Length);
        for (int i = 0; i < GridManager.Directions8.Length; i++)
            adjacentCells.Add(anchorGrid + GridManager.Directions8[i]);

        return adjacentCells;
    }

    public string GetOutpostTypeDisplayName()
    {
        return outpostType switch
        {
            OutpostType.Bank => "Bank",
            OutpostType.Composite => "Composite",
            OutpostType.Library => "Library",
            OutpostType.JewelryShop => "Jewelry Shop",
            OutpostType.BlockStore => "Block Store",
            _ => outpostType.ToString()
        };
    }

    public string GetProductionDisplayText()
    {
        return outpostType switch
        {
            OutpostType.Bank => $"Money +{resourcePerTurn} / turn",
            OutpostType.Composite => $"Chip +{resourcePerTurn}, Crystal +{resourcePerTurn}, Supply +{resourcePerTurn} / turn",
            OutpostType.Library => $"Chip +{resourcePerTurn} / turn",
            OutpostType.JewelryShop => $"Crystal +{resourcePerTurn} / turn",
            OutpostType.BlockStore => $"Supply +{resourcePerTurn} / turn",
            _ => string.Empty
        };
    }
}

public static class OutpostDefenderService
{
    private const int MinCombatSlot = 1;
    private const int MaxCombatSlot = 6;

    public static string CreateDefenderEnemyId(string outpostKey)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(outpostKey)
            ? "unknown"
            : MapProgressKey.NormalizeSegment(outpostKey);
        return $"outpost_defender_{normalizedKey}";
    }

    public static bool EnsureDefenderParty(Outpost outpost)
    {
        if (outpost == null || !outpost.IsEnemyClaimed || outpost.EnemyDefenderGroupIndex <= 0)
            return false;

        PersistentEnemyRepository enemyRepository = PersistentEnemyRepository.Instance;
        EnemyGroupPersistentRepository enemyGroupRepository = EnemyGroupPersistentRepository.Instance;
        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (enemyRepository == null || enemyGroupRepository == null || templateCatalog == null)
            return false;

        string enemyId = outpost.DefenderEnemyId;
        if (string.IsNullOrWhiteSpace(enemyId))
        {
            GridManager gridManager = Game.Grid != null ? Game.Grid : UnityEngine.Object.FindFirstObjectByType<GridManager>();
            enemyId = CreateDefenderEnemyId(outpost.GetProgressKey(gridManager));
            outpost.SetDefenderBinding(outpost.EnemyDefenderGroupIndex, enemyId);
        }

        if (enemyGroupRepository.ContainsEnemy(enemyId))
            return true;

        if (!templateCatalog.TryGetEnemyGroup(outpost.EnemyDefenderGroupIndex, out EnemyGroupData groupData) ||
            !TryBuildCsvGroupMembers(groupData, out List<CsvEnemyGroupMember> members))
        {
            Debug.LogWarning(
                $"Outpost defender group '{outpost.EnemyDefenderGroupIndex}' could not be resolved.",
                outpost);
            return false;
        }

        List<int> unitIndices = new List<int>(members.Count);
        List<int> unitSlots = new List<int>(members.Count);
        for (int i = 0; i < members.Count; i++)
        {
            CsvEnemyGroupMember member = members[i];
            string templateKey = member.EnemyUnitIndex.ToString();
            if (!templateCatalog.TryGetEnemyTemplate(templateKey, out EnemyData template))
            {
                Debug.LogWarning(
                    $"Outpost defender group '{outpost.EnemyDefenderGroupIndex}' references missing enemy unit '{templateKey}'.",
                    outpost);
                continue;
            }

            int unitIndex = enemyRepository.CreateUnit(
                templateKey,
                1,
                template.baseStats,
                template.baseStats,
                template.baseStats.HP);
            unitIndices.Add(unitIndex);
            unitSlots.Add(member.CombatSlot);
        }

        if (unitIndices.Count == 0)
            return false;

        enemyGroupRepository.RegisterOrUpdateEnemy(enemyId, unitIndices, unitSlots);
        outpost.SetDefenderBinding(outpost.EnemyDefenderGroupIndex, enemyId);
        return true;
    }

    public static void RemoveDefenderParty(string enemyId)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            return;

        EnemyGroupPersistentRepository enemyGroupRepository = EnemyGroupPersistentRepository.Instance;
        PersistentEnemyRepository enemyRepository = PersistentEnemyRepository.Instance;
        if (enemyGroupRepository == null)
            return;

        if (enemyGroupRepository.TryGetEnemy(enemyId, out EnemyPersistentData enemyData) &&
            enemyData != null &&
            enemyRepository != null)
        {
            IReadOnlyList<int> unitIndices = enemyData.UnitIndices;
            for (int i = 0; i < unitIndices.Count; i++)
                enemyRepository.RemoveUnit(unitIndices[i]);
        }

        enemyGroupRepository.RemoveEnemy(enemyId);
    }

    private static bool TryBuildCsvGroupMembers(EnemyGroupData groupData, out List<CsvEnemyGroupMember> members)
    {
        members = new List<CsvEnemyGroupMember>(5);

        if (groupData == null)
            return false;

        if (!TryAddCsvMember(members, groupData.Enemy1, groupData.Enemy1Slot) ||
            !TryAddCsvMember(members, groupData.Enemy2, groupData.Enemy2Slot) ||
            !TryAddCsvMember(members, groupData.Enemy3, groupData.Enemy3Slot) ||
            !TryAddCsvMember(members, groupData.Enemy4, groupData.Enemy4Slot) ||
            !TryAddCsvMember(members, groupData.Enemy5, groupData.Enemy5Slot))
            return false;

        return members.Count > 0;
    }

    private static bool TryAddCsvMember(List<CsvEnemyGroupMember> members, int enemyUnitIndex, int combatSlot)
    {
        if (enemyUnitIndex <= 0)
            return true;

        if (combatSlot < MinCombatSlot || combatSlot > MaxCombatSlot)
            return false;

        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].CombatSlot == combatSlot)
                return false;
        }

        members.Add(new CsvEnemyGroupMember(enemyUnitIndex, combatSlot));
        return true;
    }

    private readonly struct CsvEnemyGroupMember
    {
        public CsvEnemyGroupMember(int enemyUnitIndex, int combatSlot)
        {
            EnemyUnitIndex = enemyUnitIndex;
            CombatSlot = combatSlot;
        }

        public int EnemyUnitIndex { get; }
        public int CombatSlot { get; }
    }
}

public class OutpostDefenderController : MonoBehaviour
{
    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(EnsureAfterSceneReady());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(EnsureAfterSceneReady());
    }

    private IEnumerator EnsureAfterSceneReady()
    {
        yield return null;
        yield return null;

        Outpost[] outposts = FindObjectsByType<Outpost>(FindObjectsSortMode.None);
        MapProgressRepository repository = MapProgressRepository.Instance;
        GridManager gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        for (int i = 0; i < outposts.Length; i++)
        {
            if (outposts[i] == null)
                continue;

            if (repository != null &&
                repository.TryGetOutpostProgress(outposts[i].GetProgressKey(gridManager), out OutpostProgressState progressState) &&
                progressState != null)
            {
                outposts[i].ApplyProgressData(
                    progressState.State,
                    progressState.EnemyDefenderGroupIndex,
                    progressState.DefenderEnemyId);
            }

            outposts[i]?.EnsureDefenderParty();
        }
    }
}

public static class OutpostDefenderControllerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureController()
    {
        if (UnityEngine.Object.FindFirstObjectByType<OutpostDefenderController>() != null)
            return;

        GameObject go = new GameObject("[OutpostDefenderController]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<OutpostDefenderController>();
    }
}
