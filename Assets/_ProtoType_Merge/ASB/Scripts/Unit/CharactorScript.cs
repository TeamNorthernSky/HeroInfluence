using UnityEngine;

/// <summary>
/// CSV 기반 유닛 데이터 보관. Combat tuning은 BattleCharactor에만 반영하고,
/// 에디터 OnValidate에서 BattleCharactor를 덮어쓰지 않습니다.
/// </summary>
public class CharactorScript : MonoBehaviour, IUnitIdentifier
{
    public UnitData charactorData;


    [Header("Stat Weights (CSV Initialize 시 BattleCharactor로 전달)")]
    [SerializeField] private int level = 1;
    [SerializeField] private StatWeights levelWeight;
    [SerializeField] private StatWeights classWeight;
    [SerializeField] private StatBlock inspectorBaseStats;

    public StatWeights LevelWeight => levelWeight;

    public StatWeights ClassWeight => classWeight;

    public UnitData Data
    {
        get => charactorData;
        set => charactorData = value;
    }

    public string UnitID
    {
        get
        {
            if (charactorData == null || string.IsNullOrWhiteSpace(charactorData.Index))
            {
                return string.Empty;
            }

            return charactorData.Index.Trim();
        }
    }

    public void Initialize(UnitData data = null)
    {
        charactorData = data;
        BattleCharactor battle = GetComponent<BattleCharactor>();
        if (battle == null)
        {
            return;
        }

        StatBlock baseStats;
        if (data != null)
        {
            baseStats = data.baseStats;
            battle.SetUnitNameForSkillMatching(data.Name);
        }
        else
        {
            baseStats = inspectorBaseStats;
            battle.SetUnitNameForSkillMatching(battle.UnitName);
        }

        battle.SetBaseStats(baseStats);
        battle.SetLevelScaling(true);
        battle.ApplyCombatTuning(level, levelWeight, classWeight);
        battle.RecalculateStats();
        battle.ResolveSelectedSkill();
        battle.InitializeCurrentHpToMax();
        battle.MarkInitializedFromDataPipeline();
    }

    public void Initialize(UnitPersistentData persistentData, UnitData fallbackData = null)
    {
        if (persistentData == null)
        {
            Initialize(fallbackData);
            return;
        }

        charactorData = fallbackData;
        BattleCharactor battle = GetComponent<BattleCharactor>();
        if (battle == null)
        {
            return;
        }

        battle.BindPersistentSourceData(persistentData);
        battle.SetBaseStats(persistentData.BaseStats);
        // persistentData.BaseStats는 레벨/장비/무기 보정이 끝난 전투 스냅샷이므로
        // 추가 스케일링(StatCalculator 경로)을 비활성화합니다.
        battle.SetLevelScaling(false);

        if (!string.IsNullOrWhiteSpace(persistentData.UnitTemplateKey))
        {
            battle.SetUnitNameForSkillMatching(persistentData.UnitTemplateKey);
        }
        else if (fallbackData != null)
        {
            battle.SetUnitNameForSkillMatching(fallbackData.Name);
        }

        battle.LoadPersistentEquipment(persistentData.CurrentSkillIndex, persistentData.CurrentWeaponIndex);

        battle.RecalculateStats();
        battle.InitializeCurrentHpToMax();
        battle.MarkInitializedFromDataPipeline(true);
        Debug.Log(
            $"[Stats/Persistent] {battle.UnitName} uses precomputed snapshot. " +
            $"LevelScaling=false, " +
            $"FinalStats HP={battle.FinalStats.HP}, Atk={battle.FinalStats.Atk}, DEF={battle.FinalStats.DEF}");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        level = Mathf.Clamp(level, 1, 15);

        BattleCharactor battle = GetComponent<BattleCharactor>();
        if (battle == null)
        {
            return;
        }

        StatBlock baseStats = inspectorBaseStats;

        battle.SetBaseStats(baseStats);
        battle.SetLevelScaling(true);
        battle.ApplyCombatTuning(level, levelWeight, classWeight);
        battle.RecalculateStats(false);
    }
#endif
}
