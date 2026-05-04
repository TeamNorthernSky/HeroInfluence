using System;
using System.Collections.Generic;
using UnityEngine;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

/// <summary>
/// 전투 유닛: 원본(base) 스탯 → StatCalculator → 최종(final) 스탯 → 런타임 HP.
/// - 프로토타입: 인스펙터 maxHp/attack/defense/speed로 내부 base 구성(BuildBaseStatsFromInspector).
/// - CSV: SetBaseStats(UnitData.baseStats) 후 RecalculateStats.
/// Combat tuning(level, unitCount, 가중치)의 최종 소유는 이 컴포넌트만 담당합니다.
/// </summary>
public class BattleCharactor : MonoBehaviour, IUnitIdentifier
{
    public event Action<BattleCharactor> OnDied;

    [Header("Identity")]
    [SerializeField] private string unitName = "Unit";
    [SerializeField] private TeamType teamType = TeamType.Player;

    [Header("Prototype inspector source (CSV 미주입 시 baseStats 원본)")]
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private float attack = 10f;
    [SerializeField] private float defense = 0f;
    [SerializeField] private float speed = 1f;

    [Header("Combat tuning (StatCalculator 가중치 · 최종 소유)")]
    [SerializeField] private int level = 1;
    [SerializeField] private int unitCount = 1;
    [SerializeField] private StatWeights unitWeight = new StatWeights(1f, 1f, 1f);
    [SerializeField] private StatWeights levelWeight = new StatWeights(1f, 1f, 1f);
    [Tooltip("인스펙터 튜닝값. 추후 클래스 데이터 시스템과 연결 시 교체 예정.")]
    [SerializeField] private StatWeights classWeight = new StatWeights(1f, 1f, 1f);

    [Header("Computed / runtime (에디터 미리보기 = finalStats)")]
    [Tooltip("StatCalculator 결과. 인스펙터에서 결과 확인용.")]
    [SerializeField] private StatBlock finalStats;
    [SerializeField] private float currentHp;

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

    /// <summary>계산 원본. SetBaseStats 또는 BuildBaseStatsFromInspector로만 갱신.</summary>
    private StatBlock runtimeBaseStats;

    /// <summary>
    /// 런타임 전용(직렬화 안 함). SetBaseStats로 CSV base가 주입되면 true.
    /// 에디터 재로드 시 false로 돌아가므로 OnValidate는 인스펙터 기준 미리보기에 사용합니다.
    /// </summary>
    private bool hasExternalBaseStats;

    private GridCellRef occupiedCell;
    private bool isInitialized;
    private bool hasLoggedEmptyUnitNameWarning;

    /// <summary>프로토타입 경로: SkillManager에서 가져온 후보 목록의 캐시 키(unitName|level).</summary>
    private string prototypeSkillsCacheKey;

    /// <summary>프로토타입 경로: WeaponManager에서 가져온 후보 목록의 캐시 키(unitName).</summary>
    private string prototypeWeaponsCacheKey;

    public List<EquipmentData> EquippedEquipments { get; private set; }

    public List<SkillDataAsset> EquippedSkills { get; private set; }

    public int ClassSkillIndex => classSkillIndex;

    public bool IsPlayer { get; set; }

    public bool IsDead { get; private set; }

    public IUnitData UnitData => null;
    public string UnitId => string.IsNullOrWhiteSpace(unitName) || unitName == "Unit" ? gameObject.name : unitName;
    public string UnitName => string.IsNullOrWhiteSpace(unitName) ? gameObject.name : unitName;
    public TeamType TeamType => teamType;
    public string UnitID => UnitId;
    public int Level => level;

    public StatBlock FinalStats => finalStats;

    public float CurrentHp => currentHp;
    public float MaxHp => finalStats.HP;
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
    public bool IsInFrontRow => occupiedCell != null && occupiedCell.IsFrontRow;

    /// <summary>디버그용: 현재 계산에 쓰는 base 원본.</summary>
    public StatBlock RuntimeBaseStats => runtimeBaseStats;

    private void Awake()
    {
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
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
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
        ClampPrototypeScalars();
        level = Mathf.Max(1, level);
        unitCount = Mathf.Max(1, unitCount);

        if (!hasExternalBaseStats)
        {
            BuildBaseStatsFromInspector();
        }

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
        hasExternalBaseStats = true;
    }

