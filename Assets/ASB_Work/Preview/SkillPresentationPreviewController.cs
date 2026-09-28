using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

using GridCellRef = ASB.Work.BattleGrid.GridCell;

public enum PreviewEnemySkillKind
{
    ClassSkill,
    WeaponSkill
}

public sealed class PreviewEnemySkillOption
{
    public PreviewEnemySkillOption(PreviewEnemySkillKind kind, SkillData skill, string label)
    {
        Kind = kind;
        Skill = skill;
        Label = label;
    }

    public PreviewEnemySkillKind Kind { get; }
    public SkillData Skill { get; }
    public string Label { get; }
    public int SkillIndex => Skill != null ? Skill.skillIndex : 0;
}

/// <summary>
/// PreviewScene 전용 실행 샌드박스. BattleManager.ExecuteGridSkill을 그대로 태워서
/// 애니메이션·이펙트·사운드·투사체·대미지 팝업까지 실제 전투와 동일한 흐름으로 재생한다.
/// </summary>
public class SkillPresentationPreviewController : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleVisualDirector visualDirector;
    [SerializeField] private BattleCharactor previewActor;
    [SerializeField] private BattleCharactor previewTarget;
    [SerializeField] private BattleCharactor previewAllyTarget;
    [SerializeField] private int selectedSkillIndex;
    [SerializeField] private bool ignoreSkillCost = true;
    [FormerlySerializedAs("preventPreviewTargetDeath")]
    [SerializeField, Tooltip("프리뷰 중 시전자 반대 진영의 HP를 최소 1로 유지해 사망 처리와 모델 비활성화를 막습니다.")]
    private bool preventPreviewDeaths = true;

    private readonly List<BattleCharactor> _previewPlayers = new List<BattleCharactor>();
    private readonly List<BattleCharactor> _previewEnemies = new List<BattleCharactor>();
    private readonly Dictionary<BattleCharactor, UnitSnapshot> _unitSnapshots =
        new Dictionary<BattleCharactor, UnitSnapshot>();
    private readonly Dictionary<BattleCharactor, Action<float, float>> _deathGuardHandlers =
        new Dictionary<BattleCharactor, Action<float, float>>();

    private BattleCharactor _selectedEnemyActor;
    private BattleCharactor _selectedEnemyTarget;
    private PreviewEnemySkillKind _selectedEnemySkillKind;
    private int _selectedEnemySkillIndex;
    private bool _isPlaying;
    private Coroutine _playRoutine;
    private bool _isRestoringPreviewUnitHp;

    public bool IsPlaying => _isPlaying;
    public string ActorName => previewActor != null ? previewActor.UnitName : "-";
    public string TargetName => previewTarget != null ? previewTarget.UnitName : "-";
    public Transform PreviewActorTransform => previewActor != null ? previewActor.transform : null;
    public Transform PreviewTargetTransform => previewTarget != null ? previewTarget.transform : null;
    public IReadOnlyList<BattleCharactor> PreviewPlayers => _previewPlayers;
    public IReadOnlyList<BattleCharactor> PreviewEnemies => _previewEnemies;
    public BattleCharactor SelectedEnemyActor => _selectedEnemyActor;
    public BattleCharactor SelectedEnemyTarget => _selectedEnemyTarget;
    public PreviewEnemySkillKind SelectedEnemySkillKind => _selectedEnemySkillKind;
    public int SelectedEnemySkillIndex => _selectedEnemySkillIndex;

    private BattleCharactor AllyUnit => previewAllyTarget != null ? previewAllyTarget : previewTarget;
    public bool HasAllyTarget => AllyUnit != null;
    public string AllyTargetName => AllyUnit != null ? AllyUnit.UnitName : "-";
    public bool IsAllyTargetDead => AllyUnit != null && AllyUnit.IsDead;

    public bool CanPlaySelectedEnemySkill
    {
        get
        {
            SanitizeEnemySelections();
            return !_isPlaying &&
                   battleManager != null &&
                   _selectedEnemyActor != null &&
                   !_selectedEnemyActor.IsDead &&
                   _selectedEnemyTarget != null &&
                   !_selectedEnemyTarget.IsDead &&
                   FindSelectedEnemySkillOption() != null;
        }
    }

    private void Awake()
    {
        if (battleManager == null)
        {
            battleManager = FindFirstObjectByType<BattleManager>();
        }

        if (visualDirector == null)
        {
            visualDirector = FindFirstObjectByType<BattleVisualDirector>();
        }

        if (battleManager == null)
        {
            Debug.LogWarning("[SkillPresentationPreviewController] battleManager를 찾지 못했습니다. 인스펙터에서 연결해주세요.");
        }

        if (visualDirector == null)
        {
            Debug.LogWarning("[SkillPresentationPreviewController] visualDirector를 찾지 못했습니다. 인스펙터에서 연결해주세요.");
        }

        if (DHCsvTemplateCatalog.Instance == null)
        {
            Debug.LogWarning("[SkillPresentationPreviewController] DHCsvTemplateCatalog.Instance가 아직 준비되지 않았습니다.");
        }
    }

    private void OnDisable()
    {
        UnhookDeathGuards();
    }

    public void SetSelectedSkillIndex(int skillIndex)
    {
        selectedSkillIndex = skillIndex;
    }

    /// <summary>
    /// 기존 프리뷰/핫키 호출부 호환용 오버로드입니다.
    /// 이미 전체 로스터를 받은 상태라면 목록은 보존하고 기본 actor/target만 교체합니다.
    /// </summary>
    public void SetUnits(BattleCharactor actor, BattleCharactor target, BattleCharactor allyTarget = null)
    {
        previewActor = actor;
        previewTarget = target;
        previewAllyTarget = allyTarget != null ? allyTarget : FindFirstOtherPlayer(actor);

        PruneRoster(_previewPlayers);
        PruneRoster(_previewEnemies);
        AddUnique(_previewPlayers, actor);
        AddUnique(_previewPlayers, allyTarget);
        AddUnique(_previewEnemies, target);

        PrepareRosterState();
    }

    /// <summary>PreViewsScene에서 생성된 전체 플레이어/적 로스터를 등록합니다.</summary>
    public void SetUnits(
        BattleCharactor primaryPlayer,
        IReadOnlyList<BattleCharactor> players,
        IReadOnlyList<BattleCharactor> enemies)
    {
        _previewPlayers.Clear();
        _previewEnemies.Clear();
        CopyUnique(players, _previewPlayers);
        CopyUnique(enemies, _previewEnemies);
        AddUnique(_previewPlayers, primaryPlayer);

        previewActor = primaryPlayer != null
            ? primaryPlayer
            : FindFirstValid(_previewPlayers, requireAlive: false);
        previewTarget = FindFirstValid(_previewEnemies, requireAlive: false);
        previewAllyTarget = FindFirstOtherPlayer(previewActor);

        PrepareRosterState();
    }

    public void SetSelectedEnemyActor(BattleCharactor actor)
    {
        if (actor == null || !_previewEnemies.Contains(actor))
        {
            return;
        }

        _selectedEnemyActor = actor;
        SanitizeEnemySkillSelection();
    }

    public void SetSelectedEnemyTarget(BattleCharactor target)
    {
        if (target != null && _previewPlayers.Contains(target))
        {
            _selectedEnemyTarget = target;
        }
    }

    public void SetSelectedEnemySkill(PreviewEnemySkillKind kind, int skillIndex)
    {
        _selectedEnemySkillKind = kind;
        _selectedEnemySkillIndex = skillIndex;
        SanitizeEnemySkillSelection();
    }

    public List<PreviewEnemySkillOption> GetSelectedEnemySkillOptions()
    {
        return BuildEnemySkillOptions(_selectedEnemyActor);
    }

    public string GetPreviewUnitLabel(BattleCharactor unit)
    {
        if (unit == null)
        {
            return "Missing Unit";
        }

        string grid = unit.OccupiedCell != null ? unit.OccupiedCell.name : "No Grid";
        string state = unit.IsDead ? " (Dead)" : string.Empty;
        return $"{grid} | {unit.gameObject.name} | {unit.DisplayName}{state}";
    }

    public void KillAllyTarget()
    {
        BattleCharactor ally = AllyUnit;
        if (ally == null || ally.IsDead)
        {
            return;
        }
        ally.TakeDamage(ally.MaxHp * 2f);
    }

    public void ReviveAllyTarget()
    {
        BattleCharactor ally = AllyUnit;
        if (ally == null || !ally.IsDead)
        {
            return;
        }
        ally.Revive(1f);
    }

    public void PlaySelectedSkill()
    {
        if (_isPlaying)
        {
            return;
        }

        if (battleManager == null || previewActor == null || previewTarget == null)
        {
            Debug.LogWarning("[SkillPresentationPreviewController] battleManager/previewActor/previewTarget 중 비어있는 게 있어 실행을 막습니다.");
            return;
        }

        if (DHCsvTemplateCatalog.Instance == null)
        {
            Debug.LogWarning("[SkillPresentationPreviewController] DHCsvTemplateCatalog.Instance가 없어 실행을 막습니다.");
            return;
        }

        SkillData source = DHCsvTemplateCatalog.Instance.GetSkillTemplate(selectedSkillIndex);
        if (source == null)
        {
            Debug.LogWarning($"[SkillPresentationPreviewController] skillIndex {selectedSkillIndex}에 해당하는 SkillData를 찾지 못했습니다.");
            return;
        }

        BattleCharactor executionTarget = ResolveExecutionTarget(source);
        if (executionTarget == null)
        {
            Debug.LogWarning($"[SkillPresentationPreviewController] skillIndex {selectedSkillIndex} has no valid preview target.");
            return;
        }

        SkillData clone = CloneForPreview(source);
        BeginPreparedSkill(previewActor, executionTarget, clone, "Player");
    }

    public void PlaySelectedEnemySkill()
    {
        if (_isPlaying)
        {
            return;
        }

        SanitizeEnemySelections();
        PreviewEnemySkillOption option = FindSelectedEnemySkillOption();
        if (_selectedEnemyActor == null || _selectedEnemyTarget == null || option == null)
        {
            Debug.LogWarning("[SkillPresentationPreviewController] 적 공격자/스킬/대상 중 유효하지 않은 항목이 있어 실행을 막습니다.");
            return;
        }

        SkillData prepared = EnemySkillExecutionPreparer.Prepare(option.Skill);
        BeginPreparedSkill(_selectedEnemyActor, _selectedEnemyTarget, prepared, "Enemy");
    }

    public void ResetPreview()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        UnhookDeathGuards();
        _isPlaying = false;

        foreach (BattleCharactor unit in CollectPreviewUnits())
        {
            unit?.GetComponent<PresentationRuntimeContext>()?.Clear();
        }

        RestoreAllSnapshots();

        // TODO: 남아있는 임시 VFX/투사체 정리는 별도 프리뷰 인스턴스 추적 기능에서 처리한다.
    }

    private void BeginPreparedSkill(
        BattleCharactor actor,
        BattleCharactor target,
        SkillData preparedSkill,
        string sourceLabel)
    {
        if (_isPlaying)
        {
            return;
        }

        if (battleManager == null || actor == null || target == null || preparedSkill == null)
        {
            Debug.LogWarning($"[SkillPresentationPreviewController] {sourceLabel} 스킬 실행에 필요한 참조가 비어 있습니다.");
            return;
        }

        SkillPresentationData presentation = visualDirector != null
            ? visualDirector.GetPresentation(preparedSkill.skillIndex)
            : null;
        if (presentation == null)
        {
            Debug.LogWarning(
                $"[SkillPresentationPreviewController] skillIndex {preparedSkill.skillIndex}에 대한 " +
                "SkillPresentationData가 없습니다 (Catalog 연결/데이터 누락 확인 필요).");
        }

        if (ignoreSkillCost)
        {
            preparedSkill.IPCost = 0;
        }

        Debug.Log(
            $"[SkillPresentationPreviewController] Play source={sourceLabel}, " +
            $"skillIndex={preparedSkill.skillIndex}, skillName={preparedSkill.skillName}, " +
            $"actor={actor.UnitName}, target={target.UnitName}");

        _isPlaying = true;
        _playRoutine = StartCoroutine(PlayRoutine(actor, target, preparedSkill));
    }

    private IEnumerator PlayRoutine(BattleCharactor actor, BattleCharactor executionTarget, SkillData preparedSkill)
    {
        try
        {
            List<BattleCharactor> units = CollectPreviewUnits(actor, executionTarget);
            bool isRevive = preparedSkill.classSkillEffect == 2;
            bool keepTargetDead = isRevive && executionTarget != null && executionTarget.IsDead;

            for (int i = 0; i < units.Count; i++)
            {
                BattleCharactor unit = units[i];
                if (keepTargetDead && ReferenceEquals(unit, executionTarget))
                {
                    continue;
                }
                RestoreUnit(unit);
            }

            CaptureSnapshots(units);

            if (isRevive)
            {
                actor.HasUsedRevive = false;
                if (executionTarget != null && !executionTarget.IsDead)
                {
                    executionTarget.TakeDamage(executionTarget.MaxHp * 2f);
                }
            }

            HookDeathGuards(actor, units);

            bool executed = false;
            try
            {
                yield return battleManager.ExecuteGridSkill(
                    actor,
                    executionTarget,
                    preparedSkill,
                    success => executed = success);
            }
            finally
            {
                UnhookDeathGuards();
            }

            if (!executed)
            {
                Debug.LogWarning(
                    $"[SkillPresentationPreviewController] skillIndex {preparedSkill.skillIndex} 실행이 실패했습니다 (executed=false).");
            }
        }
        finally
        {
            UnhookDeathGuards();
            RestoreAllSnapshots();
            _isPlaying = false;
            _playRoutine = null;
        }
    }

    private BattleCharactor ResolveExecutionTarget(SkillData skill)
    {
        if (skill != null && (skill.classSkillEffect == 1 || skill.classSkillEffect == 2))
        {
            return previewAllyTarget != null ? previewAllyTarget : previewActor;
        }

        return previewTarget;
    }

    private void PrepareRosterState()
    {
        foreach (BattleCharactor unit in CollectPreviewUnits())
        {
            RestoreUnit(unit);
        }

        CaptureSnapshots(CollectPreviewUnits());
        SanitizeEnemySelections();
    }

    private void SanitizeEnemySelections()
    {
        PruneRoster(_previewPlayers);
        PruneRoster(_previewEnemies);

        if (_selectedEnemyActor == null || !_previewEnemies.Contains(_selectedEnemyActor) || _selectedEnemyActor.IsDead)
        {
            _selectedEnemyActor = FindFirstValid(_previewEnemies, requireAlive: true)
                                  ?? FindFirstValid(_previewEnemies, requireAlive: false);
        }

        if (_selectedEnemyTarget == null || !_previewPlayers.Contains(_selectedEnemyTarget) || _selectedEnemyTarget.IsDead)
        {
            _selectedEnemyTarget = FindFirstValid(_previewPlayers, requireAlive: true)
                                   ?? FindFirstValid(_previewPlayers, requireAlive: false);
        }

        SanitizeEnemySkillSelection();
    }

    private void SanitizeEnemySkillSelection()
    {
        List<PreviewEnemySkillOption> options = BuildEnemySkillOptions(_selectedEnemyActor);
        for (int i = 0; i < options.Count; i++)
        {
            PreviewEnemySkillOption option = options[i];
            if (option.Kind == _selectedEnemySkillKind && option.SkillIndex == _selectedEnemySkillIndex)
            {
                return;
            }
        }

        if (options.Count > 0)
        {
            _selectedEnemySkillKind = options[0].Kind;
            _selectedEnemySkillIndex = options[0].SkillIndex;
        }
        else
        {
            _selectedEnemySkillKind = PreviewEnemySkillKind.ClassSkill;
            _selectedEnemySkillIndex = 0;
        }
    }

    private PreviewEnemySkillOption FindSelectedEnemySkillOption()
    {
        List<PreviewEnemySkillOption> options = BuildEnemySkillOptions(_selectedEnemyActor);
        for (int i = 0; i < options.Count; i++)
        {
            PreviewEnemySkillOption option = options[i];
            if (option.Kind == _selectedEnemySkillKind && option.SkillIndex == _selectedEnemySkillIndex)
            {
                return option;
            }
        }
        return null;
    }

    private static List<PreviewEnemySkillOption> BuildEnemySkillOptions(BattleCharactor enemy)
    {
        var options = new List<PreviewEnemySkillOption>();
        if (enemy == null)
        {
            return options;
        }

        if (enemy.availableSkills != null)
        {
            for (int i = 0; i < enemy.availableSkills.Count; i++)
            {
                SkillData skill = enemy.availableSkills[i];
                if (skill == null)
                {
                    continue;
                }

                options.Add(new PreviewEnemySkillOption(
                    PreviewEnemySkillKind.ClassSkill,
                    skill,
                    $"[Class] {skill.skillIndex} | {skill.skillName}"));
            }
        }

        if (enemy.EquippedWeaponData != null)
        {
            SkillData weaponSkill = enemy.EquippedWeaponData.ToSkillData();
            if (weaponSkill != null)
            {
                options.Add(new PreviewEnemySkillOption(
                    PreviewEnemySkillKind.WeaponSkill,
                    weaponSkill,
                    $"[Weapon] {weaponSkill.skillIndex} | {weaponSkill.skillName}"));
            }
        }

        return options;
    }

    private void HookDeathGuards(BattleCharactor actor, List<BattleCharactor> units)
    {
        UnhookDeathGuards();
        if (!preventPreviewDeaths || actor == null || units == null)
        {
            return;
        }

        for (int i = 0; i < units.Count; i++)
        {
            BattleCharactor unit = units[i];
            if (unit == null || ReferenceEquals(unit, actor) || unit.TeamType == actor.TeamType)
            {
                continue;
            }

            BattleCharactor protectedUnit = unit;
            Action<float, float> handler = (currentHp, maxHp) =>
                KeepPreviewUnitAlive(protectedUnit, currentHp);
            protectedUnit.OnHpChanged += handler;
            _deathGuardHandlers[protectedUnit] = handler;
        }
    }

    private void UnhookDeathGuards()
    {
        foreach (KeyValuePair<BattleCharactor, Action<float, float>> pair in _deathGuardHandlers)
        {
            if (pair.Key != null)
            {
                pair.Key.OnHpChanged -= pair.Value;
            }
        }
        _deathGuardHandlers.Clear();
    }

    private void KeepPreviewUnitAlive(BattleCharactor unit, float currentHp)
    {
        if (_isRestoringPreviewUnitHp || unit == null || currentHp > 0f)
        {
            return;
        }

        _isRestoringPreviewUnitHp = true;
        unit.InitializeCurrentState(1f, unit.CurrentInfluence);
        _isRestoringPreviewUnitHp = false;
    }

    private void CaptureSnapshots(List<BattleCharactor> units)
    {
        _unitSnapshots.Clear();
        if (units == null)
        {
            return;
        }

        for (int i = 0; i < units.Count; i++)
        {
            BattleCharactor unit = units[i];
            if (unit != null)
            {
                _unitSnapshots[unit] = UnitSnapshot.Capture(unit);
            }
        }
    }

    private void RestoreAllSnapshots()
    {
        foreach (KeyValuePair<BattleCharactor, UnitSnapshot> pair in _unitSnapshots)
        {
            if (pair.Key != null && pair.Value != null)
            {
                pair.Value.Restore();
            }
        }
    }

    private List<BattleCharactor> CollectPreviewUnits(
        BattleCharactor additionalA = null,
        BattleCharactor additionalB = null)
    {
        var units = new List<BattleCharactor>();
        CopyUnique(_previewPlayers, units);
        CopyUnique(_previewEnemies, units);
        AddUnique(units, previewActor);
        AddUnique(units, previewTarget);
        AddUnique(units, previewAllyTarget);
        AddUnique(units, additionalA);
        AddUnique(units, additionalB);
        return units;
    }

    private BattleCharactor FindFirstOtherPlayer(BattleCharactor primary)
    {
        for (int i = 0; i < _previewPlayers.Count; i++)
        {
            BattleCharactor candidate = _previewPlayers[i];
            if (candidate != null && !ReferenceEquals(candidate, primary))
            {
                return candidate;
            }
        }
        return null;
    }

    private static BattleCharactor FindFirstValid(List<BattleCharactor> units, bool requireAlive)
    {
        if (units == null)
        {
            return null;
        }

        for (int i = 0; i < units.Count; i++)
        {
            BattleCharactor unit = units[i];
            if (unit != null && (!requireAlive || !unit.IsDead))
            {
                return unit;
            }
        }
        return null;
    }

    private static void PruneRoster(List<BattleCharactor> units)
    {
        if (units != null)
        {
            units.RemoveAll(unit => unit == null);
        }
    }

    private static void CopyUnique(IReadOnlyList<BattleCharactor> source, List<BattleCharactor> destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            AddUnique(destination, source[i]);
        }
    }

    private static void AddUnique(List<BattleCharactor> units, BattleCharactor unit)
    {
        if (units != null && unit != null && !units.Contains(unit))
        {
            units.Add(unit);
        }
    }

    private static void RestoreUnit(BattleCharactor unit)
    {
        if (unit == null)
        {
            return;
        }

        if (!unit.gameObject.activeSelf)
        {
            unit.gameObject.SetActive(true);
        }

        if (unit.IsDead)
        {
            unit.Revive(1f);
        }

        ClearStatusEffects(unit);
        unit.InitializeCurrentHpToMax();
    }

    private static void ClearStatusEffects(BattleCharactor unit)
    {
        if (unit == null || unit.ActiveStatusEffects == null || unit.ActiveStatusEffects.Count == 0)
        {
            return;
        }

        var effectTypes = new List<StatusEffectType>();
        for (int i = 0; i < unit.ActiveStatusEffects.Count; i++)
        {
            StatusEffectInstance effect = unit.ActiveStatusEffects[i];
            if (effect != null && effect.effectType != StatusEffectType.none)
            {
                effectTypes.Add(effect.effectType);
            }
        }

        for (int i = 0; i < effectTypes.Count; i++)
        {
            unit.RemoveStatusEffect(effectTypes[i]);
        }
    }

    private sealed class UnitSnapshot
    {
        private readonly BattleCharactor unit;
        private readonly float hp;
        private readonly float influence;
        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly GridCellRef occupiedCell;
        private readonly List<StatusEffectInstance> statusEffects;
        private readonly bool hasUsedRevive;

        private UnitSnapshot(BattleCharactor unit)
        {
            this.unit = unit;
            hp = unit.CurrentHp;
            influence = unit.CurrentInfluence;
            position = unit.transform.position;
            rotation = unit.transform.rotation;
            occupiedCell = unit.OccupiedCell;
            statusEffects = CloneStatusEffects(unit);
            hasUsedRevive = unit.HasUsedRevive;
        }

        public static UnitSnapshot Capture(BattleCharactor unit)
        {
            return unit != null ? new UnitSnapshot(unit) : null;
        }

        public void Restore()
        {
            if (unit == null)
            {
                return;
            }

            if (!unit.gameObject.activeSelf)
            {
                unit.gameObject.SetActive(true);
            }

            if (unit.IsDead && hp > 0f)
            {
                unit.Revive(1f);
            }

            unit.transform.SetPositionAndRotation(position, rotation);
            if (occupiedCell != null)
            {
                unit.AssignToCell(occupiedCell);
            }

            ClearStatusEffects(unit);
            for (int i = 0; i < statusEffects.Count; i++)
            {
                unit.ApplyStatusEffect(CloneStatusEffect(statusEffects[i]));
            }

            unit.InitializeCurrentState(hp, influence);
            unit.HasUsedRevive = hasUsedRevive;
        }

        private static List<StatusEffectInstance> CloneStatusEffects(BattleCharactor source)
        {
            var clones = new List<StatusEffectInstance>();
            if (source == null || source.ActiveStatusEffects == null)
            {
                return clones;
            }

            for (int i = 0; i < source.ActiveStatusEffects.Count; i++)
            {
                StatusEffectInstance effect = source.ActiveStatusEffects[i];
                if (effect != null && effect.effectType != StatusEffectType.none)
                {
                    clones.Add(CloneStatusEffect(effect));
                }
            }
            return clones;
        }

        private static StatusEffectInstance CloneStatusEffect(StatusEffectInstance source)
        {
            if (source == null)
            {
                return null;
            }

            return new StatusEffectInstance
            {
                effectType = source.effectType,
                category = source.category,
                value = source.value,
                remainingTurns = source.remainingTurns,
                source = source.source
            };
        }
    }

    private static SkillData CloneForPreview(SkillData source)
    {
        return new SkillData
        {
            skillIndex = source.skillIndex,
            skillKey = source.skillKey,
            category = source.category,
            slot = source.slot,
            skillClass = source.skillClass,
            acquireLevel = source.acquireLevel,
            skillName = source.skillName,
            description = source.description,
            IPCost = source.IPCost,
            classSkillEffect = source.classSkillEffect,
            classSkillRange = source.classSkillRange,
            EnemySkill1Range = source.EnemySkill1Range,
            EnemySkill2Range = source.EnemySkill2Range,
            classSkillRangeLine = source.classSkillRangeLine,
            classSkillTarget = source.classSkillTarget,
            boundary = source.boundary != null
                ? new List<int>(source.boundary)
                : new List<int>(),
            multiTargetCount = source.multiTargetCount,
            multiTargetType = source.multiTargetType,
            skillValue = source.skillValue,
            skillSubValue = source.skillSubValue,
            AnimationTrigger = source.AnimationTrigger,
            StateName = source.StateName,
            UseAnimEvent = source.UseAnimEvent,
            HitDelay = source.HitDelay,
            TotalDelay = source.TotalDelay,
            TargetAnimationTrigger = source.TargetAnimationTrigger
        };
    }
}
