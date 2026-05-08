//using System;
//using System.Collections.Generic;
//using System.Reflection;
//using UnityEngine;

//namespace ASB.Work.Spawning
//{
///// <summary>
///// 실행 주체(스포너) 역할.
///// - Grid_1 ~ Grid_6 같은 자식 Transform을 캐싱
///// - 외부로부터 unitId/gridNumber를 받아 CharactorScript 인스턴스를 생성/주입
///// </summary>
//public class PlayerSpawner : MonoBehaviour
//{
//    [Serializable]
//    public class SpawnRequest
//    {
//        public string unitIndex;
//        public int gridNumber;
//    }

//    [Header("Dependencies")]
//    [SerializeField] private CharactorManager charactorManager;

//    [Header("Prefab")]
//    [SerializeField] private GameObject charactorPrefab;

//    [Header("Inspector Test")]
//    [SerializeField] private List<SpawnRequest> debugSpawnRequests = new List<SpawnRequest>();
//    [SerializeField] private bool spawnOnStart = true;

//    // Grid_1.. 캐싱
//    private readonly Dictionary<int, Transform> gridTable = new Dictionary<int, Transform>();
//    private readonly Dictionary<int, GameObject> spawnedByGrid = new Dictionary<int, GameObject>();

//    private void Awake()
//    {
//        CacheGrids();
//    }

//    private void Start()
//    {
//        if (!spawnOnStart)
//        {
//            return;
//        }

//        bool spawnedFromRepository = SpawnFromPersistentRepository();
//        if (!spawnedFromRepository)
//        {
//            // 폴백: 기존 경로(디버그/CSV 기반 unitId 스폰) 사용
//            DebugSpawn();
//        }
//    }

//    private void CacheGrids()
//    {
//        gridTable.Clear();

//        // "부모 오브젝트의 자식들"에서 Grid_* 를 찾습니다.
//        foreach (Transform child in transform)
//        {
//            if (child == null) continue;

//            // 예: Grid_1
//            string name = child.name;
//            if (!name.StartsWith("Grid_", StringComparison.OrdinalIgnoreCase)) continue;

//            string suffix = name.Substring("Grid_".Length);
//            if (int.TryParse(suffix, out int gridNumber))
//            {
//                if (!gridTable.ContainsKey(gridNumber))
//                {
//                    gridTable.Add(gridNumber, child);
//                }
//            }
//        }
//    }

//    public GameObject SpawnUnit(string unitId, int gridNumber)
//    {
//        if (string.IsNullOrWhiteSpace(unitId))
//        {
//            Debug.LogError("PlayerSpawner: unitId가 비어있습니다.");
//            return null;
//        }

//        if (charactorManager == null)
//        {
//            Debug.LogError("PlayerSpawner: charactorManager가 할당되지 않았습니다.");
//            return null;
//        }

//        if (charactorPrefab == null)
//        {
//            Debug.LogError("PlayerSpawner: charactorPrefab이 할당되지 않았습니다.");
//            return null;
//        }

//        if (!gridTable.TryGetValue(gridNumber, out var gridTransform) || gridTransform == null)
//        {
//            Debug.LogError($"PlayerSpawner: {gridNumber}번 그리드를 찾지 못했습니다. (Grid_{gridNumber} 필요)");
//            return null;
//        }

//        // 기존 객체 제거
//        ClearGrid(gridNumber);

//        // 데이터 조회
//        UnitData unit = charactorManager.GetCharactorData(unitId);
//        if (unit == null)
//        {
//            Debug.LogError($"PlayerSpawner: UnitData를 찾지 못했습니다. unitId={unitId}");
//            return null;
//        }

//        // 생성/배치
//        var go = Instantiate(charactorPrefab, gridTransform.position, gridTransform.rotation);
//        go.name = $"Charactor_{unit.Index}";
//        go.transform.SetParent(gridTransform, worldPositionStays: true);

//        // 데이터 주입
//        var script = go.GetComponent<CharactorScript>();
//        if (script == null)
//        {
//            script = go.AddComponent<CharactorScript>();
//        }
//        script.Initialize(unit);

//        spawnedByGrid[gridNumber] = go;
//        return go;
//    }

