using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

/// <summary>
/// 전투 유닛: 원본(base) 스탯 → StatCalculator → 최종(final) 스탯 → 런타임 HP.
/// - base 원본은 래퍼(CharactorScript/EnemyScript)에서 조립해 SetBaseStats로 주입합니다.
/// Combat tuning(level, 가중치)의 최종 소유는 이 컴포넌트만 담당합니다.
/// </summary>
public partial class BattleCharactor : MonoBehaviour, IUnitIdentifier
{
    public event Action<BattleCharactor> OnDied;
    public event Action<float, float> OnHpChanged;
    public event Action<float, float> OnInfluenceChanged;

    [Header("Identity")]
    [SerializeField] private string unitName = "Unit";
    [SerializeField] private TeamType teamType = TeamType.Player;

    /// <summary>
    /// 전투 씬 런타임 전용 키(battleById 등). 씬/인스턴스마다 고유.
    /// 영속 저장소(UnitPersistentData 등)에 기록하지 마세요.
    /// </summary>
    [NonSerialized]
    private string runtimeInstanceKey;

    [Header("Combat tuning (StatCalculator 가중치 · 최종 소유)")]
    private int level = 1;
    private StatWeights levelWeight = new StatWeights(1f, 1f, 1f);
    private StatWeights classWeight = new StatWeights(1f, 1f, 1f);
    private bool useLevelScaling = true;

    [Header("Computed / runtime (에디터 미리보기 = finalStats)")]
    [Tooltip("StatCalculator 결과. 인스펙터에서 결과 확인용.")]
    [SerializeField] private StatBlock finalStats;
    [SerializeField] private float currentHp;
    [SerializeField] private float currentInfluence;

    [SerializeField] private List<StatusEffectInstance> activeStatusEffects = new List<StatusEffectInstance>();
    [NonSerialized] private StatusEffectManager statusEffectManager;
    [HideInInspector] [SerializeField] private Skill activeSkill;
    // skillDataLoader 제거 — DHCsvTemplateCatalog.Instance 로 대체
    /// <summary>availableWeapons 목록 내 로컬 인덱스(0..Count-1). 인스펙터에서는 BattleCharactorEditor 드롭다운으로만 설정합니다.</summary>
    [HideInInspector] [SerializeField] private int equippedWeaponIndex;
    [HideInInspector] public List<WeaponData> availableWeapons = new List<WeaponData>();
    public WeaponData EquippedWeaponData { get; private set; }

    // [레거시 fallback]
    // 메인 스킬 선택은 unitName 기반 매칭으로 변경됨
    // availableSkills 매칭 실패 시에만 사용
    [Tooltip("레거시 fallback 인덱스. unitName 매칭 실패 시에만 조회합니다.")]
    [HideInInspector] [SerializeField] private int classSkillIndex = 1010;
    // 의미 변경: 전역 skillIndex가 아니라
    // availableSkills 리스트 안에서의 로컬 인덱스
    [HideInInspector] [SerializeField] private int selectedSkillIndex;

    [HideInInspector] public List<SkillData> availableSkills = new List<SkillData>();
    public SkillData SelectedSkillData { get; private set; }
    public UnitPersistentData SourceData { get; private set; }
    public EnemyUnitPersistentData SourceEnemyData { get; private set; }
    public float ExperienceReward { get; private set; }

    /// <summary>계산 원본. 래퍼에서 SetBaseStats로 주입합니다.</summary>
    private StatBlock runtimeBaseStats;

    private GridCellRef occupiedCell;
    private bool isInitialized;
    private bool hasLoggedEmptyUnitNameWarning;
    private Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();

    /// <summary>프로토타입 경로: SkillManager에서 가져온 후보 목록의 캐시 키(unitName|level).</summary>
    private string prototypeSkillsCacheKey;

    /// <summary>프로토타입 경로: WeaponManager에서 가져온 후보 목록의 캐시 키(unitName).</summary>
    private string prototypeWeaponsCacheKey;

    public List<EquipmentData> EquippedEquipments { get; private set; }

    public List<SkillDataAsset> EquippedSkills { get; private set; }

    public int ClassSkillIndex => classSkillIndex;

    public bool IsPlayer { get; set; }

    [SerializeField]  public bool IsDead { get; private set; }
    public bool IsStunned => Effects.IsStunned;

    /// <summary>이번 전투에서 부활(RebirthSkill)을 이미 사용했는지. 전투 시작(MarkInitializedFromDataPipeline)에 리셋.</summary>
    public bool HasUsedRevive { get; set; }

