using System;
using System.Collections.Generic;
using System.Linq;
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
    [HideInInspector] [SerializeField] private Skill activeSkill;
    [SerializeField] private SkillDataLoader skillDataLoader;
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
    public bool IsStunned
    {
        get
        {
            if (IsDead || activeStatusEffects == null)
            {
                return false;
            }

            return activeStatusEffects.Any(e =>
                e != null &&
                e.effectType == StatusEffectType.stun &&
                e.remainingTurns > 0);
        }
    }
    public bool IsHealBanned
    {
        get
        {
            if (IsDead || activeStatusEffects == null)
            {
                return false;
            }

            for (int i = 0; i < activeStatusEffects.Count; i++)
            {
                StatusEffectInstance effect = activeStatusEffects[i];
                if (effect == null)
                {
                    continue;
                }

                if (effect.effectType == StatusEffectType.healBan && effect.remainingTurns > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public IUnitData UnitData => null;

    /// <summary>
    /// 딕셔너리/Outline 등 런타임 조회용 고유 ID. 스킬 CSV 매칭용 <see cref="unitName"/>에는 포함되지 않습니다.
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

    /// <summary>표시·로그용. 스킬/무기 매칭 원본(접미사 없음).</summary>
    public string UnitName => string.IsNullOrWhiteSpace(unitName) ? gameObject.name : unitName;

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
    public float MaxInfluence => FinalStats.Influence;
    public IReadOnlyList<StatusEffectInstance> ActiveStatusEffects => activeStatusEffects;

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
            cell = ASB.Work.BattleGrid.GridManager.Instance?.FindCellByUnit(this);
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
        if (effect == null || effect.effectType == StatusEffectType.none)
        {
            return;
        }

        if (activeStatusEffects == null)
        {
            activeStatusEffects = new List<StatusEffectInstance>();
        }

        StatusEffectInstance existing = activeStatusEffects.Find(x => x != null && x.effectType == effect.effectType);
        if (existing != null)
        {
            existing.category = effect.category;
            existing.value = effect.value;
            existing.remainingTurns = Mathf.Max(1, effect.remainingTurns);
            existing.source = effect.source;
            Debug.Log($"[Status] 갱신: {UnitName} effect={effect.effectType} turns={existing.remainingTurns}");
        }
        else
        {
            activeStatusEffects.Add(new StatusEffectInstance
            {
                effectType = effect.effectType,
                category = effect.category,
                value = effect.value,
                remainingTurns = Mathf.Max(1, effect.remainingTurns),
                source = effect.source
            });
            Debug.Log($"[Status] 적용: {UnitName} effect={effect.effectType} turns={Mathf.Max(1, effect.remainingTurns)}");
        }

        if (RequiresStatRecalculation(effect.effectType))
        {
            RecalculateStats(applyCurrentHpClamp: true);
        }
    }

    public void RemoveStatusEffect(StatusEffectType effectType)
    {
        if (activeStatusEffects == null || activeStatusEffects.Count == 0)
        {
            return;
        }

        bool removed = activeStatusEffects.RemoveAll(x => x != null && x.effectType == effectType) > 0;
        if (removed && RequiresStatRecalculation(effectType))
        {
            RecalculateStats(applyCurrentHpClamp: true);
        }
    }

    public bool HasStatusEffect(StatusEffectType effectType)
    {
        if (activeStatusEffects == null || activeStatusEffects.Count == 0)
        {
            return false;
        }

        return activeStatusEffects.Exists(x => x != null && x.effectType == effectType);
    }

    public BattleCharactor GetTauntSource()
    {
        if (activeStatusEffects == null || activeStatusEffects.Count == 0)
        {
            return null;
        }

        for (int i = activeStatusEffects.Count - 1; i >= 0; i--)
        {
            StatusEffectInstance effect = activeStatusEffects[i];
            if (effect == null || effect.effectType != StatusEffectType.taunt)
            {
                continue;
            }

            if (effect.remainingTurns > 0 && effect.source != null && !effect.source.IsDead)
            {
                return effect.source;
            }

            activeStatusEffects.RemoveAt(i);
        }

        return null;
    }

    public void ProcessTurnStartStatusEffects()
    {
        if (IsDead || activeStatusEffects == null || activeStatusEffects.Count == 0)
        {
            return;
        }

        for (int i = 0; i < activeStatusEffects.Count; i++)
        {
            StatusEffectInstance effect = activeStatusEffects[i];
            if (effect == null || effect.remainingTurns <= 0)
            {
                continue;
            }

            if (effect.effectType != StatusEffectType.poison && effect.effectType != StatusEffectType.bleed)
            {
                continue;
            }

            float dotDamage = Mathf.Max(0f, effect.value);
            if (dotDamage <= 0f)
            {
                continue;
            }

            float before = currentHp;
            TakeDamage(dotDamage);
            float applied = Mathf.Max(0f, before - currentHp);
            Debug.Log($"[Status] DOT tick: {UnitName} effect={effect.effectType} dmg={applied:F1} turnsLeft={effect.remainingTurns}");

            if (IsDead)
            {
                return;
            }
        }
    }

    public void AdvanceStatusEffectDuration()
    {
        if (activeStatusEffects == null || activeStatusEffects.Count == 0)
        {
            return;
        }

        for (int i = 0; i < activeStatusEffects.Count; i++)
        {
            StatusEffectInstance effect = activeStatusEffects[i];
            if (effect == null)
            {
                continue;
            }

            effect.remainingTurns -= 1;
        }

        bool removedAny = false;
        bool removedStatModifier = false;
        for (int i = activeStatusEffects.Count - 1; i >= 0; i--)
        {
            StatusEffectInstance effect = activeStatusEffects[i];
            if (effect == null || effect.remainingTurns > 0)
            {
                continue;
            }

            if (effect != null && RequiresStatRecalculation(effect.effectType))
            {
                removedStatModifier = true;
            }

            activeStatusEffects.RemoveAt(i);
            removedAny = true;
        }

        if (removedAny && removedStatModifier)
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

        var outlines = GetComponentsInChildren<Outline>(true);
        for (int i = 0; i < outlines.Length; i++)
        {
            if (outlines[i] != null)
            {
                outlines[i].enabled = false;
                outlines[i].OutlineMode = Outline.Mode.OutlineHidden;
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
        EnableVisuals();

        var animator = GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isActiveAndEnabled)
        {
            animator.SetTrigger("Revive");
        }

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

        var outlines = GetComponentsInChildren<Outline>(true);
        for (int i = 0; i < outlines.Length; i++)
        {
            if (outlines[i] != null)
            {
                outlines[i].enabled = true;
                outlines[i].OutlineMode = Outline.Mode.OutlineHidden;
            }
        }
    }

    public void ReviveToFull()
    {
        IsDead = false;
        InitializeCurrentHpToMax();
    }

    public void AssignToCell(GridCellRef cell)
    {
        if (occupiedCell == cell)
        {
            if (cell != null)
            {
                cell.SetOccupyingUnit(this);
                ASB.Work.BattleGrid.GridManager.Instance?.RegisterUnitToCell(this, cell);
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
            ASB.Work.BattleGrid.GridManager.Instance?.UnregisterUnit(this);
            occupiedCell.ClearIfOccupying(this);
        }

        occupiedCell = cell;
        if (occupiedCell != null)
        {
            occupiedCell.SetOccupyingUnit(this);
            ASB.Work.BattleGrid.GridManager.Instance?.RegisterUnitToCell(this, occupiedCell);
        }

        RefreshFormationPassiveStatsForAllUnits();
    }

    public void SyncOccupancy(GridCellRef cell)
    {
        AssignToCell(cell);
    }

    public void ClearOccupiedCell()
    {
        ASB.Work.BattleGrid.GridManager.Instance?.UnregisterUnit(this);
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
        if (activeStatusEffects == null || activeStatusEffects.Count == 0)
        {
            return;
        }

        float atkMultiplier = 1f;
        float defMultiplier = 1f;

        for (int i = 0; i < activeStatusEffects.Count; i++)
        {
            StatusEffectInstance effect = activeStatusEffects[i];
            if (effect == null || effect.remainingTurns <= 0)
            {
                continue;
            }

            float amount = Mathf.Max(0f, effect.value);
            switch (effect.effectType)
            {
                case StatusEffectType.attack_up:
                    atkMultiplier += amount;
                    break;
                case StatusEffectType.attack_down:
                    atkMultiplier -= amount;
                    break;
                case StatusEffectType.defense_up:
                    defMultiplier += amount;
                    break;
                case StatusEffectType.defense_down:
                    defMultiplier -= amount;
                    break;
            }
        }

        atkMultiplier = Mathf.Max(0.1f, atkMultiplier);
        defMultiplier = Mathf.Max(0.1f, defMultiplier);

        finalStats.Atk = Mathf.Max(1f, finalStats.Atk * atkMultiplier);
        finalStats.DEF = Mathf.Max(0f, finalStats.DEF * defMultiplier);
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

    private static bool RequiresStatRecalculation(StatusEffectType effectType)
    {
        if (effectType == StatusEffectType.healBan)
        {
            return false;
        }

        return effectType == StatusEffectType.attack_up
               || effectType == StatusEffectType.attack_down
               || effectType == StatusEffectType.defense_up
               || effectType == StatusEffectType.defense_down;
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

    private void InvalidatePrototypeSkillWeaponCache()
    {
        prototypeSkillsCacheKey = null;
        prototypeWeaponsCacheKey = null;
    }

    private static string BuildPrototypeSkillCacheKey(string trimmedUnitName, int characterLevel)
    {
        return $"{trimmedUnitName}\u001f{Mathf.Max(1, characterLevel)}";
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
        string cacheKey = BuildPrototypeSkillCacheKey(trimmed, level);
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
            availableSkills.AddRange(skillManager.GetAvailableSkillsForCharacter(trimmed, level));
            prototypeSkillsCacheKey = cacheKey;
            return;
        }

        if (skillDataLoader != null)
        {
            availableSkills.Clear();
            availableSkills.AddRange(skillDataLoader.GetSkillsByClass(trimmed));
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
