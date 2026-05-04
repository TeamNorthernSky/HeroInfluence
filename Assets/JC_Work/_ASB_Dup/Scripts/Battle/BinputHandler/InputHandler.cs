using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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
        if (hoverTarget == null || battleManager == null)
        {
            return;
        }

        if (!TargetingHelper.IsStillValidTarget(actor, pendingAction, hoverTarget))
        {
            validTargets.Remove(hoverTarget);
            SetHoverTarget(null);
            return;
        }

        BattleCharactor target = hoverTarget;
        bool executed = false;

        switch (pendingAction)
        {
            case PendingActionType.BasicAttack:
                executed = battleManager.ExecuteBasicAttack(actor, target);
                break;

            case PendingActionType.ClassSkill:
                if (!TryGetSelectedSkill(actor, out SkillData classSkill))
                {
                    Debug.LogWarning($"[InputHandler] 선택된 CSV 스킬이 없습니다: actor={actor.UnitName}");
                    break;
                }
                executed = battleManager.ExecuteGridSkill(actor, target, classSkill);
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
                executed = battleManager.ExecuteGridSkill(actor, target, convertedSkill);
                break;
        }

        if (executed)
        {
            PlayerSkillActionResolved?.Invoke(actor, target);
            ResetTargetingState();
        }
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
