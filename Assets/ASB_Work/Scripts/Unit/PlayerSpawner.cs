using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerPlace 최상위에 부착. Grid/Grid_n에서 월드 위치만 참조하고, 유닛은 Units 자식으로 둡니다.
/// 프리팹은 Resources/prefab/Unit_{UnitType} 에서 로드.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    //[Serializable]
    //public struct SpawnRequest
    //{
    //    public string unitId;
    //    public int gridNumber;
    //    public bool isPlayer;
    //}

    //[Header("Dependencies")]
    //[SerializeField] private CharactorManager charactorManager;

    //[Header("Inspector Test / Battle debug spawn")]
    //public List<SpawnRequest> debugSpawnRequests = new List<SpawnRequest>();

    //private Transform unitParent;
    //private readonly Dictionary<int, Vector3> gridSlots = new Dictionary<int, Vector3>();
    //private readonly Dictionary<int, Quaternion> gridRotations = new Dictionary<int, Quaternion>();
    //private readonly Dictionary<int, GameObject> spawnedByGrid = new Dictionary<int, GameObject>();
    //private readonly Dictionary<int, GridCell> gridCellsByNumber = new Dictionary<int, GridCell>();
    //private bool hierarchyReady;

    //private void Awake()
    //{
    //    gridSlots.Clear();
    //    gridRotations.Clear();
    //    unitParent = null;
    //    hierarchyReady = false;

    //    unitParent = transform.Find("UnitContainer");
    //    if (unitParent == null)
    //    {
    //        var created = new GameObject("UnitContainer");
    //        created.transform.SetParent(transform, false);
    //        unitParent = created.transform;
    //    }
    //    Transform gridRoot = transform.Find("Grid");

    //    if (unitParent == null || gridRoot == null)
    //    {
    //        Debug.LogError($"[{gameObject.name}] 'Units' 또는 'Grid' 자식 오브젝트를 찾을 수 없습니다.");
    //        return;
    //    }

    //    foreach (Transform child in gridRoot)
    //    {
    //        if (child == null) continue;

    //        string n = child.name;
    //        if (!n.StartsWith("Grid_", StringComparison.OrdinalIgnoreCase)) continue;

    //        string suffix = n.Substring("Grid_".Length);
    //        if (!TryGetGridNumberFromSuffix(suffix, out int gridNumber)) continue;

    //        if (gridSlots.ContainsKey(gridNumber)) continue;

    //        gridSlots[gridNumber] = child.position;
    //        gridRotations[gridNumber] = child.rotation;
    //        gridCellsByNumber[gridNumber] = child.GetComponent<GridCell>();
    //    }

    //    hierarchyReady = true;
    //}

    //private GameObject FindPrefab(UnitData unit)
    //{
    //    if (unit == null || string.IsNullOrWhiteSpace(unit.UnitType))
    //    {
    //        Debug.LogError("[PlayerSpawner] UnitType이 비어 있거나 UnitData가 없습니다.");
    //        return null;
    //    }

    //    string path = $"prefab/Unit_{unit.UnitType}";
    //    var prefab = Resources.Load<GameObject>(path);
    //    if (prefab == null)
    //    {
    //        Debug.LogError($"[PlayerSpawner] 프리팹을 찾을 수 없습니다: {path}");
    //        return null;
    //    }

    //    return prefab;
    //}

    //public GameObject SpawnUnit(string unitId, int gridNumber)
    //{
    //    if (string.IsNullOrWhiteSpace(unitId))
    //    {
    //        Debug.LogError($"[PlayerSpawner] unitId가 비어있습니다. ({gameObject.name})");
    //        return null;
    //    }

    //    if (!hierarchyReady || unitParent == null)
    //    {
    //        Debug.LogError($"[PlayerSpawner] Grid/Units 계층이 준비되지 않았습니다. ({gameObject.name})");
    //        return null;
    //    }

    //    if (charactorManager == null)
    //    {
    //        Debug.LogError("[PlayerSpawner] charactorManager가 할당되지 않았습니다.");
    //        return null;
    //    }

    //    if (!gridSlots.TryGetValue(gridNumber, out Vector3 worldPos) ||
    //        !gridRotations.TryGetValue(gridNumber, out Quaternion worldRot))
    //    {
    //        Debug.LogError(
    //            $"[PlayerSpawner] {gridNumber}번 그리드를 찾지 못했습니다. (Grid/Grid_{gridNumber}, {gameObject.name})");
    //        return null;
    //    }

    //    ClearGrid(gridNumber);

    //    UnitData unit = charactorManager.GetCharactorData(unitId);
    //    if (unit == null)
    //    {
    //        Debug.LogError($"[PlayerSpawner] UnitData를 찾지 못했습니다. unitId={unitId}");
    //        return null;
    //    }

    //    GameObject prefab = FindPrefab(unit);
    //    if (prefab == null)
    //    {
    //        return null;
    //    }

    //    var go = Instantiate(prefab, worldPos, worldRot, unitParent);
    //    go.name = $"Charactor_{unit.Index}";

    //    var script = go.GetComponent<CharactorScript>();
    //    if (script == null)
    //    {
    //        script = go.AddComponent<CharactorScript>();
    //    }

    //    script.Initialize(unit);

    //    var battle = go.GetComponent<BattleCharactor>();
    //    if (battle == null)
    //    {
    //        battle = go.AddComponent<BattleCharactor>();
    //    }

    //    if (gridCellsByNumber.TryGetValue(gridNumber, out var cell) && cell != null)
    //    {
    //        //지금은 현재 코드와 달라 연결이 없지만, 추후 사용시 주석 해제
    //        //battle.AssignToCell(cell);
    //        //cell.SetOccupyingUnit(battle);
    //    }
    //    else
    //    {
    //        Debug.LogWarning($"[PlayerSpawner] GridCell이 없어 점유 정보를 연결하지 못했습니다. grid={gridNumber}");
    //    }

    //    spawnedByGrid[gridNumber] = go;
    //    return go;
    //}

    //private void ClearGrid(int gridNumber)
    //{
    //    if (spawnedByGrid.TryGetValue(gridNumber, out var existing) && existing != null)
    //    {
    //        if (Application.isPlaying)
    //        {
    //            Destroy(existing);
    //        }
    //        else
    //        {
    //            DestroyImmediate(existing);
    //        }
    //    }

    //    spawnedByGrid.Remove(gridNumber);
    //}

    //[ContextMenu("Debug Spawn")]
    //private void DebugSpawn()
    //{
    //    if (debugSpawnRequests == null) return;

    //    for (int i = 0; i < debugSpawnRequests.Count; i++)
    //    {
    //        var req = debugSpawnRequests[i];
    //        if (string.IsNullOrWhiteSpace(req.unitId)) continue;

    //        SpawnUnit(req.unitId, req.gridNumber);
    //    }
    //}

    //private static bool TryGetGridNumberFromSuffix(string suffix, out int gridNumber)
    //{
    //    gridNumber = 0;
    //    if (int.TryParse(suffix, out gridNumber))
    //    {
    //        return true;
    //    }

    //    string[] xy = suffix.Split('_');
    //    if (xy.Length == 2 && int.TryParse(xy[0], out int x) && int.TryParse(xy[1], out int y))
    //    {
    //        gridNumber = (x * 100) + y;
    //        return true;
    //    }

    //    return false;
    //}
}