    /// <summary>이번 시전에서 부활시킬 아군(연출의 SpawnAnchor.ReviveTarget이 참조). 부활 없으면 null. RebirthSkillHandler가 매 시전 설정.</summary>
    public BattleCharactor PendingReviveTarget { get; set; }
    public bool IsHealBanned => Effects.IsHealBanned;

    private StatusEffectManager Effects
    {
        get
        {
            if (statusEffectManager == null)
            {
                if (activeStatusEffects == null)
                {
                    activeStatusEffects = new List<StatusEffectInstance>();
                }

                statusEffectManager = new StatusEffectManager(this, activeStatusEffects);
            }

            return statusEffectManager;
        }
    }
    public IUnitData UnitData => null;

    /// <summary>
    /// 딕셔너리 등 런타임 조회용 고유 ID. 스킬 CSV 매칭용 <see cref="unitName"/>에는 포함되지 않습니다.
    /// 스포너가 <c>gameObject.name</c>에 Index와 GetInstanceID를 붙인 뒤에는 그 이름을 그대로 씁니다(이중 접미사 방지).
    /// </summary>
    public string UnitId
    {
        get
        {
            if (string.IsNullOrWhiteSpace(unitName) || unitName == "Unit")
            {
                return gameObject.name;
            }

            return $"{unitName}_{gameObject.GetInstanceID()}";
        }
    }

    /// <summary>스킬/무기 매칭 원본(접미사 없음). 플레이어는 클래스명(UnitType)이 들어감 — 표시엔 <see cref="DisplayName"/> 사용.</summary>
    public string UnitName => string.IsNullOrWhiteSpace(unitName) ? gameObject.name : unitName;

    // [JC 260621] 표시 전용 이름(히어로명). 스킬매칭키(unitName=클래스명)와 분리. 비면 UnitName 폴백(적 유닛 호환).
    private string displayName;
    /// <summary>UI 표시·로그용 히어로명. 미설정 시 UnitName 폴백.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? UnitName : displayName;

    public TeamType TeamType => teamType;

    /// <summary><see cref="UnitId"/>와 동일. IUnitIdentifier 및 레거시 호환.</summary>
    public string UnitID => UnitId;
    public int Level => level;

    public StatBlock FinalStats => finalStats;

    public float CurrentHp => currentHp;
    public float MaxHp => finalStats.HP;
    public float CurrentInfluence
    {
        get => currentInfluence;
        private set => currentInfluence = value;
    }
    /// <summary>전투 IP 상한. 영속 유닛은 IngameStats.Influence(레벨·무기·성장 반영), 그 외는 FinalStats.</summary>
    public float MaxInfluence => ResolveMaxInfluenceCap();

    private float ResolveMaxInfluenceCap()
    {
        if (SourceData != null)
        {
            return Mathf.Clamp(SourceData.IngameStats.Influence, 0f, 200f);
        }

        if (SourceEnemyData != null)
        {
            return Mathf.Clamp(SourceEnemyData.IngameStats.Influence, 0f, 200f);
        }

        return FinalStats.Influence;
    }
    public IReadOnlyList<StatusEffectInstance> ActiveStatusEffects => Effects.ActiveEffects;

    // [레거시] 인스펙터 스킬 선택 방식으로 전환 완료 후 제거 예정.
    // [마이그레이션 종료 기준]
    // 1) 모든 BattleCharactor의 인스펙터 스킬 선택 완료
    // 2) 전체 씬에서 SelectedSkillData 경로 정상 동작 확인
    // 3) ActiveSkill 참조가 더 이상 없음 확인
    public Skill ActiveSkill => activeSkill;
    public int SelectedSkillIndex => selectedSkillIndex;
    public int EquippedWeaponIndex => equippedWeaponIndex;


    public GridCellRef OccupiedCell => occupiedCell;
    public bool IsInitialized => isInitialized;
    /// <summary>
    /// 절대 좌표 기반 동적 전열 판정.
    /// 기존 하드코딩(x==0) 의존 제거를 위해 TargetingHelper 공용 로직을 사용합니다.
    /// </summary>
    public bool IsInFrontRow => TargetingHelper.IsUnitInFrontRow(this);
    public bool IsInBackRow => TargetingHelper.IsUnitInBackRow(this);

