using System.Collections.Generic;
using UnityEngine;

/// <summary>적군 전용 마스터 데이터 캐시. CharactorManager와 분리하여 이후 적 AI를 독립 적용하기 위함.</summary>
public class EnemyManager : MonoBehaviour
{
    private readonly Dictionary<string, EnemyData> enemyTable = new Dictionary<string, EnemyData>();

    /// <summary>DataManager가 CSV에서 적 행만 모아 전달하면 캐싱합니다.</summary>
    public void SetEnemyData(List<EnemyData> enemies)
    {
        enemyTable.Clear();
        if (enemies == null)
        {
            Debug.LogWarning("[EnemyManager] SetEnemyData: 리스트가 null입니다.");
            return;
        }

        int added = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null || string.IsNullOrWhiteSpace(e.Index))
            {
                continue;
            }

            enemyTable[e.Index] = e;
            added++;
        }

        Debug.Log($"[EnemyManager] 적 데이터 {added}건을 캐시했습니다. (총 행 {enemies.Count})");
    }

    public EnemyData GetEnemyData(string enemyId)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
        {
            return null;
        }

        return enemyTable.TryGetValue(enemyId, out var data) ? data : null;
    }

    /// <summary>전투 레벨 등. 추후 적 전용 성장 테이블과 연동 가능.</summary>
    public int GetEnemyLevel(string enemyId)
    {
        return 1;
    }
}
