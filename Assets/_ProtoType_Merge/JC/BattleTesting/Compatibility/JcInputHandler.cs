// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs
// 원본 객체: InputHandler -> JcInputHandler
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using System;
using System.Collections.Generic;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;
using ASBGridManager = ASB.Work.BattleGrid.JcBattleGridManager;
using UnityEngine;
using UnityEngine.EventSystems;
using ASB.Work.BattleGrid;
using ASB.Work.Battle.SkillExecution;
/// <summary>
/// 적 타겟 선택(클릭), 타겟 아웃라인, 숫자키로 JcBattleManager 실행 요청. 데미지 계산은 하지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public class JcInputHandler : MonoBehaviour
{
    public static event Action<BattleCharactor, BattleCharactor> PlayerSkillActionResolved;
    public event Action<string> OnActionSelected;
    // UI는 요청한 버튼이 아니라 실제 선택/취소 결과를 표시합니다.
    public event Action SelectionChanged;
    public PendingActionType PendingAction => pendingAction;
    public SkillData PendingSkill { get; private set; }
    private BattleCharactor selectionActor;

    [Tooltip("기존 씬의 턴 시작 자동 선택입니다. 개편 전투씬에서는 끄고 매 시전마다 스킬을 직접 선택합니다.")]
    [SerializeField] private bool selectSkillOnTurnStart = true;

    [Header("Raycast")]
    [Tooltip("대상 선택 광선을 쏘는 전투 카메라입니다. 비우면 MainCamera를 조회합니다.")]
    [SerializeField] private Camera raycastCamera;
    [Tooltip("대상 선택 광선의 최대 월드 거리입니다. 단위는 미터입니다.")]
    [SerializeField] private float maxRayDistance = 200f;

    [Header("Target selection")]
    [Tooltip("PlayerGrid, EnemyGrid, Player, Enemy 레이어를 포함한 마스크.")]
    [SerializeField] private LayerMask selectionRaycastMask = ~0;

    [Tooltip("JC 전투의 현재 턴과 행동 권한을 읽는 흐름입니다.")]
    [SerializeField] private JcBattleFlowManager battleFlowManager;
    [Tooltip("JC 전투의 스킬과 연출을 실행하는 컴포넌트입니다.")]
    [SerializeField] private JcBattleManager battleManager;
    [Tooltip("JC 입력이 선택 가능한 유닛과 셀의 표시를 갱신하는 제어입니다.")]
    [SerializeField] private JcTargetingVisualController targetingVisualController;

    private PlayerActionState currentState = PlayerActionState.Idle;
    private PendingActionType pendingAction = PendingActionType.None;
    private HashSet<BattleCharactor> validTargets = new HashSet<BattleCharactor>();
    private readonly HashSet<HostageBattleActor> validHostageTargets = new HashSet<HostageBattleActor>();
    private BattleCharactor hoverTarget = null;
    private HostageBattleActor hoverHostageTarget = null;
    private readonly HashSet<BattleCharactor> deathSubscribedUnits = new HashSet<BattleCharactor>();
    private bool isProcessingAction;

    public bool IsAutoBattleActive { get; set; }

    // 원본 함수 대응: InputHandler.Awake (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private void Awake()
    {
        if (raycastCamera == null)
            raycastCamera = Camera.main;

        if (targetingVisualController == null)
            targetingVisualController = FindFirstObjectByType<JcTargetingVisualController>();

        if (battleFlowManager == null)
            battleFlowManager = FindFirstObjectByType<JcBattleFlowManager>();

        if (battleManager == null)
            battleManager = FindFirstObjectByType<JcBattleManager>();
    }

    // 원본 함수 대응: InputHandler.OnEnable (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private void OnEnable()
    {
        BindUnitDeathEvents(FindObjectsByType<BattleCharactor>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        if (battleFlowManager != null)
            battleFlowManager.OnTurnStarted += OnTurnStarted;
    }

    // 원본 함수 대응: InputHandler.OnDisable (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private void OnDisable()
    {
        UnbindAllUnitDeathEvents();
        ResetTargetingState();
        ClearAoEPreview();
        if (battleFlowManager != null)
            battleFlowManager.OnTurnStarted -= OnTurnStarted;
    }

    // 원본 함수 대응: InputHandler.OnTurnStarted (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private void OnTurnStarted(int round, BattleCharactor unit)
    {
        if (!selectSkillOnTurnStart)
        {
            ResetTargetingState();
            return;
        }
        if (unit == null || !unit.IsPlayer) return;
        // 자동전투 중에는 타겟팅을 무장하지 않는다. 무장해두면 턴 도중 자동전투를 끄는 순간
        // 이미 준비된 선택 상태가 되살아나 같은 턴에 두 번 행동할 수 있다.
        if (IsAutoBattleActive) return;
        BeginPendingAction(PendingActionType.ClassSkill);
    }

    // 원본 함수 대응: InputHandler.ResolveAutoBattleAction (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    public void ResolveAutoBattleAction(BattleCharactor actor, BattleCharactor target)
    {
        PlayerSkillActionResolved?.Invoke(actor, target);
    }

    // 원본 함수 대응: InputHandler.Update (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private void Update()
    {
        if (battleFlowManager != null && battleFlowManager.IsTurnPresentationPending) return;
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

        // [JC 260812] ESC 제거: 시스템 메뉴 전역 키(SystemMenuController)와 같은 프레임에 이중 소비되어
        // 메뉴를 여는 순간 타겟팅이 소거되던 문제. 취소 제스처는 우클릭만 유지.
        // [JC 260914] 우클릭은 대상 선택만 취소합니다. 공격이 확정된 뒤에는 무시합니다.
        if (Input.GetMouseButtonDown(1))
        {
            TryCancelSkillSelection();
            return;
        }

        UpdateHoverTarget(actor);
        UpdateHoverHostageTarget(actor);
        if (TryResolveSkillData(actor, pendingAction, out SkillData currentSelectedSkill))
        {
            if (hoverTarget != null)
                UpdateAoEPreview(hoverTarget, currentSelectedSkill);
            else if (hoverHostageTarget != null)
                UpdateHostageAoEPreview(actor, hoverHostageTarget, currentSelectedSkill);
            else
                ClearAoEPreview();
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

    /// <summary>사용자 입력으로 선택을 비웁니다. 자동전투·공격 실행 중에는 false를 반환하고 유지합니다.</summary>
    // 원본 함수 대응: InputHandler.TryCancelSkillSelection (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)
    public bool TryCancelSkillSelection()
    {
        if (battleFlowManager != null && battleFlowManager.IsTurnPresentationPending) return false;
        if (IsAutoBattleActive || isProcessingAction) return false;
        ResetTargetingState();
        return true;
    }

    /// <summary>BattleFlowManager가 턴 경계에서 호출해 선택 상태를 비웁니다.</summary>
    // 원본 함수 대응: InputHandler.ClearSelectionState (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)
    public void ClearSelectionState()
    {
        ResetTargetingState();
    }

    // [JC 260513] 키 4 스킵 처리. hover/선택 무관 즉시 발화. target=null → BattleFlowManager가 다음 턴으로.
    // 원본 함수 대응: InputHandler.SkipCurrentTurn (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)
    private void SkipCurrentTurn()
    {
        if (isProcessingAction) return;
        if (!TryGetCurrentActor(out BattleCharactor actor)) return;
        if (battleFlowManager != null &&
            !battleFlowManager.IsPlayerActionAllowed(actor, PendingActionType.Skip, null))
        {
            return;
        }
        // 스킵도 '이번 턴의 행동'이다. 점유권을 얻지 못하면(이미 자동전투 등이 실행 중) 무시한다.
        if (battleFlowManager != null && !battleFlowManager.TryClaimPlayerAction(actor)) return;
        ResetTargetingState();
        ClearAoEPreview();
        PlayerSkillActionResolved?.Invoke(actor, null);
        Debug.Log($"[JcInputHandler] 턴 스킵: {actor.UnitName}");
    }

    // 원본 함수 대응: InputHandler.BindUnitDeathEvents (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

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

    // 원본 함수 대응: InputHandler.UnbindAllUnitDeathEvents (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

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
    // 원본 함수 대응: InputHandler.OnUnitDied (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)
    private void OnUnitDied(BattleCharactor deadUnit)
    {
        if (deadUnit == null)
        {
            return;
        }

        RemoveDeadUnitFromTargets(deadUnit);
    }

    // 원본 함수 대응: InputHandler.RemoveDeadUnitFromTargets (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

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

    // 원본 함수 대응: InputHandler.BeginPendingAction (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    public void BeginPendingAction(PendingActionType actionType, int skillId = 0)
    {
        if (battleFlowManager != null && battleFlowManager.IsTurnPresentationPending) return;
        if (IsAutoBattleActive || isProcessingAction) return;
        if (!TryGetCurrentActor(out BattleCharactor actor))
        {
            return;
        }

        if (battleFlowManager != null &&
            !battleFlowManager.IsPlayerActionAllowed(actor, actionType, null))
        {
            Debug.Log($"[JcInputHandler] 현재 시나리오에서 허용되지 않은 행동입니다: action={actionType}");
            return;
        }

        ResetTargetingState();
        if (actionType == PendingActionType.ClassSkill && skillId > 0 && !actor.TrySelectSkillForCast(skillId)) return;
        selectionActor = actor;

        if (actionType == PendingActionType.ClassSkill && !TryGetSelectedSkill(actor, out _))
        {
            Debug.LogWarning($"[JcInputHandler] 선택된 CSV 스킬이 없습니다: actor={actor.UnitName}");
            return;
        }

        if (actionType == PendingActionType.WeaponSkill && actor.EquippedWeaponData == null)
        {
            Debug.LogWarning($"[JcInputHandler] 장착 무기가 없어 무기 스킬을 사용할 수 없습니다: actor={actor.UnitName}");
            return;
        }

        if (!selectSkillOnTurnStart && actionType == PendingActionType.ClassSkill &&
            actor.SelectedSkillData.acquireLevel > actor.Level)
        {
            ResetTargetingState();
            return;
        }

        if (!HasEnoughInfluenceForAction(actor, actionType))
        {
            Debug.LogWarning("[JcInputHandler] Influence 부족 (선택 불가)");
            ResetTargetingState();
            return;
        }

        HashSet<BattleCharactor> targets = TargetingHelper.GetValidTargets(actor, actionType);
        validHostageTargets.Clear();
        if (TryResolveSkillData(actor, actionType, out SkillData hostageTargetSkill))
            validHostageTargets.UnionWith(HostageFriendlyFireResolver.GetSelectableTargets(actor, hostageTargetSkill));
        if (targets.Count == 0 && validHostageTargets.Count == 0)
        {
            Debug.LogWarning($"[JcInputHandler] 유효 타겟이 없습니다: actor={actor.UnitName}, action={actionType}");
            ResetTargetingState();
            return;
        }

        pendingAction = actionType;
        TryResolveSkillData(actor, actionType, out var selected);
        PendingSkill = selected;
        validTargets = targets;
        currentState = PlayerActionState.WaitingForTarget;
        targetingVisualController?.ShowSelectableTargets(validTargets);
        targetingVisualController?.ShowSelectableHostageTargets(validHostageTargets);
        SetHoverTarget(null);
        SetHoverHostageTarget(null);
        OnActionSelected?.Invoke(BuildSelectedActionLabel(actor, actionType));
        SelectionChanged?.Invoke();
    }

    // 원본 함수 대응: InputHandler.BuildSelectedActionLabel (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

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

    // 원본 함수 대응: InputHandler.UpdateHoverTarget (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private void UpdateHoverTarget(BattleCharactor actor)
    {
        if (raycastCamera == null)
        {
            SetHoverTarget(null);
            return;
        }

        BattleCharactor hitUnit = RaycastUnitUnderCursor();
        SkillActivationRules.TryResolveClick(actor, PendingSkill, hitUnit, RaycastCellUnderCursor(), out hitUnit);
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

    // 원본 함수 대응: InputHandler.TryExecutePendingAction (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private void TryExecutePendingAction(BattleCharactor actor)
    {
        if (isProcessingAction || (hoverTarget == null && hoverHostageTarget == null) || battleManager == null)
        {
            return;
        }

        BattleCharactor requestedTarget = hoverTarget;
        if (battleFlowManager != null &&
            !battleFlowManager.IsPlayerActionAllowed(actor, pendingAction, requestedTarget))
        {
            Debug.Log($"[JcInputHandler] 현재 시나리오에서 허용되지 않은 대상/행동입니다: action={pendingAction}");
            return;
        }

        // Hostages are not BattleCharactors. Validate and execute this route before the
        // normal BattleCharactor validity check, which correctly rejects a null hoverTarget.
        if (hoverHostageTarget != null)
        {
            if (!HostageFriendlyFireResolver.IsStillValidTarget(actor, pendingAction, hoverHostageTarget))
            {
                validHostageTargets.Remove(hoverHostageTarget);
                targetingVisualController?.RemoveSelectableHostageTarget(hoverHostageTarget);
                SetHoverHostageTarget(null);
                return;
            }

            if (battleFlowManager != null && !battleFlowManager.TryClaimPlayerAction(actor)) return;
            StartCoroutine(ExecuteHostageActionRoutine(actor, hoverHostageTarget, pendingAction));
            return;
        }

        if (hoverTarget == null || !TargetingHelper.IsStillValidTarget(actor, pendingAction, hoverTarget))
        {
            if (hoverTarget != null)
            {
                validTargets.Remove(hoverTarget);
                targetingVisualController?.RemoveSelectableTarget(hoverTarget);
            }
            SetHoverTarget(null);
            return;
        }

        if (battleFlowManager != null && !battleFlowManager.TryClaimPlayerAction(actor)) return;
        BattleCharactor target = hoverTarget;
        StartCoroutine(ProcessActionRoutine(actor, target, pendingAction));
    }

    // 원본 함수 대응: InputHandler.ProcessActionRoutine (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private System.Collections.IEnumerator ProcessActionRoutine(BattleCharactor actor, BattleCharactor target, PendingActionType actionType)
    {
        isProcessingAction = true;
        bool executed = false;
        SkillData requestedSkill = PendingSkill;

        switch (actionType)
        {
            case PendingActionType.ClassSkill:
                SkillData classSkill = requestedSkill;
                if (classSkill == null)
                {
                    Debug.LogWarning($"[JcInputHandler] 선택된 CSV 스킬이 없습니다: actor={actor.UnitName}");
                    break;
                }
                yield return StartCoroutine(battleManager.ExecuteGridSkill(actor, target, classSkill, success => executed = success));
                break;

            case PendingActionType.WeaponSkill:
                WeaponData weapon = actor.EquippedWeaponData;
                if (weapon == null)
                {
                    Debug.LogWarning($"[JcInputHandler] 장착 무기가 없어 무기 스킬을 사용할 수 없습니다: actor={actor.UnitName}");
                    break;
                }
                SkillData convertedSkill = requestedSkill;
                if (convertedSkill == null)
                {
                    Debug.LogWarning($"[JcInputHandler] 무기 스킬 변환 실패: weapon={weapon.WeaponName}");
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
            Debug.LogWarning("[JcInputHandler] 행동 실행 실패(false 반환). 턴 대기를 유지하고 입력 상태를 초기화합니다.");
        }

        if (!executed) battleFlowManager?.ReleaseFailedPlayerAction(actor);
        ResetTargetingState();
        isProcessingAction = false;
    }

    // 원본 함수 대응: InputHandler.UpdateHostageAoEPreview (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private void UpdateHostageAoEPreview(BattleCharactor actor, HostageBattleActor hostage, SkillData skill)
    {
        ASBGridManager.Instance?.ClearPreviewHighlight();

        if (actor == null || hostage == null || skill == null)
            return;
        if (!validHostageTargets.Contains(hostage) ||
            !HostageFriendlyFireResolver.IsStillValidTarget(actor, pendingAction, hostage))
            return;

        if (!HostageFriendlyFireResolver.TryGetPreviewCells(hostage, skill,
                out ASBGridCell mainCell, out List<ASBGridCell> previewCells))
            return;

        var splashCells = new List<ASBGridCell>();
        for (int i = 0; i < previewCells.Count; i++)
        {
            ASBGridCell cell = previewCells[i];
            if (cell != null && cell != mainCell)
                splashCells.Add(cell);
        }

        ASBGridManager.Instance?.ShowPreviewHighlight(skill, mainCell, splashCells);
    }

    // 원본 함수 대응: InputHandler.UpdateAoEPreview (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private void UpdateAoEPreview(BattleCharactor hoverUnit, SkillData currentSelectedSkill)
    {
        ASBGridManager.Instance?.ClearPreviewHighlight();

        if (hoverUnit == null || currentSelectedSkill == null) return;
        if (!TryGetCurrentActor(out BattleCharactor actor)) return;
        if (!validTargets.Contains(hoverUnit)) return;
        if (!TargetingHelper.IsStillValidTarget(actor, pendingAction, hoverUnit)) return;

        if (!JcSkillAreaPreviewHelper.TryGetAreaCells(actor, hoverUnit, currentSelectedSkill,
                out ASBGridCell centerCell, out List<ASBGridCell> splashCells)) return;

        ASBGridManager.Instance?.ShowPreviewHighlight(currentSelectedSkill, centerCell, splashCells);
    }

    // 원본 함수 대응: InputHandler.TryResolveSkillData (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private bool TryResolveSkillData(BattleCharactor actor, PendingActionType actionType, out SkillData skillData)
    {
        skillData = null;
        if (actor == null)
        {
            return false;
        }

        if (selectionActor == actor && PendingSkill != null && actionType == pendingAction)
        {
            skillData = PendingSkill;
            return true;
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

    // 원본 함수 대응: InputHandler.HasEnoughInfluenceForAction (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

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

    // 원본 함수 대응: InputHandler.ClearAoEPreview (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private void ClearAoEPreview()
    {
        ASBGridManager.Instance?.ClearPreviewHighlight();
    }

    // 원본 함수 대응: InputHandler.UpdateHoverHostageTarget (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

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

    // 원본 함수 대응: InputHandler.ExecuteHostageActionRoutine (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

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

        if (!executed) battleFlowManager?.ReleaseFailedPlayerAction(actor);
        ResetTargetingState();
        isProcessingAction = false;
    }

    // 원본 함수 대응: InputHandler.RaycastHostageUnderCursor (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

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

    // 원본 함수 대응: InputHandler.RaycastCellUnderCursor (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private ASBGridCell RaycastCellUnderCursor()
    {
        if (raycastCamera == null) return null;
        var hits = Physics.RaycastAll(raycastCamera.ScreenPointToRay(Input.mousePosition), maxRayDistance, selectionRaycastMask);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider == null) continue;
            var cell = hit.collider.GetComponentInParent<ASBGridCell>();
            if (cell != null) return cell;
            var unit = hit.collider.GetComponentInParent<BattleCharactor>();
            if (unit != null && unit.OccupiedCell != null) return unit.OccupiedCell;
        }
        return null;
    }

    // 원본 함수 대응: InputHandler.RaycastUnitUnderCursor (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

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
                // 가장 가까운 빈 발판 뒤의 다른 유닛으로 클릭이 뚫리지 않게 합니다.
                if (hitCell != null) return hitCell.OccupyingUnit;
            }

            if (hitUnit != null)
            {
                return hitUnit;
            }
        }

        return null;
    }

    // 원본 함수 대응: InputHandler.SetHoverHostageTarget (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private void SetHoverHostageTarget(HostageBattleActor newTarget)
    {
        if (newTarget != null && !validHostageTargets.Contains(newTarget))
            newTarget = null;

        if (hoverHostageTarget == newTarget)
            return;

        hoverHostageTarget = newTarget;
        targetingVisualController?.SetHoveredHostageTarget(hoverHostageTarget);
    }

    // 원본 함수 대응: InputHandler.SetHoverTarget (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private void SetHoverTarget(BattleCharactor newTarget)
    {
        hoverTarget = newTarget;
        targetingVisualController?.SetHoveredTarget(hoverTarget);
    }

    // 원본 함수 대응: InputHandler.ResetTargetingState (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private void ResetTargetingState()
    {
        selectionActor?.ClearSkillForCast();
        selectionActor = null;
        PendingSkill = null;
        targetingVisualController?.ClearAll();
        ClearAoEPreview();
        SetHoverTarget(null);
        SetHoverHostageTarget(null);
        validTargets.Clear();
        validHostageTargets.Clear();
        pendingAction = PendingActionType.None;
        currentState = PlayerActionState.Idle;
        SelectionChanged?.Invoke();
    }

    // 원본 함수 대응: InputHandler.TryGetCurrentActor (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)

    private bool TryGetCurrentActor(out BattleCharactor actor)
    {
        if (battleFlowManager == null)
        {
            Debug.LogError("[JcInputHandler] battleFlowManager가 null입니다! Inspector에서 연결하거나 씬에 BattleFlowManager가 있는지 확인하세요.");
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
    // 원본 함수 대응: InputHandler.TryGetSelectedSkill (Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs)
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