    /// <summary>
    /// 고정 좌표 기반 전열 판정.
    /// - 플레이어 전열: x == 1
    /// - 적 전열: x == 2
    /// </summary>
    public bool IsFrontRow()
    {
        GridCellRef cell = occupiedCell;
        if (cell == null)
        {
            cell = ASB.Work.BattleGrid.BattleGridManager.Instance?.FindCellByUnit(this);
        }

        if (cell == null)
        {
            return false;
        }

        if (IsPlayer)
        {
            return cell.Coords.x == 1;
        }

        return cell.Coords.x == 2;
    }

    /// <summary>디버그용: 현재 계산에 쓰는 base 원본.</summary>
    public StatBlock RuntimeBaseStats => runtimeBaseStats;

    private void EnsureRuntimeInstanceKey()
    {
        if (!string.IsNullOrEmpty(runtimeInstanceKey))
        {
            return;
        }

        string full = Guid.NewGuid().ToString("N");
        runtimeInstanceKey = full.Length >= 8 ? full.Substring(0, 8) : full;
    }

    private void Awake()
    {
        EnsureRuntimeInstanceKey();
        EnsureAnimationController();

        if (EquippedEquipments == null)
        {
            EquippedEquipments = new List<EquipmentData>();
        }

        if (EquippedSkills == null)
        {
            EquippedSkills = new List<SkillDataAsset>();
        }

        if (availableWeapons == null)
        {
            availableWeapons = new List<WeaponData>();
        }

        if (activeStatusEffects == null)
        {
            activeStatusEffects = new List<StatusEffectInstance>();
        }
        statusEffectManager = new StatusEffectManager(this, activeStatusEffects);

        originalColors.Clear();
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material material = renderer.material;
            if (material != null && material.HasProperty("_Color"))
            {
                originalColors[renderer] = material.color;
            }
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        // Wrapper(플레이어/적)가 소유권을 가지는 경우, 코어의 자동 미리보기 갱신은 충돌을 유발할 수 있어 스킵합니다.
        if (GetComponent<CharactorScript>() != null || GetComponent<EnemyScript>() != null)
        {
            return;
        }

        PrototypeEditorValidateOrRefresh(forceSkillWeaponRefresh: false);
    }

    /// <summary>에디터 전용: 인스펙터 버튼으로 스킬/무기 후보·스탯 미리보기를 강제 갱신합니다.</summary>
    public void EditorForcePrototypeRefresh()
    {
        PrototypeEditorValidateOrRefresh(forceSkillWeaponRefresh: true);
    }

    /// <summary>에디터 OnValidate / Force Refresh 공통: 값 클램프 → base → 스킬/무기 → RecalculateStats(false).</summary>
    private void PrototypeEditorValidateOrRefresh(bool forceSkillWeaponRefresh)
    {
        level = Mathf.Max(1, level);

        RefreshAvailableSkillsForInspector(forceSkillWeaponRefresh);
        ResolveSelectedSkill(false);
        RefreshAvailableWeapons(forceSkillWeaponRefresh);
        ResolveEquippedWeapon(false);
        RecalculateStats(applyCurrentHpClamp: false);
    }

    private void OnDisable()
    {
        ClearOccupiedCell();
    }

    /// <summary>CSV 등 외부 base 주입. RecalculateStats는 호출하지 않습니다.</summary>
    public void SetBaseStats(StatBlock stats)
    {
        runtimeBaseStats = stats;
    }

    /// <summary>CSV 초기화 시에만 외부에서 호출. 인스펙터 tuning의 최종 값으로 반영합니다.</summary>
    public void ApplyCombatTuning(int lv, StatWeights lw, StatWeights cw)
    {
        level = Mathf.Max(1, lv);
        levelWeight = lw;
        classWeight = cw;
    }

    public void SetLevelScaling(bool useScaling)
    {
        useLevelScaling = useScaling;
    }

    /// <summary>StatCalculator로 finalStats 갱신. applyCurrentHpClamp가 true일 때만 currentHp 상한을 맞춥니다.</summary>
    public void RecalculateStats(bool applyCurrentHpClamp = true)
    {
        if (useLevelScaling)
        {
            finalStats = StatCalculator.CalculateFinalStats(
                runtimeBaseStats,
                level,
                levelWeight,
                classWeight,
                EquippedEquipments,
                EquippedWeaponData != null ? EquippedWeaponData.GetBonusStatBlock() : default);
        }
        else
        {
            // 영속 스냅샷: runtimeBaseStats에 이미 무기·장비·레벨 보정이 반영됨.
            // EquippedWeaponData는 스킬/UI용으로만 유지하고 StatCalculator·무기 보너스는 합산하지 않습니다.
            finalStats = runtimeBaseStats;
            finalStats.HP = Mathf.Max(1f, finalStats.HP);
            finalStats.Atk = Mathf.Max(0f, finalStats.Atk);
            finalStats.DEF = Mathf.Max(0f, finalStats.DEF);
        }

        // CSV 미연동 신규 스탯 안전장치
        finalStats.Influence = Mathf.Clamp(finalStats.Influence, 0f, 200f);
        ApplyStatusEffectStatModifiers();
        ApplyFormationPassiveModifiers();

        if (applyCurrentHpClamp)
        {
            currentHp = Mathf.Clamp(currentHp, 0f, finalStats.HP);
        }
    }

