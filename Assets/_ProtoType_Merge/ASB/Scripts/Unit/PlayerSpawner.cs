using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ASB.Work.BattleGrid;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

/// <summary>
/// PlayerPlace 최상위에 부착. Grid/Grid_n에서 월드 위치만 참조하고, 유닛은 Units 자식으로 둡니다.
/// 프리팹은 Resources/prefab/BattlePrefab/PlayerUnit 아래에서 Unit_{UnitType}_{Index} 규칙으로 로드합니다.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [Serializable]
    public struct SpawnRequest
    {
        public string unitId;
        public int gridNumber;
        public bool isPlayer;
    }

    [Header("Dependencies")]
    [SerializeField] private BattleUnitPrefabCatalog prefabCatalog;
    [SerializeField] private TmpBattlePrefabManager prefabManager;
    // charactorManager 제거 — DHCsvTemplateCatalog.Instance 로 대체

    [Header("Inspector Test / Battle debug spawn")]
    public List<SpawnRequest> debugSpawnRequests = new List<SpawnRequest>();

    [SerializeField] private bool spawnOnStart;

    private Transform unitParent;
    private readonly Dictionary<int, Vector3> gridSlots = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, Quaternion> gridRotations = new Dictionary<int, Quaternion>();
    private readonly Dictionary<int, GameObject> spawnedByGrid = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GridCellRef> gridCellsByNumber = new Dictionary<int, GridCellRef>();
    private bool hierarchyReady;
    private bool prefabCatalogMismatchReported;

    private void Awake()
    {
        gridSlots.Clear();
        gridRotations.Clear();
        gridCellsByNumber.Clear();
        unitParent = null;
        hierarchyReady = false;

        unitParent = transform.Find("UnitContainer");
        if (unitParent == null)
        {
            var created = new GameObject("UnitContainer");
            created.transform.SetParent(transform, false);
            unitParent = created.transform;
        }
        if (unitParent == null)
        {
            Debug.LogError($"[{gameObject.name}] 'Units' 자식 오브젝트를 찾을 수 없습니다.");
            return;
        }

        // GridCell 컴포넌트를 기준으로 재귀 슬롯을 모두 수집합니다.
        GridCellRef[] cells = GetComponentsInChildren<GridCellRef>(true);
        for (int i = 0; i < cells.Length; i++)
        {
            GridCellRef cell = cells[i];
            if (cell == null) continue;

            if (!TryResolveGridNumber(cell.transform, out int gridNumber)) continue;

            if (gridSlots.ContainsKey(gridNumber)) continue;

            gridSlots[gridNumber] = cell.transform.position;
            gridRotations[gridNumber] = cell.transform.rotation;
            gridCellsByNumber[gridNumber] = cell;
        }

        hierarchyReady = true;
    }

    private void Start()
    {
        // 스포너는 BattleSceneManager가 수동 호출(ManualSpawn)로 실행을 제어합니다.
    }

    public void SetSpawnOnStart(bool enabled)
    {
        spawnOnStart = enabled;
    }

    public bool ManualSpawn()
    {
        CombatContext combatContext = CombatContext.Instance;
        if (combatContext != null && combatContext.IsSimulation)
            return SpawnFromSimulationContext(combatContext);

        // 튜토리얼: 탐사에서 넘어온 아군 런타임 데이터로 우선 스폰. 실패 시 아래 폴백으로 진행한다.
        if (combatContext != null && combatContext.IsTutorial && SpawnFromTutorialContext(combatContext))
            return true;

        // Persistent 우선 스폰. 실패 시 기존 디버그(=CSV 폴백 역할) 리스트로 폴백합니다.
        if (SpawnFromPersistentRepository())
        {
            return true;
        }

        bool spawnedAny = false;
        if (debugSpawnRequests != null)
        {
            for (int i = 0; i < debugSpawnRequests.Count; i++)
            {
                var req = debugSpawnRequests[i];
                if (string.IsNullOrWhiteSpace(req.unitId)) continue;

                if (SpawnUnit(req.unitId, req.gridNumber) != null)
                {
                    spawnedAny = true;
                }
            }
        }

        return spawnedAny;
    }

    private GameObject FindPrefab(UnitData unit)
    {
        if (unit == null || string.IsNullOrWhiteSpace(unit.Index))
        {
            Debug.LogError("[PlayerSpawner] Index가 비어 있거나 UnitData가 없습니다.");
            return null;
        }

        string trimmedIndex = unit.Index.Trim();
        if (TryGetRegisteredPlayerPrefab(trimmedIndex, out GameObject registeredPrefab))
            return registeredPrefab;

        string suffix = $"_{trimmedIndex}";

        GameObject[] all = Resources.LoadAll<GameObject>("prefab/BattlePrefab/PlayerUnit");
        foreach (GameObject prefab in all)
        {
            if (prefab.name.EndsWith(suffix))
                return prefab;
        }

        Debug.LogError($"[PlayerSpawner] Prefab not found. Index={trimmedIndex}");
        return null;
    }


    private bool TryGetRegisteredPlayerPrefab(string unitKey, out GameObject prefab)
    {
        prefab = null;

        TmpBattlePrefabManager resolver = prefabManager != null
            ? prefabManager
            : TmpBattlePrefabManager.Instance;
        if (resolver != null)
            ReportCatalogMismatchOnce(resolver);

        if (prefabCatalog != null && prefabCatalog.TryGetPlayerPrefab(unitKey, out prefab))
            return true;

        return resolver != null && resolver.TryGetPlayerPrefab(unitKey, out prefab);
    }

    private void ReportCatalogMismatchOnce(TmpBattlePrefabManager resolver)
    {
        if (prefabCatalogMismatchReported || prefabCatalog == null || resolver == null ||
            resolver.Catalog == null || resolver.Catalog == prefabCatalog)
        {
            return;
        }

        prefabCatalogMismatchReported = true;
        Debug.LogWarning(
            "[PlayerSpawner] Direct Catalog and TmpBattlePrefabManager Catalog differ. " +
            "The directly assigned Catalog has priority.",
            this);
    }

    /// <summary>
    /// 점유된 셀을 파괴하지 않는 런타임 아군 안전 스폰입니다.
    /// </summary>
    public bool TrySpawnUnitIfEmpty(string unitId, int gridNumber, out GameObject spawned)
    {
        spawned = null;
        if (!hierarchyReady)
        {
            Awake();
        }

        if (!gridCellsByNumber.TryGetValue(gridNumber, out GridCellRef cell) || cell == null)
        {
            Debug.LogWarning($"[PlayerSpawner] 안전 스폰 셀을 찾지 못했습니다: grid={gridNumber}", this);
            return false;
        }

        BattleCharactor occupyingUnit = cell.GetComponentInChildren<BattleCharactor>(true);
        HostageBattleActor occupyingHostage = cell.GetComponentInChildren<HostageBattleActor>(true);
        if (occupyingUnit != null || occupyingHostage != null)
        {
            return false;
        }

        spawned = SpawnUnit(unitId, gridNumber);
        return spawned != null;
    }

    public GameObject SpawnUnit(string unitId, int gridNumber)
    {
        if (string.IsNullOrWhiteSpace(unitId))
        {
            Debug.LogError($"[PlayerSpawner] unitId가 비어있습니다. ({gameObject.name})");
            return null;
        }

        if (!hierarchyReady || unitParent == null)
        {
            Debug.LogError($"[PlayerSpawner] Grid/Units 계층이 준비되지 않았습니다. ({gameObject.name})");
            return null;
        }

        if (!gridSlots.TryGetValue(gridNumber, out Vector3 worldPos) ||
            !gridRotations.TryGetValue(gridNumber, out Quaternion worldRot))
        {
            Debug.LogError(
                $"[PlayerSpawner] {gridNumber}번 그리드를 찾지 못했습니다. (Grid/Grid_{gridNumber}, {gameObject.name})");
            return null;
        }

        ClearGrid(gridNumber);

        DHCsvTemplateCatalog.Instance.TryGetPlayerTemplate(unitId, out UnitData unit);
        if (unit == null)
        {
            Debug.LogError($"[PlayerSpawner] UnitData를 찾지 못했습니다. unitId={unitId}");
            return null;
        }

        if (!gridCellsByNumber.TryGetValue(gridNumber, out GridCellRef resolvedCell) || resolvedCell == null)
        {
            Debug.LogWarning($"[PlayerSpawner] GridCell이 없어 점유 정보를 연결하지 못했습니다. grid={gridNumber}");
            return null;
        }

        GameObject prefab = FindPrefab(unit);
        if (prefab == null)
        {
            return null;
        }

        // BattleSceneManager.SyncGridOccupancy가 cell 하위에서 유닛을 탐색하므로, 반드시 GridCell 아래에 붙입니다.
        var go = Instantiate(prefab, worldPos, worldRot, resolvedCell.transform);
        go.name = $"Charactor_{unit.Index}_{go.GetInstanceID()}";

        var script = go.GetComponent<CharactorScript>();
        if (script == null)
        {
            script = go.AddComponent<CharactorScript>();
        }

        // 스폰 직후 래퍼 Initialize는 BattleCharactor가 존재해야 수행됩니다.
        var battle = go.GetComponent<BattleCharactor>();
        if (battle == null)
        {
            battle = go.AddComponent<BattleCharactor>();
        }

        script.Initialize(unit);
        battle.AssignToCell(resolvedCell);
        resolvedCell.SetOccupyingUnit(battle);

        spawnedByGrid[gridNumber] = go;
        return go;
    }

    private void ClearGrid(int gridNumber)
    {
        if (spawnedByGrid.TryGetValue(gridNumber, out var existing) && existing != null)
        {
            if (Application.isPlaying)
            {
                Destroy(existing);
            }
            else
            {
                DestroyImmediate(existing);
            }
        }

        spawnedByGrid.Remove(gridNumber);
    }

    private bool SpawnFromSimulationContext(CombatContext combatContext)
    {
        if (combatContext == null || !combatContext.IsSimulation || combatContext.CombatParty == null)
            return false;

        IReadOnlyList<int> combatUnitIndices = ResolveCombatUnitIndices(combatContext.CombatParty);
        if (combatUnitIndices == null || combatUnitIndices.Count == 0)
        {
            Debug.LogError("[PlayerSpawner] Simulation combat has no party unit indices.", this);
            return false;
        }

        if (!hierarchyReady || DHCsvTemplateCatalog.Instance == null || gridSlots.Count == 0)
        {
            Debug.LogError("[PlayerSpawner] Simulation spawn dependencies are not ready.", this);
            return false;
        }

        List<int> sortedGrids = new List<int>(gridSlots.Keys);
        sortedGrids.Sort();
        if (combatUnitIndices.Count > sortedGrids.Count)
        {
            Debug.LogError(
                $"[PlayerSpawner] Simulation party exceeds available grids. units={combatUnitIndices.Count}, grids={sortedGrids.Count}",
                this);
            return false;
        }

        var unitTemplates = new UnitData[combatUnitIndices.Count];
        var runtimeUnits = new UnitPersistentData[combatUnitIndices.Count];
        for (int i = 0; i < combatUnitIndices.Count; i++)
        {
            int runtimeUnitIndex = combatUnitIndices[i];
            if (!combatContext.TryGetSimulationAlly(runtimeUnitIndex, out SimulationAllyRuntimeData runtimeData) ||
                runtimeData == null ||
                runtimeData.UnitData == null)
            {
                Debug.LogError($"[PlayerSpawner] Simulation ally data is missing. runtimeIndex={runtimeUnitIndex}", this);
                return false;
            }

            UnitPersistentData persistentData = runtimeData.UnitData;
            if (persistentData.CurrentHp <= 0f)
            {
                Debug.LogError($"[PlayerSpawner] Simulation ally HP must be positive. runtimeIndex={runtimeUnitIndex}", this);
                return false;
            }

            if (!DHCsvTemplateCatalog.Instance.TryGetPlayerTemplate(persistentData.UnitTemplateKey, out UnitData csvUnitData) ||
                csvUnitData == null ||
                FindPrefab(csvUnitData) == null)
            {
                Debug.LogError(
                    $"[PlayerSpawner] Simulation ally template or prefab is missing. key='{persistentData.UnitTemplateKey}'",
                    this);
                return false;
            }

            int gridNumber = sortedGrids[i];
            if (!gridCellsByNumber.TryGetValue(gridNumber, out GridCellRef cell) || cell == null)
            {
                Debug.LogError($"[PlayerSpawner] Simulation grid cell is missing. grid={gridNumber}", this);
                return false;
            }

            unitTemplates[i] = csvUnitData;
            runtimeUnits[i] = persistentData;
        }

        var spawnedGrids = new List<int>();
        for (int i = 0; i < runtimeUnits.Length; i++)
        {
            int gridNumber = sortedGrids[i];
            if (SpawnPersistentUnit(unitTemplates[i], runtimeUnits[i], gridNumber) == null)
            {
                for (int j = 0; j < spawnedGrids.Count; j++)
                    ClearGrid(spawnedGrids[j]);
                return false;
            }
            spawnedGrids.Add(gridNumber);
        }

        return runtimeUnits.Length > 0;
    }

    // 튜토리얼 전투는 시뮬레이션과 동일하게 CombatContext가 들고 온 런타임 아군 데이터로 스폰한다.
    // (탐사 TutorialCombatLauncher가 defaultUnitTemplateKeys로 빌드해 BeginTutorial에 실어 보낸 값)
    private bool SpawnFromTutorialContext(CombatContext combatContext)
    {
        if (combatContext == null || !combatContext.IsTutorial || combatContext.CombatParty == null)
            return false;

        IReadOnlyList<int> combatUnitIndices = ResolveCombatUnitIndices(combatContext.CombatParty);
        if (combatUnitIndices == null || combatUnitIndices.Count == 0)
        {
            Debug.LogError("[PlayerSpawner] Tutorial combat has no party unit indices.", this);
            return false;
        }

        // 튜토리얼 아군은 일반 카탈로그(DataStorage, 임시)가 아니라 튜토리얼 전용 데이터 매니저(TutorialCatalog,
        // DontDestroyOnLoad로 탐사→전투 유지)에서 템플릿을 해석한다.
        if (!hierarchyReady || TutorialCatalog.Instance == null || gridSlots.Count == 0)
        {
            Debug.LogError("[PlayerSpawner] Tutorial spawn dependencies are not ready.", this);
            return false;
        }

        List<int> sortedGrids = new List<int>(gridSlots.Keys);
        sortedGrids.Sort();
        if (combatUnitIndices.Count > sortedGrids.Count)
        {
            Debug.LogError(
                $"[PlayerSpawner] Tutorial party exceeds available grids. units={combatUnitIndices.Count}, grids={sortedGrids.Count}",
                this);
            return false;
        }

        var unitTemplates = new UnitData[combatUnitIndices.Count];
        var runtimeUnits = new UnitPersistentData[combatUnitIndices.Count];
        for (int i = 0; i < combatUnitIndices.Count; i++)
        {
            int runtimeUnitIndex = combatUnitIndices[i];
            if (!combatContext.TryGetTutorialAlly(runtimeUnitIndex, out SimulationAllyRuntimeData runtimeData) ||
                runtimeData == null ||
                runtimeData.UnitData == null)
            {
                Debug.LogError($"[PlayerSpawner] Tutorial ally data is missing. runtimeIndex={runtimeUnitIndex}", this);
                return false;
            }

            UnitPersistentData persistentData = runtimeData.UnitData;
            if (persistentData.CurrentHp <= 0f)
            {
                Debug.LogError($"[PlayerSpawner] Tutorial ally HP must be positive. runtimeIndex={runtimeUnitIndex}", this);
                return false;
            }

            if (!TutorialCombatUnitFactory.TryCreateUnitData(persistentData.UnitTemplateKey, out UnitData csvUnitData) ||
                csvUnitData == null ||
                FindPrefab(csvUnitData) == null)
            {
                Debug.LogError(
                    $"[PlayerSpawner] Tutorial ally template or prefab is missing. key='{persistentData.UnitTemplateKey}'",
                    this);
                return false;
            }

            int gridNumber = sortedGrids[i];
            if (!gridCellsByNumber.TryGetValue(gridNumber, out GridCellRef cell) || cell == null)
            {
                Debug.LogError($"[PlayerSpawner] Tutorial grid cell is missing. grid={gridNumber}", this);
                return false;
            }

            unitTemplates[i] = csvUnitData;
            runtimeUnits[i] = persistentData;
        }

        var spawnedGrids = new List<int>();
        for (int i = 0; i < runtimeUnits.Length; i++)
        {
            int gridNumber = sortedGrids[i];
            if (SpawnPersistentUnit(unitTemplates[i], runtimeUnits[i], gridNumber) == null)
            {
                for (int j = 0; j < spawnedGrids.Count; j++)
                    ClearGrid(spawnedGrids[j]);
                return false;
            }
            spawnedGrids.Add(gridNumber);
        }

        return runtimeUnits.Length > 0;
    }

    private bool SpawnFromPersistentRepository()
    {
        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null)
        {
            return false;
        }

        CombatContext combatContext = CombatContext.Instance;
        IReadOnlyList<int> combatUnitIndices = ResolveCombatUnitIndices(combatContext != null ? combatContext.CombatParty : null);
        if (combatUnitIndices == null || combatUnitIndices.Count == 0)
        {
            return false;
        }

        if (!hierarchyReady || DHCsvTemplateCatalog.Instance == null || gridSlots.Count == 0)
        {
            return false;
        }

        List<int> sortedGrids = new List<int>(gridSlots.Keys);
        sortedGrids.Sort();

        bool spawnedAny = false;
        bool skippedDeadUnit = false;
        int spawnCount = Mathf.Min(combatUnitIndices.Count, sortedGrids.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            int persistentUnitIndex = combatUnitIndices[i];
            if (persistentUnitIndex <= 0) continue;

            if (!repository.TryGetUnit(persistentUnitIndex, out UnitPersistentData persistentData) || persistentData == null)
            {
                continue;
            }

            if (persistentData.CurrentHp <= 0f)
            {
                skippedDeadUnit = true;
                continue;
            }

            DHCsvTemplateCatalog.Instance.TryGetPlayerTemplate(persistentData.UnitTemplateKey, out UnitData csvUnitData);
            if (csvUnitData == null)
            {
                // unitTemplateKey 미매핑 시 인덱스 문자열 폴백
                DHCsvTemplateCatalog.Instance.TryGetPlayerTemplate(persistentUnitIndex.ToString(), out csvUnitData);
            }

            if (csvUnitData == null) continue;

            int gridNumber = sortedGrids[i];
            SpawnPersistentUnit(csvUnitData, persistentData, gridNumber);
            spawnedAny = true;
        }

        return spawnedAny || skippedDeadUnit;
    }

    private GameObject SpawnPersistentUnit(UnitData unitData, UnitPersistentData persistentData, int gridNumber)
    {
        if (unitData == null || persistentData == null) return null;
        if (!gridCellsByNumber.TryGetValue(gridNumber, out GridCellRef persistentCell) || persistentCell == null) return null;

        ClearGrid(gridNumber);

        GameObject prefab = FindPrefab(unitData);
        if (prefab == null) return null;

        var go = Instantiate(prefab, persistentCell.transform.position, persistentCell.transform.rotation, persistentCell.transform);
        go.name = $"Charactor_{unitData.Index}_{go.GetInstanceID()}";

        var script = go.GetComponent<CharactorScript>();
        if (script == null)
        {
            script = go.AddComponent<CharactorScript>();
        }

        var battle = go.GetComponent<BattleCharactor>();
        if (battle == null)
        {
            battle = go.AddComponent<BattleCharactor>();
        }

        // 래퍼가 persistentData 참조를 BattleCharactor.SourceData에 그대로 바인딩합니다.
        script.Initialize(persistentData, unitData);

        battle.AssignToCell(persistentCell);
        persistentCell.SetOccupyingUnit(battle);

        spawnedByGrid[gridNumber] = go;
        return go;
    }

    private static IReadOnlyList<int> ResolveCombatUnitIndices(object combatParty)
    {
        if (combatParty == null) return null;

        PropertyInfo unitIndicesProperty =
            combatParty.GetType().GetProperty("UnitIndices", BindingFlags.Public | BindingFlags.Instance);
        if (unitIndicesProperty != null &&
            unitIndicesProperty.GetValue(combatParty) is IReadOnlyList<int> unitIndices &&
            unitIndices.Count > 0)
        {
            return unitIndices;
        }

        PropertyInfo unitsProperty =
            combatParty.GetType().GetProperty("Units", BindingFlags.Public | BindingFlags.Instance);
        if (unitsProperty != null &&
            unitsProperty.GetValue(combatParty) is IReadOnlyList<int> units &&
            units.Count > 0)
        {
            return units;
        }

        return null;
    }

    private static bool TryResolveGridNumber(Transform slotTransform, out int gridNumber)
    {
        gridNumber = 0;
        if (slotTransform == null)
        {
            return false;
        }

        string name = slotTransform.name ?? string.Empty;
        if (!name.StartsWith("Grid_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = name.Substring("Grid_".Length);
        return TryGetGridNumberFromSuffix(suffix, out gridNumber);
    }

    private static bool TryGetGridNumberFromSuffix(string suffix, out int gridNumber)
    {
        gridNumber = 0;
        if (int.TryParse(suffix, out gridNumber))
        {
            return true;
        }

        string[] xy = suffix.Split('_');
        if (xy.Length == 2 && int.TryParse(xy[0], out int x) && int.TryParse(xy[1], out int y))
        {
            gridNumber = (x * 100) + y;
            return true;
        }

        return false;
    }
}