//    private bool SpawnFromPersistentRepository()
//    {
//        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
//        if (repository == null)
//        {
//            return false;
//        }

//        IReadOnlyList<int> combatUnitIndices = ResolveCombatUnitIndices(repository.CombatParty);
//        if (combatUnitIndices == null || combatUnitIndices.Count == 0)
//        {
//            return false;
//        }

//        if (charactorManager == null || charactorPrefab == null || gridTable.Count == 0)
//        {
//            return false;
//        }

//        List<int> sortedGrids = new List<int>(gridTable.Keys);
//        sortedGrids.Sort();

//        bool spawnedAny = false;
//        int spawnCount = Mathf.Min(combatUnitIndices.Count, sortedGrids.Count);
//        for (int i = 0; i < spawnCount; i++)
//        {
//            int persistentUnitIndex = combatUnitIndices[i];
//            if (persistentUnitIndex <= 0)
//            {
//                continue;
//            }

//            int gridNumber = sortedGrids[i];
//            if (!repository.TryGetUnit(persistentUnitIndex, out UnitPersistentData persistentData) || persistentData == null)
//            {
//                continue;
//            }

//            UnitData csvUnitData = charactorManager.GetCharactorData(persistentUnitIndex.ToString());
//            if (csvUnitData != null)
//            {
//                SpawnPersistentUnit(csvUnitData, persistentData, gridNumber);
//                spawnedAny = true;
//            }
//        }

//        return spawnedAny;
//    }

//    private GameObject SpawnPersistentUnit(UnitData unitData, UnitPersistentData persistentData, int gridNumber)
//    {
//        if (unitData == null || persistentData == null)
//        {
//            return null;
//        }

//        if (!gridTable.TryGetValue(gridNumber, out Transform gridTransform) || gridTransform == null)
//        {
//            return null;
//        }

//        ClearGrid(gridNumber);
//        var go = Instantiate(charactorPrefab, gridTransform.position, gridTransform.rotation);
//        go.name = $"Charactor_{unitData.Index}";
//        go.transform.SetParent(gridTransform, worldPositionStays: true);

//        CharactorScript script = go.GetComponent<CharactorScript>();
//        if (script == null)
//        {
//            script = go.AddComponent<CharactorScript>();
//        }

//        script.Initialize(persistentData, unitData);
//        spawnedByGrid[gridNumber] = go;
//        return go;
//    }

//    private static IReadOnlyList<int> ResolveCombatUnitIndices(object combatParty)
//    {
//        if (combatParty == null)
//        {
//            return null;
//        }

//        PropertyInfo unitIndicesProperty = combatParty.GetType().GetProperty("UnitIndices", BindingFlags.Public | BindingFlags.Instance);
//        if (unitIndicesProperty != null && unitIndicesProperty.GetValue(combatParty) is IReadOnlyList<int> unitIndices && unitIndices.Count > 0)
//        {
//            return unitIndices;
//        }

//        PropertyInfo unitsProperty = combatParty.GetType().GetProperty("Units", BindingFlags.Public | BindingFlags.Instance);
//        if (unitsProperty != null && unitsProperty.GetValue(combatParty) is IReadOnlyList<int> units && units.Count > 0)
//        {
//            return units;
//        }

//        return null;
//    }

//    private void ClearGrid(int gridNumber)
//    {
//        if (spawnedByGrid.TryGetValue(gridNumber, out var existing) && existing != null)
//        {
//            if (Application.isPlaying)
//            {
//                Destroy(existing);
//            }
//            else
//            {
//                DestroyImmediate(existing);
//            }
//        }

//        spawnedByGrid.Remove(gridNumber);
//    }

//    [ContextMenu("Debug Spawn")]
//    private void DebugSpawn()
//    {
//        // 인스펙터 리스트 대로 즉시 소환
//        if (debugSpawnRequests == null) return;

//        for (int i = 0; i < debugSpawnRequests.Count; i++)
//        {
//            var req = debugSpawnRequests[i];
//            if (req == null) continue;
//            if (string.IsNullOrWhiteSpace(req.unitIndex)) continue;

//            SpawnUnit(req.unitIndex, req.gridNumber);
//        }
//    }
//}
//}
