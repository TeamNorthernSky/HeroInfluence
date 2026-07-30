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
    [SerializeField] private BattleCharactor previewAllyTarget;
    [SerializeField] private int selectedSkillIndex;
    [SerializeField] private bool ignoreSkillCost = true;
    [SerializeField, Tooltip("프리뷰 중 적 타깃의 HP를 최소 1로 유지해 사망 처리와 모델 비활성화를 막습니다.")]
    private bool preventPreviewTargetDeath = true;

    private bool _isPlaying;
    private Coroutine _playRoutine;
    private bool _isRestoringPreviewEnemyHp;
    private UnitSnapshot _actorSnapshot;
    private UnitSnapshot _targetSnapshot;
    private UnitSnapshot _allyTargetSnapshot;

    public bool IsPlaying => _isPlaying;
    public string ActorName => previewActor != null ? previewActor.UnitName : "-";
    public string TargetName => previewTarget != null ? previewTarget.UnitName : "-";
            // Scene-view path editor uses these only as preview anchors; runtime battle code never depends on this controller.
            public Transform PreviewActorTransform => previewActor != null ? previewActor.transform : null;
            public Transform PreviewTargetTransform => previewTarget != null ? previewTarget.transform : null;

    // ── 아군(부활 대상) 수동 제어: 부활 스킬 미리보기용. previewAllyTarget 우선, 없으면 previewTarget. ──
    private BattleCharactor AllyUnit => previewAllyTarget != null ? previewAllyTarget : previewTarget;
    public bool HasAllyTarget => AllyUnit != null;
    public string AllyTargetName => AllyUnit != null ? AllyUnit.UnitName : "-";
    public bool IsAllyTargetDead => AllyUnit != null && AllyUnit.IsDead;

    /// <summary>미리보기에서 아군(부활 대상)을 쓰러뜨린다. 부활 스킬 연출 확인용.</summary>
    public void KillAllyTarget()
    {
        BattleCharactor ally = AllyUnit;
        if (ally == null || ally.IsDead)
        {
            return;
        }
        ally.TakeDamage(ally.MaxHp * 2f);
    }

    /// <summary>미리보기에서 아군(부활 대상)을 풀피로 되살린다.</summary>
    public void ReviveAllyTarget()
    {
        BattleCharactor ally = AllyUnit;
        if (ally == null || !ally.IsDead)
        {
            return;
        }
        ally.Revive(1f);
    }

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

    public void SetUnits(BattleCharactor actor, BattleCharactor target, BattleCharactor allyTarget = null)
    {
        previewActor = actor;
        previewTarget = target;
        previewAllyTarget = allyTarget;

        RestoreUnit(previewActor);
        RestoreUnit(previewTarget);
        RestoreUnit(previewAllyTarget);

        _actorSnapshot = UnitSnapshot.Capture(previewActor);
        _targetSnapshot = UnitSnapshot.Capture(previewTarget);
        _allyTargetSnapshot = UnitSnapshot.Capture(previewAllyTarget);
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
        SkillPresentationData presentation = visualDirector != null ? visualDirector.GetPresentation(selectedSkillIndex) : null;
        if (presentation == null)
        {
            Debug.LogWarning($"[SkillPresentationPreviewController] skillIndex {selectedSkillIndex}에 대한 SkillPresentationData가 없습니다 (Catalog 연결/데이터 누락 확인 필요).");
        }

        Debug.Log(
            $"[SkillPresentationPreviewController] Play skillIndex={selectedSkillIndex}, skillName={source.skillName}, " +
            $"actor={previewActor.UnitName}, target={executionTarget.UnitName}, " +
            $"battleManager={(battleManager != null)}, visualDirector={(visualDirector != null)}, " +
            $"csvCatalog={(DHCsvTemplateCatalog.Instance != null)}");

        SkillData clone = CloneForPreview(source);
        if (ignoreSkillCost)
        {
            clone.IPCost = 0;
        }

        _isPlaying = true;
        _playRoutine = StartCoroutine(PlayRoutine(clone, executionTarget));
    }

    public void ResetPreview()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        _isPlaying = false;

        previewActor?.GetComponent<PresentationRuntimeContext>()?.Clear();
        previewTarget?.GetComponent<PresentationRuntimeContext>()?.Clear();
        previewAllyTarget?.GetComponent<PresentationRuntimeContext>()?.Clear();

        RestoreSnapshotOrUnit(_actorSnapshot, previewActor);
        RestoreSnapshotOrUnit(_targetSnapshot, previewTarget);
        RestoreSnapshotOrUnit(_allyTargetSnapshot, previewAllyTarget);

        // TODO: 남아있는 임시 VFX/투사체 정리는 이번 스코프에서 구현하지 않는다.
    }

    private IEnumerator PlayRoutine(SkillData clonedSkill, BattleCharactor executionTarget)
    {
        try
        {
            // 부활 스킬(ClassSkillEffect=2)이고 대상이 '이미' 죽어있으면, 복구(되살리기)하지 않고 그대로 둔다.
            //  → 안 그러면 RestoreUnit이 되살렸다가 아래에서 다시 쓰러뜨려 '죽는 모션'이 한 번 더 재생된다.
            bool isRevive = clonedSkill != null && clonedSkill.classSkillEffect == 2;
            bool keepTargetDead = isRevive && executionTarget != null && executionTarget.IsDead;

            if (!(keepTargetDead && ReferenceEquals(previewActor, executionTarget)))
            {
                RestoreUnit(previewActor);
            }
            if (!(keepTargetDead && ReferenceEquals(previewTarget, executionTarget)))
            {
                RestoreUnit(previewTarget);
            }
            if (!(keepTargetDead && ReferenceEquals(previewAllyTarget, executionTarget)))
            {
                RestoreUnit(previewAllyTarget);
            }

            _actorSnapshot = UnitSnapshot.Capture(previewActor);
            _targetSnapshot = UnitSnapshot.Capture(previewTarget);
            _allyTargetSnapshot = UnitSnapshot.Capture(previewAllyTarget);

            // 부활 스킬 미리보기 보정:
            //  - 부활은 전투당 1회 제한이므로 매 재생마다 시전자 플래그를 리셋(반복 재생 가능).
            //  - ExecuteGridSkill 가드가 '죽은 대상'을 요구하므로, 대상이 '살아있을 때만' 조용히 쓰러뜨린다.
            //    (이미 죽어있으면 위에서 복구를 건너뛰었으므로 그대로 부활 연출만 재생된다)
            if (isRevive)
            {
                if (previewActor != null)
                {
                    previewActor.HasUsedRevive = false;
                }
                if (executionTarget != null && !executionTarget.IsDead)
                {
                    executionTarget.TakeDamage(executionTarget.MaxHp * 2f);
                }
            }

            bool protectEnemiesFromDeath =
                preventPreviewTargetDeath &&
                ReferenceEquals(executionTarget, previewTarget);

            var deathGuardHandlers = new System.Collections.Generic.Dictionary<BattleCharactor, System.Action<float, float>>();
            if (protectEnemiesFromDeath)
            {
                BattleCharactor[] sceneUnits = Object.FindObjectsByType<BattleCharactor>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                for (int i = 0; i < sceneUnits.Length; i++)
                {
                    BattleCharactor enemy = sceneUnits[i];
                    if (enemy == null || enemy.TeamType == TeamType.Player)
                    {
                        continue;
                    }

                    BattleCharactor protectedEnemy = enemy;
                    System.Action<float, float> handler = (currentHp, maxHp) =>
                        KeepPreviewEnemyAlive(protectedEnemy, currentHp);
                    protectedEnemy.OnHpChanged += handler;
                    deathGuardHandlers.Add(protectedEnemy, handler);
                }
            }

            bool executed = false;
            try
            {
                yield return battleManager.ExecuteGridSkill(previewActor, executionTarget, clonedSkill, success => executed = success);
            }
            finally
            {
                foreach (var pair in deathGuardHandlers)
                {
                    if (pair.Key != null)
                    {
                        pair.Key.OnHpChanged -= pair.Value;
                    }
                }
            }

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
        RestoreSnapshotOrUnit(_allyTargetSnapshot, previewAllyTarget);
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
    private void KeepPreviewEnemyAlive(BattleCharactor enemy, float currentHp)
    {
        if (_isRestoringPreviewEnemyHp || enemy == null || currentHp > 0f)
        {
            return;
        }

        _isRestoringPreviewEnemyHp = true;
        enemy.InitializeCurrentState(1f, enemy.CurrentInfluence);
        _isRestoringPreviewEnemyHp = false;
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
