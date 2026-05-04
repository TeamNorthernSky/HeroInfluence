using UnityEngine;

/// <summary>
/// CSV 기반 유닛 데이터 보관. Combat tuning은 BattleCharactor에만 반영하고,
/// 에디터 OnValidate에서 BattleCharactor를 덮어쓰지 않습니다.
/// </summary>
public class CharactorScript : MonoBehaviour, IUnitIdentifier
{
    public UnitData charactorData;

    public StatBlock currentStats;

    [Header("Stat Weights (CSV Initialize 시 BattleCharactor로 전달)")]
    [SerializeField] private StatWeights unitWeight = new StatWeights(1f, 1f, 1f);

    [SerializeField] private StatWeights levelWeight = new StatWeights(1f, 1f, 1f);

    [SerializeField] private StatWeights classWeight = new StatWeights(1f, 1f, 1f);

    [Range(1, 15)] public int level = 1;

    [Range(1, 100)] public int unitCount = 1;

    public StatWeights UnitWeight => unitWeight;

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

    public void Initialize(UnitData data)
    {
        charactorData = data;
        currentStats = data != null ? data.baseStats : default;

        var battle = GetComponent<BattleCharactor>();
        if (battle == null)
        {
            return;
        }

        if (data != null)
        {
            battle.SetBaseStats(data.baseStats);
            battle.SetUnitNameForSkillMatching(data.Name);
        }
        else
        {
            // 프로토타입 직접 입력 모드: 유닛 데이터 에셋이 없으면 인스펙터 base 값을 원본으로 사용
            battle.BuildBaseStatsFromInspector();
            battle.SetUnitNameForSkillMatching(battle.UnitName);
        }

        battle.ApplyCombatTuning(level, unitCount, unitWeight, levelWeight, classWeight);
        battle.RecalculateStats();
        battle.ResolveSelectedSkill();
        battle.InitializeCurrentHpToMax();
        battle.MarkInitializedFromDataPipeline();
    }

    private void OnValidate()
    {
        level = Mathf.Clamp(level, 1, 15);
        unitCount = Mathf.Clamp(unitCount, 1, 100);
    }
}