    /// <summary>SetBaseStats가 한 번도 호출되지 않았을 때만, 인스펙터 스칼라로 runtime base를 구성합니다.</summary>
    public void BuildBaseStatsFromInspector()
    {
        if (hasExternalBaseStats)
        {
            return;
        }

        ClampPrototypeScalars();
        runtimeBaseStats = new StatBlock(
            hp: maxHp,
            atk: attack,
            def: defense,
            luck: 0f,
            speed: speed,
            criticalRate: 0f,
            counterRate: 0f,
            avoidRate: 0f);
    }

    /// <summary>CSV 초기화 시에만 외부에서 호출. 인스펙터 tuning의 최종 값으로 반영합니다.</summary>
    public void ApplyCombatTuning(int lv, int count, StatWeights uw, StatWeights lw, StatWeights cw)
    {
        level = Mathf.Max(1, lv);
        unitCount = Mathf.Max(1, count);
        unitWeight = uw;
        levelWeight = lw;
        classWeight = cw;
    }

    /// <summary>StatCalculator로 finalStats 갱신. applyCurrentHpClamp가 true일 때만 currentHp 상한을 맞춥니다.</summary>
    public void RecalculateStats(bool applyCurrentHpClamp = true)
    {
        if (!hasExternalBaseStats)
        {
            BuildBaseStatsFromInspector();
        }

        finalStats = StatCalculator.CalculateFinalStats(
            runtimeBaseStats,
            level,
            unitCount,
            unitWeight,
            levelWeight,
            classWeight,
            EquippedEquipments,
            EquippedWeaponData != null ? EquippedWeaponData.GetBonusStatBlock() : default);

        ApplyStatusEffectStatModifiers();

        if (applyCurrentHpClamp)
        {
            currentHp = Mathf.Clamp(currentHp, 0f, finalStats.HP);
        }
    }

    /// <summary>초기화 시점에만 HP를 최대(= final HP)로 맞춥니다.</summary>
    public void InitializeCurrentHpToMax()
    {
        currentHp = finalStats.HP;
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

        float heal = Mathf.Max(0f, amount);
        if (heal <= 0f)
        {
            return;
        }

        float before = currentHp;
        currentHp = Mathf.Clamp(currentHp + heal, 0f, finalStats.HP);
        float delta = currentHp - before;
        if (delta > 0f)
        {
            Debug.Log($"[Battle] Heal: {UnitName} +{delta:F1} (HP {before:F1}->{currentHp:F1}/{finalStats.HP:F1})");
        }
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
        var renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }

        var colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        var colliders2D = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders2D.Length; i++)
        {
            if (colliders2D[i] != null)
            {
                colliders2D[i].enabled = false;
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
        var renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = true;
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
            occupiedCell.ClearIfOccupying(this);
        }

        occupiedCell = cell;
        if (occupiedCell != null)
        {
            occupiedCell.SetOccupyingUnit(this);
        }
    }

    public void SyncOccupancy(GridCellRef cell)
    {
        AssignToCell(cell);
    }

    public void ClearOccupiedCell()
    {
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

        ClampPrototypeScalars();
        IsPlayer = teamType == TeamType.Player;
        IsDead = false;
        if (!hasExternalBaseStats)
        {
            BuildBaseStatsFromInspector();
        }

        RefreshAvailableSkillsForInspector(forceRefresh: true);
        ResolveSelectedSkill(true);
        RefreshAvailableWeapons(forceRefresh: true);
        ResolveEquippedWeapon(true);
        RecalculateStats();
        InitializeCurrentHpToMax();
        isInitialized = true;
    }

    /// <summary>CSV 경로 초기화 완료 후 호출. BattleSceneManager.Initialize와 중복 초기화를 막습니다.</summary>
    public void MarkInitializedFromDataPipeline()
    {
        RefreshAvailableSkillsForInspector(forceRefresh: true);
        ResolveSelectedSkill(true);
        RefreshAvailableWeapons(forceRefresh: true);
        ResolveEquippedWeapon(true);
        isInitialized = true;
    }

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

    private static bool RequiresStatRecalculation(StatusEffectType effectType)
    {
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

    private void ClampPrototypeScalars()
    {
        maxHp = Mathf.Max(1f, maxHp);
        attack = Mathf.Max(1f, attack);
        defense = Mathf.Max(0f, defense);
        speed = Mathf.Max(0f, speed);
    }

    public void SetSelectedSkillIndex(int localIndex)
    {
        selectedSkillIndex = Mathf.Max(0, localIndex);
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
        if (emitWarning)
        {
            Debug.LogWarning($"[BattleCharactor] 장착 가능한 무기를 찾지 못했습니다: unit={name}, unitName={unitName}");
        }
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
        if (availableSkills != null && availableSkills.Count > 0)
        {
            int localIndex = selectedSkillIndex;
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
}
