using System;
using System.Collections.Generic;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;
using ASBGridManager = ASB.Work.BattleGrid.GridManager;
using UnityEngine;
using UnityEngine.EventSystems;
using ASB.Work.BattleGrid;
using ASB.Work.Battle.SkillExecution;
public enum PlayerActionState
{
    Idle,
    WaitingForTarget
}

public enum PendingActionType
{
    None,
    BasicAttack,
    ClassSkill,
    WeaponSkill
}

/// <summary>
/// 적 타겟 선택(클릭), 타겟 아웃라인, 숫자키로 BattleManager 실행 요청. 데미지 계산은 하지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public class InputHandler : MonoBehaviour
{
    public static event Action<BattleCharactor, BattleCharactor> PlayerSkillActionResolved;
    public event Action<string> OnActionSelected;

    [Header("Raycast")]
    [SerializeField] private Camera raycastCamera;
    [SerializeField] private float maxRayDistance = 200f;

    [Header("Target selection")]
    [Tooltip("PlayerGrid, EnemyGrid, Player, Enemy 레이어를 포함한 마스크.")]
    [SerializeField] private LayerMask selectionRaycastMask = ~0;

    [SerializeField] private BattleFlowManager battleFlowManager;
    [SerializeField] private BattleManager battleManager;

    private PlayerActionState currentState = PlayerActionState.Idle;
    private PendingActionType pendingAction = PendingActionType.None;
    private HashSet<BattleCharactor> validTargets = new HashSet<BattleCharactor>();
    private BattleCharactor hoverTarget = null;
    private Outline hoverTargetOutline;
    private readonly HashSet<BattleCharactor> deathSubscribedUnits = new HashSet<BattleCharactor>();
    private readonly List<ASBGridCell> highlightedCells = new List<ASBGridCell>();
    private bool isProcessingAction;

    private void Awake()
    {
        if (raycastCamera == null)
        {
            raycastCamera = Camera.main;
        }
    }

    private void OnEnable()
    {
        BindUnitDeathEvents(FindObjectsByType<BattleCharactor>(FindObjectsInactive.Include, FindObjectsSortMode.None));
    }

    private void OnDisable()
    {
        UnbindAllUnitDeathEvents();
        ResetTargetingState();
        ClearAoEPreview();
    }

    private void Update()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            BeginPendingAction(PendingActionType.BasicAttack);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            BeginPendingAction(PendingActionType.ClassSkill);
        }

        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            BeginPendingAction(PendingActionType.WeaponSkill);
        }

        if (currentState != PlayerActionState.WaitingForTarget)
        {
            ClearAoEPreview();
            return;
        }

        if (!TryGetCurrentActor(out BattleCharactor actor))
        {
            ResetTargetingState();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            ResetTargetingState();
            return;
        }

        UpdateHoverTarget(actor);
        if (hoverTarget != null && TryGetPendingSkillData(actor, out SkillData currentSelectedSkill))
        {
            UpdateAoEPreview(hoverTarget, currentSelectedSkill);
        }
        else
        {
            ClearAoEPreview();
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryExecutePendingAction(actor);
        }
    }

    /// <summary>BattleFlowManager가 턴 경계에서 호출해 선택 상태를 비웁니다.</summary>
    public void ClearSelectionState()
    {
        ResetTargetingState();
    }

    public void BindUnitDeathEvents(IEnumerable<BattleCharactor> units)
    {
        if (units == null)
        {
            return;
        }

        foreach (var unit in units)
        {
            if (unit == null)
            {
                continue;
            }

            unit.OnDied -= OnUnitDied;
            unit.OnDied += OnUnitDied;
            deathSubscribedUnits.Add(unit);
        }
    }

    private void UnbindAllUnitDeathEvents()
    {
        foreach (var unit in deathSubscribedUnits)
        {
            if (unit == null)
            {
                continue;
            }

            unit.OnDied -= OnUnitDied;
        }

        deathSubscribedUnits.Clear();
    }

    /// <summary>부활 스킬 등으로 죽은 유닛을 타겟에 남기려면 <see cref="RemoveDeadUnitFromTargets"/> 호출을 조건부로 끄면 됩니다.</summary>
    private void OnUnitDied(BattleCharactor deadUnit)
    {
        if (deadUnit == null)
        {
            return;
        }

        RemoveDeadUnitFromTargets(deadUnit);
    }

    private void RemoveDeadUnitFromTargets(BattleCharactor deadUnit)
    {
        validTargets.Remove(deadUnit);
        if (deadUnit == hoverTarget)
        {
            SetHoverTarget(null);
        }

        if (validTargets.Count == 0 && currentState == PlayerActionState.WaitingForTarget)
        {
            ResetTargetingState();
        }
    }

    private void BeginPendingAction(PendingActionType actionType)
    {
        if (!TryGetCurrentActor(out BattleCharactor actor))
        {
            return;
        }

        if (actionType == PendingActionType.ClassSkill && !TryGetSelectedSkill(actor, out _))
        {
            Debug.LogWarning($"[InputHandler] 선택된 CSV 스킬이 없습니다: actor={actor.UnitName}");
            return;
        }

        if (actionType == PendingActionType.WeaponSkill && actor.EquippedWeaponData == null)
        {
            Debug.LogWarning($"[InputHandler] 장착 무기가 없어 무기 스킬을 사용할 수 없습니다: actor={actor.UnitName}");
            return;
        }

        if (!HasEnoughInfluenceForAction(actor, actionType))
        {
            Debug.LogWarning("[InputHandler] Influence 부족 (선택 불가)");
            ResetTargetingState();
            return;
        }

        HashSet<BattleCharactor> targets = TargetingHelper.GetValidTargets(actor, actionType);
        if (targets.Count == 0)
        {
            Debug.LogWarning($"[InputHandler] 유효 타겟이 없습니다: actor={actor.UnitName}, action={actionType}");
            ResetTargetingState();
            return;
        }

        pendingAction = actionType;
        validTargets = targets;
        currentState = PlayerActionState.WaitingForTarget;
        SetHoverTarget(null);
        OnActionSelected?.Invoke(BuildSelectedActionLabel(actor, actionType));
    }

    private string BuildSelectedActionLabel(BattleCharactor actor, PendingActionType actionType)
    {
        switch (actionType)
        {
            case PendingActionType.BasicAttack:
                return "준비 중: 기본 공격";

            case PendingActionType.ClassSkill:
                if (TryGetSelectedSkill(actor, out SkillData classSkill) && classSkill != null)
                {
                    if (!string.IsNullOrWhiteSpace(classSkill.skillName))
                    {
                        return $"준비 중: {classSkill.skillName}";
                    }

                    return $"준비 중: 스킬 ID {classSkill.skillIndex}";
                }

                return "준비 중: 클래스 스킬";

            case PendingActionType.WeaponSkill:
                if (actor != null && actor.EquippedWeaponData != null)
                {
                    SkillData converted = actor.EquippedWeaponData.ToSkillData();
                    if (converted != null)
                    {
                        if (!string.IsNullOrWhiteSpace(converted.skillName))
                        {
                            return $"준비 중: {converted.skillName}";
                        }

                        return $"준비 중: 스킬 ID {converted.skillIndex}";
                    }
                }

                return "준비 중: 무기 스킬";

            default:
                return "준비 중...";
        }
    }

    private void UpdateHoverTarget(BattleCharactor actor)
    {
        if (raycastCamera == null)
        {
            SetHoverTarget(null);
            return;
        }

        BattleCharactor hitUnit = RaycastUnitUnderCursor();
        if (hitUnit == null || !validTargets.Contains(hitUnit))
        {
            SetHoverTarget(null);
            return;
        }

        if (!TargetingHelper.IsStillValidTarget(actor, pendingAction, hitUnit))
        {
            validTargets.Remove(hitUnit);
            SetHoverTarget(null);
            return;
        }

        SetHoverTarget(hitUnit);
    }

    private void TryExecutePendingAction(BattleCharactor actor)
    {
        if (isProcessingAction || hoverTarget == null || battleManager == null)
        {
            return;
        }

        if (!TargetingHelper.IsStillValidTarget(actor, pendingAction, hoverTarget))
        {
            validTargets.Remove(hoverTarget);
            SetHoverTarget(null);
            return;
        }

        if (!HasEnoughInfluenceForAction(actor, pendingAction))
        {
            Debug.LogWarning("[InputHandler] Influence 부족 (선택 불가)");
            ResetTargetingState();
            return;
        }

        BattleCharactor target = hoverTarget;
        StartCoroutine(ProcessActionRoutine(actor, target, pendingAction));
    }

    private System.Collections.IEnumerator ProcessActionRoutine(BattleCharactor actor, BattleCharactor target, PendingActionType actionType)
    {
        isProcessingAction = true;
        bool executed = false;

        switch (actionType)
        {
            case PendingActionType.BasicAttack:
                yield return StartCoroutine(battleManager.ExecuteBasicAttack(actor, target, success => executed = success));
                break;

            case PendingActionType.ClassSkill:
                if (!TryGetSelectedSkill(actor, out SkillData classSkill))
                {
                    Debug.LogWarning($"[InputHandler] 선택된 CSV 스킬이 없습니다: actor={actor.UnitName}");
                    break;
                }
                yield return StartCoroutine(battleManager.ExecuteGridSkill(actor, target, classSkill, success => executed = success));
                break;

            case PendingActionType.WeaponSkill:
                WeaponData weapon = actor.EquippedWeaponData;
                if (weapon == null)
                {
                    Debug.LogWarning($"[InputHandler] 장착 무기가 없어 무기 스킬을 사용할 수 없습니다: actor={actor.UnitName}");
                    break;
                }
                SkillData convertedSkill = weapon.ToSkillData();
                if (convertedSkill == null)
                {
                    Debug.LogWarning($"[InputHandler] 무기 스킬 변환 실패: weapon={weapon.WeaponName}");
                    break;
                }
                yield return StartCoroutine(battleManager.ExecuteGridSkill(actor, target, convertedSkill, success => executed = success));
                break;
        }

        if (executed)
        {
            PlayerSkillActionResolved?.Invoke(actor, target);
        }
        else
        {
            Debug.LogWarning("[InputHandler] 행동 실행 실패(false 반환). 턴 대기를 유지하고 입력 상태를 초기화합니다.");
        }

        ResetTargetingState();
        isProcessingAction = false;
    }

    private void UpdateAoEPreview(BattleCharactor hoverUnit, SkillData currentSelectedSkill)
    {
        // 진입 시 이전 프리뷰를 항상 정리하고 다시 그립니다.
        ClearAoEPreview();

        if (hoverUnit == null || currentSelectedSkill == null)
        {
            return;
        }

        if (!TryGetCurrentActor(out BattleCharactor actor))
        {
            return;
        }

        if (!validTargets.Contains(hoverUnit))
        {
            return;
        }

        // boundary 포함 유효 타겟 판정 재확인
        if (!TargetingHelper.IsStillValidTarget(actor, pendingAction, hoverUnit))
        {
            return;
        }

        ASBGridManager gridManager = ASBGridManager.Instance;
        if (gridManager == null)
        {
            return;
        }

        // 실행 파이프라인과 동일한 selector를 사용해 중심 타겟을 먼저 해석합니다.
        var previewContext = new SkillExecutionContext
        {
            Caster = actor,
            Skill = currentSelectedSkill,
            SelectedTarget = hoverUnit,
            SelectedCell = hoverUnit.OccupiedCell ?? gridManager.FindCellByUnit(hoverUnit)
        };
        ITargetSelector selector = SkillTargetSelectorRegistry.GetSelector(currentSelectedSkill.skillIndex);
        BattleCharactor primaryTarget = selector.SelectTarget(previewContext) ?? hoverUnit;

        ASBGridCell centerCell = primaryTarget != null
            ? primaryTarget.OccupiedCell ?? gridManager.FindCellByUnit(primaryTarget)
            : null;
        if (centerCell == null)
        {
            return;
        }

        bool singleTarget = currentSelectedSkill.classSkillTarget == 0 ||
                            currentSelectedSkill.boundary == null ||
                            currentSelectedSkill.boundary.Count == 0;
        if (singleTarget)
        {
            centerCell.SetHighlight();
            highlightedCells.Add(centerCell);
            return;
        }

        List<int> previewPattern = BuildPatternIncludingCenter(currentSelectedSkill.boundary);
        HashSet<Vector2Int> hitCoords = SkillTargetingMapper.GetMultiTargetCoordinates(centerCell.Coords, previewPattern);
        if (hitCoords == null || hitCoords.Count == 0)
        {
            return;
        }

        bool isTargetEnemySide = centerCell.Coords.x >= 2;
        foreach (Vector2Int coord in hitCoords)
        {
            if (gridManager.TryGetCell(coord, out ASBGridCell cell) && cell != null)
            {
                bool isSplashEnemySide = cell.Coords.x >= 2;
                // 프리뷰도 중심 타겟과 같은 진영 보드만 표시합니다.
                if (isTargetEnemySide != isSplashEnemySide)
                {
                    continue;
                }

                cell.SetHighlight();
                highlightedCells.Add(cell);
            }
        }
    }

    private bool TryGetPendingSkillData(BattleCharactor actor, out SkillData skillData)
    {
        skillData = null;
        switch (pendingAction)
        {
            case PendingActionType.BasicAttack:
                return false;

            case PendingActionType.ClassSkill:
                return TryGetSelectedSkill(actor, out skillData);

            case PendingActionType.WeaponSkill:
                if (actor == null || actor.EquippedWeaponData == null)
                {
                    return false;
                }

                skillData = actor.EquippedWeaponData.ToSkillData();
                return skillData != null;

            default:
                return false;
        }
    }

    private static List<int> BuildPatternIncludingCenter(List<int> sourcePattern)
    {
        List<int> pattern = sourcePattern != null ? new List<int>(sourcePattern) : new List<int>();
        if (!pattern.Contains(0))
        {
            pattern.Insert(0, 0);
        }

        return pattern;
    }

    private bool HasEnoughInfluenceForAction(BattleCharactor actor, PendingActionType actionType)
    {
        if (actor == null || !actor.IsPlayer)
        {
            return true;
        }

        if (actionType == PendingActionType.BasicAttack)
        {
            return true;
        }

        if (!TryGetActionSkillData(actor, actionType, out SkillData skillData) || skillData == null)
        {
            return false;
        }

        return actor.CurrentInfluence >= Mathf.Max(0f, skillData.IPCost);
    }

    private bool TryGetActionSkillData(BattleCharactor actor, PendingActionType actionType, out SkillData skillData)
    {
        skillData = null;
        if (actor == null)
        {
            return false;
        }

        switch (actionType)
        {
            case PendingActionType.ClassSkill:
                return TryGetSelectedSkill(actor, out skillData);
            case PendingActionType.WeaponSkill:
                if (actor.EquippedWeaponData == null)
                {
                    return false;
                }

                skillData = actor.EquippedWeaponData.ToSkillData();
                return skillData != null;
            case PendingActionType.BasicAttack:
            default:
                return false;
        }
    }

    private void ClearAoEPreview()
    {
        for (int i = 0; i < highlightedCells.Count; i++)
        {
            ASBGridCell cell = highlightedCells[i];
            if (cell != null)
            {
                cell.ClearHighlight();
            }
        }

        highlightedCells.Clear();
    }

    private BattleCharactor RaycastUnitUnderCursor()
    {
        var ray = raycastCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, selectionRaycastMask);
        if (hits == null || hits.Length == 0)
        {
            return null;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null)
            {
                continue;
            }

            BattleCharactor hitUnit = hits[i].collider.GetComponentInParent<BattleCharactor>();
            if (hitUnit != null)
            {
                return hitUnit;
            }
        }

        return null;
    }

    private void SetHoverTarget(BattleCharactor newTarget)
    {
        if (hoverTargetOutline != null)
        {
            hoverTargetOutline.OutlineMode = Outline.Mode.OutlineHidden;
            hoverTargetOutline = null;
        }

        hoverTarget = newTarget;
        if (hoverTarget == null)
        {
            return;
        }

        hoverTargetOutline = hoverTarget.GetComponentInChildren<Outline>(true);
        if (hoverTargetOutline != null)
        {
            hoverTargetOutline.OutlineMode = Outline.Mode.OutlineVisible;
        }
    }

    private void ResetTargetingState()
    {
        ClearAoEPreview();
        SetHoverTarget(null);
        validTargets.Clear();
        pendingAction = PendingActionType.None;
        currentState = PlayerActionState.Idle;
    }

    private bool TryGetCurrentActor(out BattleCharactor actor)
    {
        actor = battleFlowManager != null ? battleFlowManager.CurrentUnit : null;
        if (actor == null || !actor.IsPlayer || actor.IsDead)
        {
            actor = null;
            return false;
        }

        return true;
    }

    // 인스펙터에서 선택된 CSV 스킬 조회.
    private bool TryGetSelectedSkill(BattleCharactor actor, out SkillData skillData)
    {
        skillData = null;
        if (actor == null)
        {
            return false;
        }

        actor.ResolveSelectedSkill();
        skillData = actor.SelectedSkillData;
        return skillData != null;
    }

    // TODO: 적 스킬 인스펙터 선택 미구현
    // 적 스킬 저장 방식이 플레이어와 달라 추후 별도 추가 예정
    // TODO: 적 AI 스킬 연결 미구현
    // SelectedSkillData 또는 스킬 슬롯 조회 후
    // 범위 내 타겟 탐색 -> ExecuteGridSkill 호출 경로 추가 필요
}
