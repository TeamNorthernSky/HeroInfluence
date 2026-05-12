using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyGroupPersistentRepository : MonoBehaviour
{
    public static EnemyGroupPersistentRepository Instance { get; private set; }
    private const string EnemyIdPrefix = "enemy_";

    [Header("Persistent Enemy Groups")]
    [SerializeField] private int nextEnemySequence = 1;
    [SerializeField] private List<EnemyPersistentData> enemies = new List<EnemyPersistentData>();

    private readonly Dictionary<string, EnemyPersistentData> enemyLookup = new Dictionary<string, EnemyPersistentData>();

    public IReadOnlyList<EnemyPersistentData> Enemies => enemies;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    public string CreateEnemy(IReadOnlyList<int> unitIndices)
    {
        int sequence = Mathf.Max(1, nextEnemySequence);
        nextEnemySequence = sequence + 1;
        string enemyId = FormatEnemyId(sequence);

        var newData = new EnemyPersistentData(enemyId, unitIndices);
        enemies.Add(newData);
        enemyLookup[enemyId] = newData;
        return enemyId;
    }

    public void RegisterOrUpdateEnemy(string enemyId, IReadOnlyList<int> unitIndices)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            return;

        if (enemyLookup.TryGetValue(enemyId, out EnemyPersistentData existingData))
        {
            existingData.SetUnitIndices(unitIndices);
            return;
        }

        EnemyPersistentData newData = new EnemyPersistentData(enemyId, unitIndices);
        enemies.Add(newData);
        enemyLookup[enemyId] = newData;
        UpdateNextEnemySequence(enemyId);
    }

    public bool ContainsEnemy(string enemyId)
    {
        return !string.IsNullOrWhiteSpace(enemyId) && enemyLookup.ContainsKey(enemyId);
    }

    public bool TryGetEnemy(string enemyId, out EnemyPersistentData data)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
        {
            data = null;
            return false;
        }

        return enemyLookup.TryGetValue(enemyId, out data);
    }

    public bool RemoveEnemy(string enemyId)
    {
        if (string.IsNullOrWhiteSpace(enemyId) || !enemyLookup.TryGetValue(enemyId, out EnemyPersistentData data))
            return false;

        enemyLookup.Remove(enemyId);
        enemies.Remove(data);
        return true;
    }

    public void ClearAllEnemies()
    {
        enemies.Clear();
        enemyLookup.Clear();
        nextEnemySequence = 1;
    }

    private void RebuildLookup()
    {
        enemyLookup.Clear();
        int highestEnemySequence = 0;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyPersistentData data = enemies[i];
            if (data == null || string.IsNullOrWhiteSpace(data.EnemyId))
                continue;

            if (enemyLookup.ContainsKey(data.EnemyId))
            {
                Debug.LogWarning($"EnemyGroupPersistentRepository has duplicate enemyId '{data.EnemyId}'.", this);
                continue;
            }

            enemyLookup.Add(data.EnemyId, data);
            if (TryParseEnemySequence(data.EnemyId, out int sequence) && sequence > highestEnemySequence)
                highestEnemySequence = sequence;
        }

        if (nextEnemySequence <= highestEnemySequence)
            nextEnemySequence = highestEnemySequence + 1;
    }

    private static string FormatEnemyId(int sequence)
    {
        return $"{EnemyIdPrefix}{Mathf.Max(1, sequence):000}";
    }

    private void UpdateNextEnemySequence(string enemyId)
    {
        if (TryParseEnemySequence(enemyId, out int sequence) && nextEnemySequence <= sequence)
            nextEnemySequence = sequence + 1;
    }

    private static bool TryParseEnemySequence(string enemyId, out int sequence)
    {
        sequence = 0;
        if (string.IsNullOrWhiteSpace(enemyId) ||
            !enemyId.StartsWith(EnemyIdPrefix, System.StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(enemyId.Substring(EnemyIdPrefix.Length), out sequence) && sequence > 0;
    }
}
