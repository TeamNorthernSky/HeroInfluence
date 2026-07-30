using System;
using System.Collections.Generic;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;
using ASBGridManager = ASB.Work.BattleGrid.BattleGridManager;
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
    [SerializeField] private TargetingVisualController targetingVisualController;

    private PlayerActionState currentState = PlayerActionState.Idle;
    private PendingActionType pendingAction = PendingActionType.None;
    private HashSet<BattleCharactor> validTargets = new HashSet<BattleCharactor>();
    private readonly HashSet<HostageBattleActor> validHostageTargets = new HashSet<HostageBattleActor>();
    private BattleCharactor hoverTarget = null;
    private HostageBattleActor hoverHostageTarget = null;
    private readonly HashSet<BattleCharactor> deathSubscribedUnits = new HashSet<BattleCharactor>();
    private bool isProcessingAction;

    public bool IsAutoBattleActive { get; set; }

    private void Awake()
    {
        if (raycastCamera == null)
            raycastCamera = Camera.main;

        if (targetingVisualController == null)
            targetingVisualController = FindFirstObjectByType<TargetingVisualController>();

        if (battleFlowManager == null)
            battleFlowManager = FindFirstObjectByType<BattleFlowManager>();

        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();
    }

    private void OnEnable()
    {
        BindUnitDeathEvents(FindObjectsByType<BattleCharactor>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        if (battleFlowManager != null)
            battleFlowManager.OnTurnStarted += OnTurnStarted;
    }

    private void OnDisable()
    {
        UnbindAllUnitDeathEvents();
        ResetTargetingState();
        ClearAoEPreview();
        if (battleFlowManager != null)
            battleFlowManager.OnTurnStarted -= OnTurnStarted;
    }

    private void OnTurnStarted(int round, BattleCharactor unit)
    {
        if (unit == null || !unit.IsPlayer) return;
        BeginPendingAction(PendingActionType.ClassSkill);
    }

    public void ResolveAutoBattleAction(BattleCharactor actor, BattleCharactor target)
    {
        PlayerSkillActionResolved?.Invoke(actor, target);
    }

    private void Update()
    {
        if (IsAutoBattleActive) return;

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            BeginPendingAction(PendingActionType.ClassSkill);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            BeginPendingAction(PendingActionType.WeaponSkill);
        }


        // [JC 260513] 키 4: 아무 행동 없이 턴 종료(스킵). hover/선택 상태 자동 해제.
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
        {
            SkipCurrentTurn();
            return;
        }

        // [JC 260622] Ctrl+Shift+8: 아군(현재 플레이어) 턴 스킵. 키4와 동일 경로(오입력 방지 조합).
        // 적 턴엔 BattleFlowManager의 IsPlayer 가드로 무시됨.
        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (ctrlHeld && shiftHeld && (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)))
        {
            SkipCurrentTurn();
            return;
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
        UpdateHoverHostageTarget(actor);
        if (hoverTarget != null && TryResolveSkillData(actor, pendingAction, out SkillData currentSelectedSkill))
        {
            UpdateAoEPreview(hoverTarget, currentSelectedSkill);
        }
        else
        {
            ClearAoEPreview();
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            TryExecutePendingAction(actor);
        }
    }

    /// <summary>BattleFlowManager가 턴 경계에서 호출해 선택 상태를 비웁니다.</summary>
    public void ClearSelectionState()
    {
        ResetTargetingState();
    }

    // [JC 260513] 키 4 스킵 처리. hover/선택 무관 즉시 발화. target=null → BattleFlowManager가 다음 턴으로.
    private void SkipCurrentTurn()
    {
        if (!TryGetCurrentActor(out BattleCharactor actor)) return;
        ResetTargetingState();
        ClearAoEPreview();
        PlayerSkillActionResolved?.Invoke(actor, null);
        Debug.Log($"[InputHandler] 턴 스킵: {actor.UnitName}");
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
        targetingVisualController?.RemoveSelectableTarget(deadUnit);
        if (deadUnit == hoverTarget)
        {
            SetHoverTarget(null);
        }

        if (validTargets.Count == 0 && currentState == PlayerActionState.WaitingForTarget)
        {
            ResetTargetingState();
        }
    }

    public void BeginPendingAction(PendingActionType actionType)
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
        validHostageTargets.Clear();
        if (TryResolveSkillData(actor, actionType, out SkillData hostageTargetSkill))
            validHostageTargets.UnionWith(HostageFriendlyFireResolver.GetSelectableTargets(actor, hostageTargetSkill));
        if (targets.Count == 0 && validHostageTargets.Count == 0)
        {
            Debug.LogWarning($"[InputHandler] 유효 타겟이 없습니다: actor={actor.UnitName}, action={actionType}");
            ResetTargetingState();
            return;
        }

        pendingAction = actionType;
        validTargets = targets;
        currentState = PlayerActionState.WaitingForTarget;
        targetingVisualController?.ShowSelectableTargets(validTargets);
        SetHoverTarget(null);
        OnActionSelected?.Invoke(BuildSelectedActionLabel(actor, actionType));
    }

    private string BuildSelectedActionLabel(BattleCharactor actor, PendingActionType actionType)
    {
        switch (actionType)
        {
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
            targetingVisualController?.RemoveSelectableTarget(hitUnit);
            SetHoverTarget(null);
            return;
        }

        SetHoverTarget(hitUnit);
    }

    private void TryExecutePendingAction(BattleCharactor actor)
    {
        if (isProcessingAction || (hoverTarget == null && hoverHostageTarget == null) || battleManager == null)
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

        if (hoverHostageTarget != null)
        {
            if (!HostageFriendlyFireResolver.IsStillValidTarget(actor, pendingAction, hoverHostageTarget))
            {
                validHostageTargets.Remove(hoverHostageTarget);
                SetHoverHostageTarget(null);
                return;
            }

            StartCoroutine(ExecuteHostageActionRoutine(actor, hoverHostageTarget, pendingAction));
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
        ASBGridManager.Instance?.ClearPreviewHighlight();

        if (hoverUnit == null || currentSelectedSkill == null) return;
        if (!TryGetCurrentActor(out BattleCharactor actor)) return;
        if (!validTargets.Contains(hoverUnit)) return;
        if (!TargetingHelper.IsStillValidTarget(actor, pendingAction, hoverUnit)) return;

        if (!SkillAreaPreviewHelper.TryGetAreaCells(actor, hoverUnit, currentSelectedSkill,
                out ASBGridCell centerCell, out List<ASBGridCell> splashCells)) return;

        ASBGridManager.Instance?.ShowPreviewHighlight(currentSelectedSkill, centerCell, splashCells);
    }

    private bool TryResolveSkillData(BattleCharactor actor, PendingActionType actionType, out SkillData skillData)
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

            default:
                return false;
        }
    }

    private bool HasEnoughInfluenceForAction(BattleCharactor actor, PendingActionType actionType)
    {
        if (actor == null || !actor.IsPlayer)
        {
            return true;
        }

        if (!TryResolveSkillData(actor, actionType, out SkillData skillData) || skillData == null)
        {
            return false;
        }

        return actor.CurrentInfluence >= Mathf.Max(0f, skillData.IPCost);
    }

    private void ClearAoEPreview()
    {
        ASBGridManager.Instance?.ClearPreviewHighlight();
    }

    private void UpdateHoverHostageTarget(BattleCharactor actor)
    {
        if (hoverTarget != null)
        {
            SetHoverHostageTarget(null);
            return;
        }

        HostageBattleActor hostage = RaycastHostageUnderCursor();
        if (hostage == null || !validHostageTargets.Contains(hostage) ||
            !HostageFriendlyFireResolver.IsStillValidTarget(actor, pendingAction, hostage))
        {
            SetHoverHostageTarget(null);
            return;
        }

        SetHoverHostageTarget(hostage);
    }

    private System.Collections.IEnumerator ExecuteHostageActionRoutine(
        BattleCharactor actor,
        HostageBattleActor hostage,
        PendingActionType actionType)
    {
        isProcessingAction = true;
        bool executed = false;
        if (TryResolveSkillData(actor, actionType, out SkillData skillData))
        {
            yield return StartCoroutine(
                battleManager.ExecuteHostageSkill(actor, hostage, skillData, success => executed = success));
        }

        if (executed)
            PlayerSkillActionResolved?.Invoke(actor, null);

        ResetTargetingState();
        isProcessingAction = false;
    }

    private HostageBattleActor RaycastHostageUnderCursor()
    {
        if (raycastCamera == null)
            return null;

        RaycastHit[] hits = Physics.RaycastAll(
            raycastCamera.ScreenPointToRay(Input.mousePosition),
            maxRayDistance,
            selectionRaycastMask);
        if (hits == null || hits.Length == 0)
            return null;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null)
                continue;

            HostageBattleActor hostage = hits[i].collider.GetComponentInParent<HostageBattleActor>();
            if (hostage == null)
            {
                ASBGridCell cell = hits[i].collider.GetComponentInParent<ASBGridCell>();
                hostage = cell != null ? cell.GetComponentInChildren<HostageBattleActor>(true) : null;
            }

            if (hostage != null)
                return hostage;
        }

        return null;
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

            // 캐릭터 모델 콜라이더를 먼저 시도하고, 없으면 그리드 셀 콜라이더의 점유 유닛으로 폴백합니다.
            // 거리순 정렬이므로 카메라에 더 가까운 쪽(대개 캐릭터 모델)이 자연스럽게 우선됩니다.
            BattleCharactor hitUnit = hits[i].collider.GetComponentInParent<BattleCharactor>();
            if (hitUnit == null)
            {
                ASBGridCell hitCell = hits[i].collider.GetComponentInParent<ASBGridCell>();
                hitUnit = hitCell?.OccupyingUnit;
            }

            if (hitUnit != null)
            {
                return hitUnit;
            }
        }

        return null;
    }

    private void SetHoverHostageTarget(HostageBattleActor newTarget)
    {
        hoverHostageTarget = newTarget;
    }

    private void SetHoverTarget(BattleCharactor newTarget)
    {
        hoverTarget = newTarget;
        targetingVisualController?.SetHoveredTarget(hoverTarget);
    }

    private void ResetTargetingState()
    {
        targetingVisualController?.ClearAll();
        ClearAoEPreview();
        SetHoverTarget(null);
        SetHoverHostageTarget(null);
        validTargets.Clear();
        validHostageTargets.Clear();
        pendingAction = PendingActionType.None;
        currentState = PlayerActionState.Idle;
    }

    private bool TryGetCurrentActor(out BattleCharactor actor)
    {
        if (battleFlowManager == null)
        {
            Debug.LogError("[InputHandler] battleFlowManager가 null입니다! Inspector에서 연결하거나 씬에 BattleFlowManager가 있는지 확인하세요.");
            actor = null;
            return false;
        }

        actor = battleFlowManager.CurrentUnit;
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
