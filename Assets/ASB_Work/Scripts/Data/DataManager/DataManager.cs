using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CSVDataLoad.LoadPlayerUnits / LoadEnemyUnits → UnitData.baseStats(원본) → CharactorManager·EnemyManager 캐시.
/// 씬의 CharactorScript/EnemyScript.Initialize(UnitData)에서 BattleCharactor.SetBaseStats → StatCalculator 경로로 전달됩니다.
/// 프로토타입 전투(BattleSceneManager만 사용)는 이 목록 없이 BattleCharactor 인스펙터로 base를 구성합니다.
/// </summary>
public class DataManager : MonoBehaviour
{
    [SerializeField] private CSVDataLoad csvLoader;
    [SerializeField] private SkillDataLoader skillDataLoader;
    [SerializeField] private WeaponDataLoader weaponDataLoader;
    [SerializeField] private CharactorManager charactorManager;
    [SerializeField] private EnemyManager enemyManager;
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

        if (charactorManager == null)
        {
            Debug.LogError("[DataManager] charactorManager가 할당되지 않았습니다.");
            return;
        }

        if (enemyManager == null)
        {
            Debug.LogError("[DataManager] enemyManager가 할당되지 않았습니다.");
            return;
        }

        playerUnits.Clear();
        playerUnits.AddRange(csvLoader.LoadPlayerUnits());
        charactorManager.SetUnitData(playerUnits);
        Debug.Log($"[DataManager] 플레이어 유닛 {playerUnits.Count}건을 CharactorManager에 반영했습니다.");

        enemyUnits.Clear();
        EnemyCsvLoadResult enemyCsv = csvLoader.LoadEnemyCsv();
        enemyUnits.AddRange(enemyCsv.Enemies);
        enemyManager.SetEnemyData(enemyUnits);
        Debug.Log($"[DataManager] 적 유닛 {enemyUnits.Count}건을 EnemyManager에 반영했습니다.");

        List<SkillData> classSkills = csvLoader.LoadClassSkills();
        var allSkills = new List<SkillData>(classSkills.Count + enemyCsv.Skills.Count);
        allSkills.AddRange(classSkills);
        allSkills.AddRange(enemyCsv.Skills);
        if (skillDataLoader != null)
        {
            skillDataLoader.ReplaceAll(allSkills);
            Debug.Log($"[DataManager] SkillDataLoader 통합: 클래스 스킬 {classSkills.Count}건 + 적 스킬 {enemyCsv.Skills.Count}건 = {allSkills.Count}건.");
        }
        else if (allSkills.Count > 0)
        {
            Debug.LogWarning("[DataManager] SkillDataLoader가 없어 스킬 CSV는 로드만 되었고 메모리 등록은 생략되었습니다.");
        }

        List<WeaponData> weapons = csvLoader.LoadWeapons();
        if (weaponDataLoader != null)
        {
            weaponDataLoader.ReplaceAll(weapons);
            Debug.Log($"[DataManager] 무기 {weapons.Count}건을 WeaponDataLoader에 반영했습니다.");
        }
        else if (weapons.Count > 0)
        {
            Debug.LogWarning("[DataManager] WeaponDataLoader가 없어 무기 CSV는 로드만 되었고 메모리 등록은 생략되었습니다.");
        }
    }
}
