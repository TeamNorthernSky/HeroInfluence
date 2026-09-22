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

    /// <summary>런타임 전투 식별자. 영속 저장소 키로 사용하지 마세요(인스턴스마다 달라짐).</summary>
    public string UnitID
    {
        get
        {
            if (charactorData == null || string.IsNullOrWhiteSpace(charactorData.Index))
            {
                return gameObject.GetInstanceID().ToString();
            }

            return $"{charactorData.Index.Trim()}_{gameObject.GetInstanceID()}";
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
            string skillMatchKey = ResolveSkillMatchKey(data);
            battle.SetUnitNameForSkillMatching(skillMatchKey);
            battle.SetDisplayName(data.Name); // [JC 260621] 표시명=히어로명(스킬매칭키=클래스명과 분리)
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
        battle.SetBaseStats(persistentData.IngameStats);
        // IngameStats = 레벨·무기·성장 보너스가 반영된 전투 스냅샷(Max IP 포함).
        // 추가 스케일링(StatCalculator 경로)은 비활성화합니다.
        battle.SetLevelScaling(false);
        // 능력치 스냅샷은 유지하되, 스킬 해금 판정에는 저장된 캐릭터 레벨을 사용합니다.
        battle.ApplyCombatTuning(persistentData.Level, levelWeight, classWeight);

        string persistentSkillMatchKey = ResolvePersistentSkillMatchKey(persistentData, fallbackData);
        if (!string.IsNullOrWhiteSpace(persistentSkillMatchKey))
        {
            battle.SetUnitNameForSkillMatching(persistentSkillMatchKey);
        }
        else if (fallbackData != null)
        {
            battle.SetUnitNameForSkillMatching(ResolveSkillMatchKey(fallbackData));
        }

        // [JC 260621] 표시명=히어로명(template.Name). 스킬매칭키(클래스명/UnitType)와 분리.
        battle.SetDisplayName(ResolveDisplayName(persistentData, fallbackData));

        battle.LoadPersistentEquipment(
            persistentData.CurrentSkillIndex,
            persistentData.CurrentWeaponIndex,
            persistentData.SkillLevel,
            persistentData.EquippedWeaponInstanceIndex);

        battle.RecalculateStats();
        battle.InitializeCurrentState(persistentData.CurrentHp, persistentData.CurrentInfluence);
        battle.MarkInitializedFromDataPipeline(true);
        Debug.Log(
            $"[Stats/Persistent] {battle.UnitName} uses precomputed snapshot. " +
            $"LevelScaling=false, " +
            $"FinalStats HP={battle.FinalStats.HP}, Atk={battle.FinalStats.Atk}, DEF={battle.FinalStats.DEF}, " +
            $"MaxIP={battle.MaxInfluence:0.#}, CurrentIP={battle.CurrentInfluence:0.#}");
    }

    private static string ResolveSkillMatchKey(UnitData data)
    {
        if (data == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(data.UnitType))
        {
            return data.UnitType.Trim();
        }

        if (!string.IsNullOrWhiteSpace(data.Name))
        {
            return data.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(data.Index))
        {
            return data.Index.Trim();
        }

        return string.Empty;
    }

    // [JC 260621] UI 표시용 히어로명 해석(스킬매칭키=UnitType과 별개로 항상 Name 우선).
    private static string ResolveDisplayName(UnitPersistentData persistentData, UnitData fallbackData)
    {
        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (persistentData != null && catalog != null &&
            !string.IsNullOrWhiteSpace(persistentData.UnitTemplateKey) &&
            catalog.TryGetPlayerTemplate(persistentData.UnitTemplateKey, out UnitData template) &&
            template != null && !string.IsNullOrWhiteSpace(template.Name))
        {
            return template.Name.Trim();
        }

        if (fallbackData != null && !string.IsNullOrWhiteSpace(fallbackData.Name))
        {
            return fallbackData.Name.Trim();
        }

        return null;
    }

    private static string ResolvePersistentSkillMatchKey(UnitPersistentData persistentData, UnitData fallbackData)
    {
        if (persistentData == null)
        {
            return ResolveSkillMatchKey(fallbackData);
        }

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null &&
            !string.IsNullOrWhiteSpace(persistentData.UnitTemplateKey) &&
            catalog.TryGetPlayerTemplate(persistentData.UnitTemplateKey, out UnitData template) &&
            template != null)
        {
            string fromTemplate = ResolveSkillMatchKey(template);
            if (!string.IsNullOrWhiteSpace(fromTemplate))
            {
                return fromTemplate;
            }
        }

        string fromFallback = ResolveSkillMatchKey(fallbackData);
        if (!string.IsNullOrWhiteSpace(fromFallback))
        {
            return fromFallback;
        }

        if (!string.IsNullOrWhiteSpace(persistentData.UnitTemplateKey))
        {
            return persistentData.UnitTemplateKey.Trim();
        }

        return string.Empty;
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
