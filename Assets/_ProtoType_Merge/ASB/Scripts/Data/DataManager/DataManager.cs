/*
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CSVDataLoad.LoadPlayerUnits / LoadEnemyUnits → UnitData.baseStats(원본) 캐시.
/// 씬의 CharactorScript/EnemyScript.Initialize(UnitData)에서 BattleCharactor.SetBaseStats → StatCalculator 경로로 전달됩니다.
/// 프로토타입 전투(BattleSceneManager만 사용)는 이 목록 없이 BattleCharactor 인스펙터로 base를 구성합니다.
/// </summary>
public class DataManager : MonoBehaviour
{
    [SerializeField] private CSVDataLoad csvLoader;
    private readonly List<UnitData> playerUnits = new List<UnitData>();
    private readonly List<EnemyData> enemyUnits = new List<EnemyData>();

    public IReadOnlyList<UnitData> PlayerUnits => playerUnits;
    public IReadOnlyList<EnemyData> EnemyUnits => enemyUnits;

    private void Awake()
    {
        if (csvLoader == null)
        {
            Debug.LogError("[DataManager] csvLoader가 할당되지 않았습니다.");
            return;
        }

        playerUnits.Clear();
        playerUnits.AddRange(csvLoader.LoadPlayerUnits());
        Debug.Log($"[DataManager] 플레이어 유닛 {playerUnits.Count}건을 로드했습니다.");

        enemyUnits.Clear();
        EnemyCsvLoadResult enemyCsv = csvLoader.LoadEnemyCsv();
        enemyUnits.AddRange(enemyCsv.Enemies);
        Debug.Log($"[DataManager] 적 유닛 {enemyUnits.Count}건을 로드했습니다.");
    }
}
*/