    /// <summary>초기화 시점에만 HP를 최대(= final HP)로 맞춥니다.</summary>
    public void InitializeCurrentHpToMax()
    {
        currentHp = finalStats.HP;
        OnHpChanged?.Invoke(CurrentHp, MaxHp);
        CurrentInfluence = MaxInfluence;
        OnInfluenceChanged?.Invoke(CurrentInfluence, MaxInfluence);
    }

    public void InitializeCurrentState(float hp, float influence)
    {
        currentHp = Mathf.Clamp(hp, 0f, MaxHp);
        CurrentInfluence = Mathf.Clamp(influence, 0f, MaxInfluence);
        IsDead = currentHp <= 0f;

        if (IsDead)
        {
            DisableVisuals();
        }
        else
        {
            EnableVisuals();
        }

        OnHpChanged?.Invoke(CurrentHp, MaxHp);
        OnInfluenceChanged?.Invoke(CurrentInfluence, MaxInfluence);
    }

    public bool TryConsumeInfluence(float amount)
    {
        float required = Mathf.Max(0f, amount);
        if (CurrentInfluence < required)
        {
            return false;
        }

        CurrentInfluence -= required;
        OnInfluenceChanged?.Invoke(CurrentInfluence, MaxInfluence);
        return true;
    }

    /// <summary>전투 결과에 따른 Influence 비율 보정. ratio 1.1 = +10%, 0.9 = -10%</summary>
    public void ApplyInfluenceModifier(float ratio)
    {
        CurrentInfluence = Mathf.Clamp(CurrentInfluence * ratio, 0f, MaxInfluence);
        OnInfluenceChanged?.Invoke(CurrentInfluence, MaxInfluence);
    }

    public void LevelUp(int amount = 1)
    {
        level = Mathf.Max(1, level + amount);
        RecalculateStats();
    }

    public void TakeDamage(float amount)
    {
        if (IsDead)
        {
            return;
        }

        float damage = Mathf.Max(0f, amount);
        currentHp = Mathf.Max(0f, currentHp - damage);
        
        // UI 갱신 이벤트를 "죽기(DisableVisuals)" 이전에 먼저 호출합니다.
        // (Die()에서 Canvas.enabled=false 처리로 인해 마지막 HP 표시가 누락되는 문제 방지)
        Debug.Log($"[HP] {UnitName} HP changed: {CurrentHp}/{MaxHp}");
        OnHpChanged?.Invoke(CurrentHp, MaxHp);

        if (currentHp <= 0f)
        {
            Die();
        }
    }

    public void ApplyHeal(float amount)
    {
        if (IsDead)
        {
            return;
        }

        if (IsHealBanned)
        {
            Debug.Log($"[HealBan] {UnitName}은(는) 힐 금지 상태여서 체력을 회복할 수 없습니다!");
            return;
        }

        float heal = Mathf.Max(0f, amount);
        if (heal <= 0f)
        {
            return;
        }

        float before = currentHp;
        currentHp = Mathf.Min(MaxHp, currentHp + heal);
        float delta = currentHp - before;
        if (delta > 0f)
        {
            Debug.Log($"[Battle] Heal: {UnitName} +{delta:F1} (HP {before:F1}->{currentHp:F1}/{finalStats.HP:F1})");
        }

        // 힐도 동일하게 HP 값 갱신 직후 이벤트를 호출합니다.
        OnHpChanged?.Invoke(CurrentHp, MaxHp);
    }

    public void ApplyStatusEffect(StatusEffectInstance effect)
    {
        if (Effects.ApplyStatusEffect(effect))
        {
            RecalculateStats(applyCurrentHpClamp: true);
        }
    }

    public void RemoveStatusEffect(StatusEffectType effectType)
    {
        if (Effects.RemoveStatusEffect(effectType))
        {
            RecalculateStats(applyCurrentHpClamp: true);
        }
    }

    public bool HasStatusEffect(StatusEffectType effectType)
    {
        return Effects.HasStatusEffect(effectType);
    }

