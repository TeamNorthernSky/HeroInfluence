using System;
using System.Collections.Generic;
using UnityEngine;

// [JC 신설 260513] 적 파티 영속 풀. GameManager 자식.
// 책임:
//   - 카탈로그 보유 (인스펙터 등록 프리팹 목록)
//   - 현재 맵에 배치된 인스턴스 영속 정보(instanceId / prefabKey / lastKnownGrid)
//   - 동시 인스턴스 수 가드(maxConcurrentInstances)
// 비책임: Instantiate / 위치 결정. 실행은 EnemySpawnController가 담당.
[DisallowMultipleComponent]
public class EnemyPartyPool : MonoBehaviour
{
    public static EnemyPartyPool Instance { get; private set; }
    private const string InstanceIdPrefix = "enemyinstance_";

    [Header("Catalog")]
    [SerializeField] private List<GameObject> enemyPartyPrefabs = new List<GameObject>();

    [Header("Guard")]
    [SerializeField, Min(1)] private int maxConcurrentInstances = 3;

    [Header("Live State")]
    [SerializeField] private int nextInstanceSequence = 1;
    [SerializeField] private List<EnemyPartyInstance> liveInstances = new List<EnemyPartyInstance>();
    // [JC 추가 260513] 마지막 spawn이 발화된 day. 같은 day 중복 spawn 차단용. 0=미발화.
    [SerializeField] private int lastSpawnTurn = 0;

    public IReadOnlyList<GameObject> Catalog => enemyPartyPrefabs;
    public IReadOnlyList<EnemyPartyInstance> LiveInstances => liveInstances;
    public int MaxConcurrentInstances => maxConcurrentInstances;
    public int LiveCount => liveInstances.Count;
    public int LastSpawnTurn => lastSpawnTurn;

    public void MarkSpawnedAt(int day)
    {
        lastSpawnTurn = Mathf.Max(0, day);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool CanRegister()
    {
        return liveInstances.Count < maxConcurrentInstances;
    }

    public GameObject PickRandomPrefab()
    {
        if (enemyPartyPrefabs == null || enemyPartyPrefabs.Count == 0)
            return null;

        int index = UnityEngine.Random.Range(0, enemyPartyPrefabs.Count);
        return enemyPartyPrefabs[index];
    }

    public GameObject FindPrefabByKey(string prefabKey)
    {
        if (string.IsNullOrWhiteSpace(prefabKey) || enemyPartyPrefabs == null)
            return null;

        for (int i = 0; i < enemyPartyPrefabs.Count; i++)
        {
            GameObject prefab = enemyPartyPrefabs[i];
            if (prefab != null && prefab.name == prefabKey)
                return prefab;
        }
        return null;
    }

    public string IssueInstanceId()
    {
        int sequence = Mathf.Max(1, nextInstanceSequence);
        nextInstanceSequence = sequence + 1;
        return $"{InstanceIdPrefix}{sequence:000}";
    }

    public bool TryRegisterInstance(string instanceId, string prefabKey, Vector2Int grid)
    {
        if (!CanRegister())
        {
            Debug.LogWarning($"[EnemyPartyPool] Concurrent guard rejected register. live={liveInstances.Count}/{maxConcurrentInstances}.", this);
            return false;
        }
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            Debug.LogWarning("[EnemyPartyPool] Cannot register instance with empty instanceId.", this);
            return false;
        }
        if (TryGetInstance(instanceId, out _))
        {
            Debug.LogWarning($"[EnemyPartyPool] Duplicate instanceId '{instanceId}'.", this);
            return false;
        }

        liveInstances.Add(new EnemyPartyInstance(instanceId, prefabKey, grid));
        return true;
    }

    public bool UpdateInstanceGrid(string instanceId, Vector2Int grid)
    {
        for (int i = 0; i < liveInstances.Count; i++)
        {
            EnemyPartyInstance instance = liveInstances[i];
            if (instance != null && instance.InstanceId == instanceId)
            {
                instance.SetLastKnownGrid(grid);
                return true;
            }
        }
        return false;
    }

    public bool UnregisterInstance(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        for (int i = 0; i < liveInstances.Count; i++)
        {
            EnemyPartyInstance instance = liveInstances[i];
            if (instance != null && instance.InstanceId == instanceId)
            {
                liveInstances.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    // [JC 추가 260513] directive 패턴용. 다음 restore 시 다른 prefab으로 등장.
    public bool ReplaceInstancePrefab(string instanceId, string prefabKey)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(prefabKey))
            return false;

        for (int i = 0; i < liveInstances.Count; i++)
        {
            EnemyPartyInstance instance = liveInstances[i];
            if (instance != null && instance.InstanceId == instanceId)
            {
                instance.SetPrefabKey(prefabKey);
                return true;
            }
        }
        return false;
    }

    public bool TryGetInstance(string instanceId, out EnemyPartyInstance instance)
    {
        instance = null;
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        for (int i = 0; i < liveInstances.Count; i++)
        {
            EnemyPartyInstance candidate = liveInstances[i];
            if (candidate != null && candidate.InstanceId == instanceId)
            {
                instance = candidate;
                return true;
            }
        }
        return false;
    }

    [ContextMenu("Clear All Live Instances")]
    public void ClearAllLiveInstances()
    {
        liveInstances.Clear();
        nextInstanceSequence = 1;
        lastSpawnTurn = 0;
    }
}

[Serializable]
public class EnemyPartyInstance
{
    [SerializeField] private string instanceId;
    [SerializeField] private string prefabKey;
    [SerializeField] private Vector2Int lastKnownGrid;

    public string InstanceId => instanceId;
    public string PrefabKey => prefabKey;
    public Vector2Int LastKnownGrid => lastKnownGrid;

    public EnemyPartyInstance(string instanceId, string prefabKey, Vector2Int lastKnownGrid)
    {
        this.instanceId = instanceId ?? string.Empty;
        this.prefabKey = prefabKey ?? string.Empty;
        this.lastKnownGrid = lastKnownGrid;
    }

    public void SetLastKnownGrid(Vector2Int grid)
    {
        lastKnownGrid = grid;
    }

    public void SetPrefabKey(string nextPrefabKey)
    {
        prefabKey = nextPrefabKey ?? string.Empty;
    }
}
