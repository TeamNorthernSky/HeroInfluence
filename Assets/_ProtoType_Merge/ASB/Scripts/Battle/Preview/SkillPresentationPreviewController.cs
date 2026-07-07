using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using GridCellRef = ASB.Work.BattleGrid.GridCell;

/// <summary>
/// PreviewScene 전용 실행 샌드박스. BattleManager.ExecuteGridSkill을 그대로 태워서
/// 애니메이션·이펙트·사운드·투사체·대미지 팝업까지 실제 전투와 동일한 흐름으로 재생한다.
/// SkillPresentationEditorWindow(데이터 편집)와 책임을 분리하기 위해 이 컨트롤러만 PreviewScene에 둔다.
/// </summary>
public class SkillPresentationPreviewController : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleVisualDirector visualDirector;
    [SerializeField] private BattleCharactor previewActor;
    [SerializeField] private BattleCharactor previewTarget;
    [SerializeField] private int selectedSkillIndex;
    [SerializeField] private bool ignoreSkillCost = true;

    private bool _isPlaying;
    private Coroutine _playRoutine;
    private UnitSnapshot _actorSnapshot;
    private UnitSnapshot _targetSnapshot;

    public bool IsPlaying => _isPlaying;
    public string ActorName => previewActor != null ? previewActor.UnitName : "-";
    public string TargetName => previewTarget != null ? previewTarget.UnitName : "-";

    private void Awake()
    {
        if (battleManager == null)
        {
            battleManager = Object.FindFirstObjectByType<BattleManager>();
        }

        if (visualDirector == null)
        {
            visualDirector = Object.FindFirstObjectByType<BattleVisualDirector>();
        }

        if (battleManager == null)
        {
            Debug.LogWarning("[SkillPresentationPreviewController] battleManager를 찾지 못했습니다. 인스펙터에서 연결해주세요.");
        }

        if (visualDirector == null)
        {
            Debug.LogWarning("[SkillPresentationPreviewController] visualDirector를 찾지 못했습니다. 인스펙터에서 연결해주세요.");
        }

        if (previewActor == null)
        {
            Debug.LogWarning("[SkillPresentationPreviewController] previewActor가 비어 있습니다. 인스펙터에서 연결해주세요.");
        }

        if (previewTarget == null)
        {
            Debug.LogWarning("[SkillPresentationPreviewController] previewTarget이 비어 있습니다. 인스펙터에서 연결해주세요.");
        }

        if (DHCsvTemplateCatalog.Instance == null)
        {
            Debug.LogWarning("[SkillPresentationPreviewController] DHCsvTemplateCatalog.Instance가 아직 준비되지 않았습니다.");
        }
    }

    public void SetSelectedSkillIndex(int skillIndex)
    {
        selectedSkillIndex = skillIndex;
    }

    public void SetUnits(BattleCharactor actor, BattleCharactor target)
    {
        previewActor = actor;
        previewTarget = target;

        RestoreUnit(previewActor);
        RestoreUnit(previewTarget);

        _actorSnapshot = UnitSnapshot.Capture(previewActor);
        _targetSnapshot = UnitSnapshot.Capture(previewTarget);
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

        SkillPresentationData presentation = visualDirector != null ? visualDirector.GetPresentation(selectedSkillIndex) : null;
        if (presentation == null)
        {
            Debug.LogWarning($"[SkillPresentationPreviewController] skillIndex {selectedSkillIndex}에 대한 SkillPresentationData가 없습니다 (Catalog 연결/데이터 누락 확인 필요).");
        }

        Debug.Log(
            $"[SkillPresentationPreviewController] Play skillIndex={selectedSkillIndex}, skillName={source.skillName}, " +
            $"actor={previewActor.UnitName}, target={previewTarget.UnitName}, " +
            $"battleManager={(battleManager != null)}, visualDirector={(visualDirector != null)}, " +
            $"csvCatalog={(DHCsvTemplateCatalog.Instance != null)}");

        SkillData clone = CloneForPreview(source);
        if (ignoreSkillCost)
        {
            clone.IPCost = 0;
        }

        _isPlaying = true;
        _playRoutine = StartCoroutine(PlayRoutine(clone));
    }

    public void ResetPreview()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        _isPlaying = false;

        RestoreSnapshotOrUnit(_actorSnapshot, previewActor);
        RestoreSnapshotOrUnit(_targetSnapshot, previewTarget);

        // TODO: 남아있는 임시 VFX/투사체 정리는 이번 스코프에서 구현하지 않는다.
    }

    private IEnumerator PlayRoutine(SkillData clonedSkill)
    {
        try
        {
            RestoreUnit(previewActor);
            RestoreUnit(previewTarget);
            _actorSnapshot = UnitSnapshot.Capture(previewActor);
            _targetSnapshot = UnitSnapshot.Capture(previewTarget);

            bool executed = false;
            yield return battleManager.ExecuteGridSkill(previewActor, previewTarget, clonedSkill, success => executed = success);

            if (!executed)
            {
                Debug.LogWarning($"[SkillPresentationPreviewController] skillIndex {clonedSkill.skillIndex} 실행이 실패했습니다 (executed=false).");
            }

            // 정상 종료 시 위치/회전은 BattleManager의 EnqueueSkillReturn/ReturnToIdleAction이 복구하므로 여기서 별도 처리하지 않는다.
        }
        finally
        {
            RestoreSnapshotOrUnit(_actorSnapshot, previewActor);
            RestoreSnapshotOrUnit(_targetSnapshot, previewTarget);
            _isPlaying = false;
            _playRoutine = null;
        }
    }

    private static void RestoreUnit(BattleCharactor unit)
    {
        if (unit == null)
        {
            return;
        }

        if (unit.IsDead)
        {
            unit.Revive(1f);
        }

        ClearStatusEffects(unit);
        unit.InitializeCurrentHpToMax();
    }

    private static void RestoreSnapshotOrUnit(UnitSnapshot snapshot, BattleCharactor fallbackUnit)
    {
        if (snapshot != null)
        {
            snapshot.Restore();
            return;
        }

        RestoreUnit(fallbackUnit);
    }

    private static void ClearStatusEffects(BattleCharactor unit)
    {
        if (unit == null || unit.ActiveStatusEffects == null || unit.ActiveStatusEffects.Count == 0)
        {
            return;
        }

        List<StatusEffectType> effectTypes = new List<StatusEffectType>();
        for (int i = 0; i < unit.ActiveStatusEffects.Count; i++)
        {
            StatusEffectInstance effect = unit.ActiveStatusEffects[i];
            if (effect == null || effect.effectType == StatusEffectType.none)
            {
                continue;
            }

            effectTypes.Add(effect.effectType);
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

        private UnitSnapshot(BattleCharactor unit)
        {
            this.unit = unit;
            hp = unit.CurrentHp;
            influence = unit.CurrentInfluence;
            position = unit.transform.position;
            rotation = unit.transform.rotation;
            occupiedCell = unit.OccupiedCell;
            statusEffects = CloneStatusEffects(unit);
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
        }

        private static List<StatusEffectInstance> CloneStatusEffects(BattleCharactor source)
        {
            List<StatusEffectInstance> clones = new List<StatusEffectInstance>();
            if (source == null || source.ActiveStatusEffects == null)
            {
                return clones;
            }

            for (int i = 0; i < source.ActiveStatusEffects.Count; i++)
            {
                StatusEffectInstance effect = source.ActiveStatusEffects[i];
                if (effect == null || effect.effectType == StatusEffectType.none)
                {
                    continue;
                }

                clones.Add(CloneStatusEffect(effect));
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
            boundary = new List<int>(source.boundary),
            multiTargetCount = source.multiTargetCount,
            multiTargetType = source.multiTargetType,
            skillValue = source.skillValue,
            skillSubValue = source.skillSubValue,
            AnimationTrigger = source.AnimationTrigger,
            StateName = source.StateName,
            UseAnimEvent = source.UseAnimEvent,
            HitDelay = source.HitDelay,
            TotalDelay = source.TotalDelay,
            TargetAnimationTrigger = source.TargetAnimationTrigger,
        };
    }
}