    public BattleCharactor GetTauntSource()
    {
        return Effects.GetTauntSource();
    }

    public void ProcessTurnStartStatusEffects()
    {
        Effects.ProcessTurnStartStatusEffects();
    }

    public void AdvanceStatusEffectDuration()
    {
        if (Effects.AdvanceStatusEffectDuration())
        {
            RecalculateStats(applyCurrentHpClamp: true);
        }
    }
    private void Die()
    {
        if (IsDead)
        {
            return;
        }

        IsDead = true;
        currentHp = 0f;

        // 대열 경계(minX/maxX)는 '생존한' 팀원만으로 계산된다. 죽은 순간 경계가 바뀌므로
        // 남은 팀원의 대열 패시브(전열 CounterRate / 후열 CriticalRate)를 다시 계산해야 한다.
        // IsDead=true 이후에 호출해야 이 유닛이 경계 계산에서 빠진다.
        RefreshFormationPassiveStatsForAllUnits();

        EnsureAnimationController();
        Anim?.PlayGenericAnimation("Die");
        OnDied?.Invoke(this);
        DisableVisuals();
    }

    private void DisableVisuals()
    {
        foreach (var renderer in originalColors.Keys)
        {
            if (renderer == null)
            {
                continue;
            }

            Material material = renderer.material;
            if (material != null && material.HasProperty("_Color"))
            {
                material.color = Color.black;
            }
        }

        var canvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null)
            {
                canvases[i].enabled = false;
            }
        }

        StopAllCoroutines();
    }

    /// <summary>시체 상태에서 전투 복귀. hpRatio는 최대 체력 대비 비율(예: 0.5 = 50%).</summary>
    public void Revive(float hpRatio)
    {
        if (!IsDead)
        {
            return;
        }

        IsDead = false;
        float ratio = Mathf.Clamp01(hpRatio);
        currentHp = Mathf.Clamp(MaxHp * ratio, 1f, MaxHp);

        // 부활로 대열 경계가 되돌아간다. 사망 시와 대칭으로 재계산한다(IsDead=false 이후).
        RefreshFormationPassiveStatsForAllUnits();

        EnableVisuals();

        // 부활 HP를 HP바 UI에 반영한다. (이게 없으면 사망 시점의 0 표시가 남아 '부활했는데 HP 0'처럼 보임)
        OnHpChanged?.Invoke(CurrentHp, MaxHp);

        EnsureAnimationController();
        Anim?.PlayGenericAnimation("Revive");

        Debug.Log($"[Battle] Revive: {UnitName} HP={currentHp:F1}/{MaxHp:F1} (ratio={ratio:0.##})");
    }

    private void EnableVisuals()
    {
        foreach (var kvp in originalColors)
        {
            Renderer renderer = kvp.Key;
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = true;
            Material material = renderer.material;
            if (material != null && material.HasProperty("_Color"))
            {
                material.color = kvp.Value;
            }
        }

        var colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = true;
            }
        }

        var colliders2D = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders2D.Length; i++)
        {
            if (colliders2D[i] != null)
            {
                colliders2D[i].enabled = true;
            }
        }

        var canvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null)
            {
                canvases[i].enabled = true;
            }
        }

    }

    public void ReviveToFull()
    {
        IsDead = false;
        InitializeCurrentHpToMax();

        // Revive(float)와 동일하게 대열 경계 복귀를 반영한다.
        RefreshFormationPassiveStatsForAllUnits();
    }

    public void AssignToCell(GridCellRef cell)
    {
        if (occupiedCell == cell)
        {
            if (cell != null)
            {
                cell.SetOccupyingUnit(this);
                ASB.Work.BattleGrid.BattleGridManager.Instance?.RegisterUnitToCell(this, cell);
            }

            return;
        }

        if (cell != null && cell.OccupyingUnit != null && cell.OccupyingUnit != this)
        {
            Debug.LogWarning(
                $"[BattleCharactor] 셀 점유 불가(다른 유닛/시체): cell={cell.name}, occupant={cell.OccupyingUnit.UnitName}, incoming={UnitName}");
            return;
        }

        if (occupiedCell != null)
        {
            ASB.Work.BattleGrid.BattleGridManager.Instance?.UnregisterUnit(this);
            occupiedCell.ClearIfOccupying(this);
        }

        occupiedCell = cell;
        if (occupiedCell != null)
        {
            occupiedCell.SetOccupyingUnit(this);
            ASB.Work.BattleGrid.BattleGridManager.Instance?.RegisterUnitToCell(this, occupiedCell);
        }

        RefreshFormationPassiveStatsForAllUnits();
    }

    public void SyncOccupancy(GridCellRef cell)
    {
        AssignToCell(cell);
    }

    public void ClearOccupiedCell()
    {
        ASB.Work.BattleGrid.BattleGridManager.Instance?.UnregisterUnit(this);
        if (occupiedCell != null)
        {
            occupiedCell.ClearIfOccupying(this);
            occupiedCell = null;
        }
    }

    /// <summary>프로토타입 부트: CSV 미주입 시 인스펙터로 base 구성 후 계산·HP 초기화.</summary>
    public void Initialize()
    {
        if (isInitialized)
        {
            return;
        }

        IsPlayer = teamType == TeamType.Player;
        IsDead = false;

        RefreshAvailableSkillsForInspector(forceRefresh: true);
        ResolveSelectedSkill(true);
        RefreshAvailableWeapons(forceRefresh: true);
        ResolveEquippedWeapon(true);
        RecalculateStats();
        InitializeCurrentHpToMax();
        isInitialized = true;
    }

    /// <summary>CSV 경로 초기화 완료 후 호출. BattleSceneManager.Initialize와 중복 초기화를 막습니다.</summary>
    // MarkInitializedFromDataPipeline 구현은 Assets/ASB_Work partial 에서 제공합니다.

    private void ApplyStatusEffectStatModifiers()
    {
        Effects.ApplyStatusEffectStatModifiers(ref finalStats);
    }
    private void ApplyFormationPassiveModifiers()
    {
        // CounterRate는 현재 0~1 스케일을 사용하므로 +30%는 +0.3으로 적용합니다.
        if (IsInFrontRow)
        {
            finalStats.CounterRate += 0.3f;
        }
        else if (IsInBackRow)
        {
            finalStats.CriticalRate += 0.15f;
        }
    }

    private static void RefreshFormationPassiveStatsForAllUnits()
    {
        BattleCharactor[] units = FindObjectsByType<BattleCharactor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (units == null || units.Length == 0)
        {
            return;
        }

        for (int i = 0; i < units.Length; i++)
        {
            BattleCharactor unit = units[i];
            if (unit == null)
            {
                continue;
            }

            unit.RecalculateStats(applyCurrentHpClamp: true);
        }
    }


    public void Initialize(IUnitData data, TeamType team)
    {
        teamType = team;
        Initialize();
    }

    public void SetSelectedSkillIndex(int localIndex)
    {
        selectedSkillIndex = Mathf.Max(0, localIndex);
    }

    /// <summary>
    /// 전역 스킬 인덱스를 직접 지정합니다.
    /// 예: 적 인덱스 20001 + 슬롯1 => classSkillIndex=200011.
    /// </summary>
    public void SetClassSkillIndex(int globalSkillIndex)
    {
        classSkillIndex = Mathf.Max(0, globalSkillIndex);
    }

    public void BindPersistentSourceData(UnitPersistentData sourceData)
    {
        SourceData = sourceData;
    }

    public void BindPersistentEnemySourceData(EnemyUnitPersistentData sourceData)
    {
        SourceEnemyData = sourceData;
    }

    public void SetExperienceReward(float exp)
    {
        ExperienceReward = Mathf.Max(0f, exp);
    }

    public void SetUnitNameForSkillMatching(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        // TODO: 현재는 unitName/UnitData.Name 기반 임시 매칭
        // 추후 classKey / unitType 혹은 전용 식별자 컬럼 도입 시 이 부분 교체 예정
        unitName = name.Trim();
        InvalidatePrototypeSkillWeaponCache();
        RefreshAvailableSkillsForInspector(forceRefresh: true);
        RefreshAvailableWeapons(forceRefresh: true);
    }

    /// <summary>[JC 260621] UI 표시용 히어로명 설정(스킬매칭 unitName과 별개). 비면 UnitName 폴백.</summary>
    public void SetDisplayName(string name)
    {
        displayName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    private void InvalidatePrototypeSkillWeaponCache()
    {
        prototypeSkillsCacheKey = null;
        prototypeWeaponsCacheKey = null;
    }

    private static string BuildPrototypeSkillCacheKey(string trimmedUnitName, int characterLevel)
    {
        return $"{trimmedUnitName}\u001f{Mathf.Max(1, characterLevel)}";
    }

    private static string BuildPrototypeSkillCacheKey(int classIndex, int characterLevel)
    {
        return $"class:{classIndex}\u001f{Mathf.Max(1, characterLevel)}";
    }

    private bool TryResolvePlayerClassIndex(string trimmedUnitName, out int classIndex)
    {
        classIndex = 0;

        if (SourceEnemyData != null)
        {
            return false;
        }

        if (SourceData != null &&
            !string.IsNullOrWhiteSpace(SourceData.UnitTemplateKey) &&
            int.TryParse(SourceData.UnitTemplateKey.Trim(), out classIndex))
        {
            return classIndex > 0;
        }

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog != null &&
            !string.IsNullOrWhiteSpace(trimmedUnitName) &&
            catalog.TryGetPlayerTemplate(trimmedUnitName, out UnitData template) &&
            template != null &&
            !string.IsNullOrWhiteSpace(template.Index) &&
            int.TryParse(template.Index.Trim(), out classIndex))
        {
            return classIndex > 0;
        }

        return false;
    }

    /// <summary>
    /// SkillManager(우선) 또는 skillDataLoader로 후보를 채웁니다. 매니저/로더가 없으면 목록을 덮어쓰지 않습니다.
    /// </summary>
    public void RefreshAvailableSkillsForInspector(bool forceRefresh = false)
    {
        if (availableSkills == null)
        {
            availableSkills = new List<SkillData>();
        }

        string trimmed = string.IsNullOrWhiteSpace(unitName) ? string.Empty : unitName.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            if (!hasLoggedEmptyUnitNameWarning)
            {
                Debug.LogWarning($"[BattleCharactor] unitName이 비어 있어 스킬 후보 갱신을 건너뜁니다: GameObject={name}");
                hasLoggedEmptyUnitNameWarning = true;
            }

            return;
        }

        hasLoggedEmptyUnitNameWarning = false;
        bool hasPlayerClassIndex = TryResolvePlayerClassIndex(trimmed, out int playerClassIndex);
        string cacheKey = hasPlayerClassIndex
            ? BuildPrototypeSkillCacheKey(playerClassIndex, level)
            : BuildPrototypeSkillCacheKey(trimmed, level);
        if (!forceRefresh && prototypeSkillsCacheKey == cacheKey && availableSkills.Count > 0)
        {
            return;
        }

        SkillManager skillManager = SkillManager.Instance;
        if (skillManager == null)
        {
            skillManager = UnityEngine.Object.FindObjectOfType<SkillManager>();
        }

        if (skillManager != null)
        {
            availableSkills.Clear();
            if (hasPlayerClassIndex)
            {
                availableSkills.AddRange(skillManager.GetAvailableSkillsForCharacter(playerClassIndex, level));
            }

            if (availableSkills.Count == 0)
            {
                availableSkills.AddRange(skillManager.GetAvailableSkillsForCharacter(trimmed, level));
            }

            prototypeSkillsCacheKey = cacheKey;
            return;
        }

        if (DHCsvTemplateCatalog.Instance != null)
        {
            availableSkills.Clear();
            if (hasPlayerClassIndex)
            {
                availableSkills.AddRange(DHCsvTemplateCatalog.Instance.GetAvailableSkillsByClassIndex(playerClassIndex, level));
            }

            if (availableSkills.Count == 0)
            {
                availableSkills.AddRange(DHCsvTemplateCatalog.Instance.GetSkillsByClass(trimmed));
            }

            prototypeSkillsCacheKey = cacheKey;
            return;
        }

        // 소스 없음: 기존 후보 유지 (빈 리스트로 덮어쓰지 않음)
    }

    /// <summary>WeaponManager를 통해 unitName과 weaponClass가 일치하는 무기만 후보로 채웁니다. 매니저가 없으면 목록을 덮어쓰지 않습니다.</summary>
    public void RefreshAvailableWeapons(bool forceRefresh = false)
    {
        if (availableWeapons == null)
        {
            availableWeapons = new List<WeaponData>();
        }

        string trimmed = string.IsNullOrWhiteSpace(unitName) ? string.Empty : unitName.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            if (!hasLoggedEmptyUnitNameWarning)
            {
                Debug.LogWarning($"[BattleCharactor] unitName이 비어 있어 무기 후보 갱신을 건너뜁니다: GameObject={name}");
                hasLoggedEmptyUnitNameWarning = true;
            }

            return;
        }

        if (!forceRefresh && prototypeWeaponsCacheKey == trimmed && availableWeapons.Count > 0)
        {
            return;
        }

        WeaponManager wm = WeaponManager.Instance;
        if (wm == null)
        {
            wm = UnityEngine.Object.FindObjectOfType<WeaponManager>();
        }

        if (wm == null)
        {
            return;
        }

        availableWeapons.Clear();
        availableWeapons.AddRange(wm.GetWeaponsForClass(trimmed));
        prototypeWeaponsCacheKey = trimmed;
    }

    public void ResolveEquippedWeapon(bool emitWarning = true)
    {
        if (availableWeapons != null && availableWeapons.Count > 0)
        {
            int localIndex = equippedWeaponIndex;
            if (localIndex < 0 || localIndex >= availableWeapons.Count)
            {
                localIndex = 0;
            }

            equippedWeaponIndex = localIndex;
            WeaponData chosen = availableWeapons[localIndex];
            if (chosen == null)
            {
                for (int i = 0; i < availableWeapons.Count; i++)
                {
                    if (availableWeapons[i] != null)
                    {
                        equippedWeaponIndex = i;
                        chosen = availableWeapons[i];
                        break;
                    }
                }
            }

            EquippedWeaponData = chosen;
            return;
        }

        equippedWeaponIndex = 0;
        EquippedWeaponData = null;
        //if (emitWarning)
        //{
        //    Debug.LogWarning($"[BattleCharactor] 장착 가능한 무기를 찾지 못했습니다: unit={name}, unitName={unitName}");
        //}
    }

    /// <summary>
    /// 인스펙터/에디터에서 무기 드롭다운 선택 시 호출. equippedWeaponIndex는 availableWeapons 내 로컬 인덱스입니다.
    /// </summary>
    public void SetEquippedWeaponIndex(int index)
    {
        equippedWeaponIndex = Mathf.Max(0, index);
        RefreshAvailableWeapons();
        ResolveEquippedWeapon(false);
        RecalculateStats(Application.isPlaying);
    }

    public void ResolveSelectedSkill(bool emitWarning = true)
    {
        if (SourceData != null && SourceData.CurrentSkillIndex > 0)
        {
            classSkillIndex = SourceData.CurrentSkillIndex;
        }
        else if (SourceEnemyData != null)
        {
            int persistentEnemySkillIndex = TryReadPersistentEnemySkillIndex(SourceEnemyData);
            if (persistentEnemySkillIndex > 0)
            {
                classSkillIndex = persistentEnemySkillIndex;
            }
        }

        if (availableSkills != null && availableSkills.Count > 0)
        {
            int localIndex = -1;

            // classSkillIndex(전역) 지정이 있으면 먼저 해당 skillIndex를 찾습니다.
            if (classSkillIndex > 0)
            {
                for (int i = 0; i < availableSkills.Count; i++)
                {
                    SkillData candidate = availableSkills[i];
                    if (candidate != null && candidate.skillIndex == classSkillIndex)
                    {
                        localIndex = i;
                        break;
                    }
                }
            }

            // 전역 인덱스가 없거나 매칭 실패 시 기존 로컬 인덱스로 폴백합니다.
            if (localIndex < 0)
            {
                localIndex = selectedSkillIndex;
            }

            if (localIndex < 0 || localIndex >= availableSkills.Count)
            {
                localIndex = 0;
            }

            selectedSkillIndex = localIndex;
            SkillData chosen = availableSkills[localIndex];
            if (chosen == null)
            {
                for (int i = 0; i < availableSkills.Count; i++)
                {
                    if (availableSkills[i] != null)
                    {
                        selectedSkillIndex = i;
                        chosen = availableSkills[i];
                        break;
                    }
                }
            }

            SelectedSkillData = chosen;
            return;
        }

        selectedSkillIndex = 0;
        SelectedSkillData = null;
        if (emitWarning)
        {
            Debug.LogWarning($"[BattleCharactor] 장착 가능한 스킬이 없습니다: unitName={unitName}");
        }
    }

    private static int TryReadPersistentEnemySkillIndex(EnemyUnitPersistentData persistentData)
    {
        if (persistentData == null)
        {
            return 0;
        }

        PropertyInfo skillProp = persistentData.GetType().GetProperty("CurrentSkillIndex", BindingFlags.Public | BindingFlags.Instance);
        if (skillProp != null && skillProp.GetValue(persistentData) is int propIndex && propIndex > 0)
        {
            return propIndex;
        }

        FieldInfo skillField =
            persistentData.GetType().GetField("currentSkillIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (skillField != null && skillField.GetValue(persistentData) is int fieldIndex && fieldIndex > 0)
        {
            return fieldIndex;
        }

        if (persistentData.UnitIndex > 0)
        {
            return (persistentData.UnitIndex * 10) + 1;
        }

        return 0;
    }
}
