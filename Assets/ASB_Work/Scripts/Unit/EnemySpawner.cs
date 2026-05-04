using System;
using System.Collections.Generic;
using UnityEngine;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

/// <summary>
/// EnemyPlace 최상위에 부착. Grid/Grid_n 월드 위치 참조, 소환 유닛은 Units 자식.
/// 프리팹은 Resources/prefab/Unit_{UnitType} 에서 로드.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Serializable]
    public struct SpawnRequest
    {
        public string unitId;
        public int gridNumber;
    }

    [Header("Dependencies")]
    [SerializeField] private EnemyManager enemyManager;

    [Header("Inspector / Battle debug spawn")]
    public List<SpawnRequest> debugSpawnRequests = new List<SpawnRequest>();

    private Transform unitParent;
    private readonly Dictionary<int, Vector3> gridSlots = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, Quaternion> gridRotations = new Dictionary<int, Quaternion>();
    private readonly Dictionary<int, GameObject> spawnedByGrid = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GridCellRef> gridCellsByNumber = new Dictionary<int, GridCellRef>();
    private bool hierarchyReady;

    private void Awake()
    {
        gridSlots.Clear();
        gridRotations.Clear();
        unitParent = null;
        hierarchyReady = false;

        unitParent = transform.Find("UnitContainer");
        if (unitParent == null)
        {
            var created = new GameObject("UnitContainer");
            created.transform.SetParent(transform, false);
            unitParent = created.transform;
        }
        Transform gridRoot = transform.Find("Grid");

        if (unitParent == null || gridRoot == null)
        {
            Debug.LogError($"[{gameObject.name}] 'Units' 또는 'Grid' 자식 오브젝트를 찾을 수 없습니다.");
            return;
        }

        foreach (Transform child in gridRoot)
        {
            if (child == null) continue;

            string n = child.name;
            if (!n.StartsWith("Grid_", StringComparison.OrdinalIgnoreCase)) continue;

            string suffix = n.Substring("Grid_".Length);
            if (!TryGetGridNumberFromSuffix(suffix, out int gridNumber)) continue;

            if (gridSlots.ContainsKey(gridNumber)) continue;

            gridSlots[gridNumber] = child.position;
            gridRotations[gridNumber] = child.rotation;
            gridCellsByNumber[gridNumber] = child.GetComponent<GridCellRef>();
        }

        hierarchyReady = true;
    }

    private GameObject FindPrefab(EnemyData data)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.UnitType))
        {
            Debug.LogError("[EnemySpawner] UnitType이 비어 있거나 EnemyData가 없습니다.");
            return null;
        }

        string path = $"prefab/Unit_{data.UnitType}";
        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[EnemySpawner] 프리팹을 찾을 수 없습니다: {path}");
            return null;
        }

        return prefab;
    }

    public GameObject SpawnUnit(string enemyId, int gridNumber)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
        {
            Debug.LogError($"[EnemySpawner] enemyId가 비어 있습니다. ({gameObject.name})");
            return null;
        }

        if (!hierarchyReady || unitParent == null)
        {
            Debug.LogError($"[EnemySpawner] Grid/Units 계층이 준비되지 않았습니다. ({gameObject.name})");
            return null;
        }

        if (enemyManager == null)
        {
            Debug.LogError("[EnemySpawner] enemyManager가 할당되지 않았습니다.");
            return null;
        }

        if (!gridSlots.TryGetValue(gridNumber, out Vector3 worldPos) ||
            !gridRotations.TryGetValue(gridNumber, out Quaternion worldRot))
        {
            Debug.LogError(
                $"[EnemySpawner] {gridNumber}번 그리드를 찾지 못했습니다. (Grid/Grid_{gridNumber}, {gameObject.name})");
            return null;
        }

        ClearGrid(gridNumber);

        EnemyData data = enemyManager.GetEnemyData(enemyId);
        if (data == null)
        {
            Debug.LogError(
                $"[EnemySpawner] EnemyManager에서 UnitData(EnemyData)를 찾지 못했습니다. enemyId='{enemyId}' — 스폰을 중단합니다.");
            return null;
        }

        GameObject prefab = FindPrefab(data);
        if (prefab == null)
        {
            return null;
        }

        var go = Instantiate(prefab, worldPos, worldRot, unitParent);
        go.name = $"Enemy_{data.Index}";

        // 적 인스턴스에서는 IUnitIdentifier를 EnemyScript만 담당하도록 CharactorScript 제거(클릭 식별 모호 방지).
        // 같은 프레임에 RebuildRuntimeLookup이 돌 수 있어 DestroyImmediate로 즉시 제거한다.
        foreach (var legacy in go.GetComponentsInChildren<CharactorScript>(true))
        {
            DestroyImmediate(legacy);
        }

        var enemyScript = go.GetComponent<EnemyScript>();
        if (enemyScript == null)
        {
            enemyScript = go.AddComponent<EnemyScript>();
        }

        // 플레이어 스폰(PlayerSpawner → CharactorScript.Initialize)과 동일하게 루트에 UnitData 주입.
        enemyScript.Initialize(data);

        var battle = go.GetComponent<BattleCharactor>();
        if (battle == null)
        {
            battle = go.AddComponent<BattleCharactor>();
        }

        if (gridCellsByNumber.TryGetValue(gridNumber, out var cell) && cell != null)
        {
            battle.AssignToCell(cell);
            cell.SetOccupyingUnit(battle);
        }
        else
        {
            Debug.LogWarning($"[EnemySpawner] GridCell이 없어 점유 정보를 연결하지 못했습니다. grid={gridNumber}");
        }

        // 디버그 확인용, 이후 제거 — 스폰 직후 UnitID가 battleById 키와 일치하는지 확인
        Debug.Log(
            $"[EnemySpawner] 스폰 직후 EnemyScript.UnitID='{enemyScript.UnitID}' (enemyId={enemyId}, grid={gridNumber})");

        spawnedByGrid[gridNumber] = go;
        Debug.Log(
            $"[EnemySpawner] 적 스폰 완료: id={enemyId}, grid={gridNumber}, UnitType={data.UnitType}, place={gameObject.name}");
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

    [ContextMenu("Debug Spawn Enemies")]
    private void DebugSpawn()
    {
        if (debugSpawnRequests == null) return;

        for (int i = 0; i < debugSpawnRequests.Count; i++)
        {
            var req = debugSpawnRequests[i];
            if (string.IsNullOrWhiteSpace(req.unitId)) continue;

            SpawnUnit(req.unitId, req.gridNumber);
        }
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
