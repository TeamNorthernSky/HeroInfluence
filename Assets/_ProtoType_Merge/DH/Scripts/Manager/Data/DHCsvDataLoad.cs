using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DH 탐험씬에서 사용할 CSV 로더 래퍼입니다.
/// ASB의 CSVLoader 파서를 그대로 사용하되, DH 쪽 인스펙터 구성을 분리합니다.
/// </summary>
[DisallowMultipleComponent]
public class DHCsvDataLoad : MonoBehaviour
{
    [Header("Unit CSV")]
    [SerializeField] private TextAsset playerUnitCsv;
    [SerializeField] private TextAsset enemyUnitCsv;

    [Header("Optional CSV")]
    [SerializeField] private TextAsset classSkillSheetCsv;
    [SerializeField] private TextAsset weaponSheetCsv;

    public List<UnitData> LoadPlayerUnits()
    {
        if (playerUnitCsv == null)
        {
            Debug.LogError("[DHCsvDataLoad] playerUnitCsv가 비어 있습니다.", this);
            return new List<UnitData>();
        }

        List<UnitData> allUnits = CSVLoader.LoadUnitData(playerUnitCsv.text);
        if (enemyUnitCsv != null)
            return allUnits;

        List<UnitData> players = new List<UnitData>();
        for (int i = 0; i < allUnits.Count; i++)
        {
            UnitData unit = allUnits[i];
            if (unit == null || CSVLoader.IsEnemyUnitRow(unit))
                continue;

            players.Add(unit);
        }

        return players;
    }

    public EnemyCsvLoadResult LoadEnemyCsv()
    {
        if (enemyUnitCsv != null)
            return CSVLoader.LoadEnemyDataAndSkills(enemyUnitCsv.text);

        if (playerUnitCsv == null)
        {
            Debug.LogWarning("[DHCsvDataLoad] enemyUnitCsv와 playerUnitCsv가 모두 비어 있습니다.", this);
            return new EnemyCsvLoadResult();
        }

        List<UnitData> allUnits = CSVLoader.LoadUnitData(playerUnitCsv.text);
        List<EnemyData> enemies = new List<EnemyData>();
        for (int i = 0; i < allUnits.Count; i++)
        {
            UnitData unit = allUnits[i];
            if (unit == null || !CSVLoader.IsEnemyUnitRow(unit))
                continue;

            enemies.Add(CSVLoader.UnitDataToEnemyData(unit, string.Empty));
        }

        return new EnemyCsvLoadResult
        {
            Enemies = enemies,
            Skills = new List<SkillData>()
        };
    }

    public List<SkillData> LoadClassSkills()
    {
        if (classSkillSheetCsv == null)
        {
            Debug.LogWarning("[DHCsvDataLoad] classSkillSheetCsv가 비어 있습니다.", this);
            return new List<SkillData>();
        }

        return CSVLoader.LoadClassSkillData(classSkillSheetCsv.text);
    }

    public List<WeaponData> LoadWeapons()
    {
        if (weaponSheetCsv == null)
        {
            Debug.LogWarning("[DHCsvDataLoad] weaponSheetCsv가 비어 있습니다.", this);
            return new List<WeaponData>();
        }

        return CSVLoader.LoadWeaponData(weaponSheetCsv.text);
    }
}
