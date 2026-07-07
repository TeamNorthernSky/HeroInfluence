using System.Collections.Generic;
using UnityEngine;

public class VillainUnionBase : MonoBehaviour
{
    [SerializeField] private string baseId = "villain_union_base_001";
    [SerializeField, Min(0)] private int defenderEnemyGroupIndex = 30001;
    [SerializeField] private string defenderEnemyId;
    [SerializeField] private string zoneId;
    [SerializeField] private GridManager gridManager;
    private VillainUnionBaseRegistry villainUnionBaseRegistry;

    public string BaseId => baseId;
    public int DefenderEnemyGroupIndex => defenderEnemyGroupIndex;
    public string DefenderEnemyId => defenderEnemyId;
    public string ZoneId => NormalizeZoneId(zoneId);

    private void Awake()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
    }

    private void OnEnable()
    {
        ResolveReferences();
        villainUnionBaseRegistry?.Register(this);
    }

    private void OnDisable()
    {
        villainUnionBaseRegistry?.Unregister(this);
    }

    public void ApplyZoneIdFromLoader(string loaderZoneId)
    {
        if (!string.IsNullOrWhiteSpace(loaderZoneId))
            zoneId = NormalizeZoneId(loaderZoneId);
    }
    public Vector2Int GetCurrentGrid()
    {
        return gridManager != null ? gridManager.WorldToGrid(transform.position) : Vector2Int.zero;
    }

    public Vector2Int GetAnchorGrid()
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (occupant != null)
            return occupant.AnchorGrid;

        return GetCurrentGrid();
    }

    public IReadOnlyList<Vector2Int> GetAdjacentOuterCells()
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (occupant != null)
            return occupant.GetAdjacentOuterCells();

        Vector2Int origin = GetCurrentGrid();
        List<Vector2Int> adjacentCells = new List<Vector2Int>(GridManager.Directions8.Length);
        for (int i = 0; i < GridManager.Directions8.Length; i++)
            adjacentCells.Add(origin + GridManager.Directions8[i]);

        return adjacentCells;
    }

    public IReadOnlyList<Vector2Int> GetInteractionCells()
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (occupant != null)
        {
            if (occupant.IsTwoByTwo())
                return occupant.GetBottomOuterCells();

            return occupant.GetAdjacentOuterCells();
        }

        Vector2Int origin = GetCurrentGrid();
        List<Vector2Int> adjacentCells = new List<Vector2Int>(GridManager.Directions8.Length);
        for (int i = 0; i < GridManager.Directions8.Length; i++)
            adjacentCells.Add(origin + GridManager.Directions8[i]);

        return adjacentCells;
    }

    public bool IsAdjacentCell(Vector2Int grid)
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (occupant != null)
            return occupant.IsTwoByTwo()
                ? occupant.IsBottomOuterCell(grid)
                : occupant.IsAdjacentOuterCell(grid);

        Vector2Int origin = GetCurrentGrid();
        int dx = Mathf.Abs(grid.x - origin.x);
        int dy = Mathf.Abs(grid.y - origin.y);
        return dx <= 1 && dy <= 1 && (dx != 0 || dy != 0);
    }

    public bool EnsureDefenderParty()
    {
        if (!Application.isPlaying)
            return false;

        EnsureDefenderId();
        bool ensured = VillainUnionDefenderService.EnsureDefenderParty(this);
        return ensured;
    }

    public string GetProgressKey(GridManager targetGridManager)
    {
        return MapProgressKey.ForVillainUnion(GetAnchorGrid());
    }

    public void SetDefenderBinding(int groupIndex, string enemyId)
    {
        if (groupIndex > 0)
            defenderEnemyGroupIndex = groupIndex;

        defenderEnemyId = string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId;
    }

    private static string NormalizeZoneId(string value)
    {
        return MapProgressKey.NormalizeSegment(value);
    }
    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();
    }

    private void EnsureDefenderId()
    {
        if (!string.IsNullOrWhiteSpace(defenderEnemyId))
            return;

        GridManager resolvedGridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        defenderEnemyId = VillainUnionDefenderService.CreateDefenderEnemyId(GetProgressKey(resolvedGridManager));
    }
}

public static class VillainUnionDefenderService
{
    private const int MinCombatSlot = 1;
    private const int MaxCombatSlot = 6;

    public static string CreateDefenderEnemyId(string villainUnionKey)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(villainUnionKey)
            ? "unknown"
            : MapProgressKey.NormalizeSegment(villainUnionKey);
        return $"villain_union_defender_{normalizedKey}";
    }

    public static bool EnsureDefenderParty(VillainUnionBase villainUnionBase)
    {
        if (villainUnionBase == null || villainUnionBase.DefenderEnemyGroupIndex <= 0)
            return false;

        PersistentEnemyRepository enemyRepository = PersistentEnemyRepository.Instance;
        EnemyGroupPersistentRepository enemyGroupRepository = EnemyGroupPersistentRepository.Instance;
        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (enemyRepository == null || enemyGroupRepository == null || templateCatalog == null)
            return false;

        string enemyId = villainUnionBase.DefenderEnemyId;
        if (string.IsNullOrWhiteSpace(enemyId))
        {
            GridManager gridManager = Game.Grid != null ? Game.Grid : Object.FindFirstObjectByType<GridManager>();
            enemyId = CreateDefenderEnemyId(villainUnionBase.GetProgressKey(gridManager));
            villainUnionBase.SetDefenderBinding(villainUnionBase.DefenderEnemyGroupIndex, enemyId);
        }

        if (enemyGroupRepository.ContainsEnemy(enemyId))
            return true;

        if (!templateCatalog.TryGetEnemyGroup(villainUnionBase.DefenderEnemyGroupIndex, out EnemyGroupData groupData) ||
            !TryBuildCsvGroupMembers(groupData, out List<CsvEnemyGroupMember> members))
        {
            Debug.LogWarning(
                $"VillainUnion defender group '{villainUnionBase.DefenderEnemyGroupIndex}' could not be resolved.",
                villainUnionBase);
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
                    $"VillainUnion defender group '{villainUnionBase.DefenderEnemyGroupIndex}' references missing enemy unit '{templateKey}'.",
                    villainUnionBase);
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
        villainUnionBase.SetDefenderBinding(villainUnionBase.DefenderEnemyGroupIndex, enemyId);
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
