using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자동 전투 컨트롤러.
/// 플레이어 턴에 랜덤 액션/타겟을 선택해 BattleManager로 실행합니다.
/// InputHandler.IsAutoBattleActive 플래그로 수동 입력을 차단합니다.
/// UI에서 OnAutoBattleToggleRequested 이벤트를 발생시켜 자동 전투를 제어합니다.
/// </summary>
[DisallowMultipleComponent]
public class AutoBattleController : MonoBehaviour
{
    /// <summary>UI 버튼 등 외부에서 자동 전투 토글을 요청할 때 발생시킵니다.</summary>
    public static event Action<bool> OnAutoBattleToggleRequested;

    [SerializeField] private BattleFlowManager flowManager;
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private InputHandler inputHandler;

    [Header("Auto Battle")]
    [SerializeField] private bool isAutoBattle = false;
    [SerializeField] private float thinkDelay = 0.5f;

    public bool IsAutoBattle
    {
        get => isAutoBattle;
        set
        {
            if (isAutoBattle == value)
            {
                BattleRuntimeSettings.SetAutoBattle(value);
                if (inputHandler != null)
                    inputHandler.IsAutoBattleActive = value;
                return;
            }

            isAutoBattle = value;
            BattleRuntimeSettings.SetAutoBattle(value);

            if (inputHandler != null)
                inputHandler.IsAutoBattleActive = value;

            // 타겟팅 선택 상태는 켤 때든 끌 때든 정리한다.
            // 끌 때 정리하지 않으면 자동전투 중 무장돼 있던 선택 상태가 되살아나 이중 행동으로 이어진다.
            inputHandler?.ClearSelectionState();

            if (value)
            {
                // 이미 플레이어 턴 WaitUntil 중이면 즉시 실행
                if (flowManager != null
                    && flowManager.CurrentUnit != null
                    && flowManager.CurrentUnit.IsPlayer
                    && !flowManager.CurrentUnit.IsDead)
                {
                    StartCoroutine(RunAutoBattleTurn(flowManager.CurrentUnit));
                }
            }
        }
    }

    private void OnEnable()
    {
        if (flowManager != null)
            flowManager.OnTurnStarted += OnTurnStarted;

        OnAutoBattleToggleRequested += HandleToggleRequest;
        IsAutoBattle = BattleRuntimeSettings.IsAutoBattle;
    }

    private void OnDisable()
    {
        if (flowManager != null)
            flowManager.OnTurnStarted -= OnTurnStarted;

        OnAutoBattleToggleRequested -= HandleToggleRequest;

        if (inputHandler != null)
            inputHandler.IsAutoBattleActive = false;
    }

    private void HandleToggleRequest(bool enable)
    {
        IsAutoBattle = enable;
    }

    [ContextMenu("자동 전투 ON")]
    private void DebugEnableAutoBattle() => IsAutoBattle = true;

    [ContextMenu("자동 전투 OFF")]
    private void DebugDisableAutoBattle() => IsAutoBattle = false;

    private void OnTurnStarted(int round, BattleCharactor unit)
    {
        if (!isAutoBattle) return;
        if (unit == null || !unit.IsPlayer || unit.IsDead) return;

        StartCoroutine(RunAutoBattleTurn(unit));
    }

    private IEnumerator RunAutoBattleTurn(BattleCharactor actor)
    {
        if (inputHandler != null)
            inputHandler.IsAutoBattleActive = true;

        // 유효한 액션 후보 수집 (타겟 존재 + Influence 충분)
        var candidates = new List<(PendingActionType actionType, List<BattleCharactor> targets)>();

        // ClassSkill
        actor.ResolveSelectedSkill();
        if (actor.SelectedSkillData != null &&
            actor.CurrentInfluence >= actor.SelectedSkillData.IPCost)
        {
            var classTargets = new List<BattleCharactor>(
                TargetingHelper.GetValidTargets(actor, PendingActionType.ClassSkill));
            if (classTargets.Count > 0)
                candidates.Add((PendingActionType.ClassSkill, classTargets));
        }

        // WeaponSkill
        if (actor.EquippedWeaponData != null)
        {
            var weaponSkill = actor.EquippedWeaponData.ToSkillData();
            if (weaponSkill != null && actor.CurrentInfluence >= weaponSkill.IPCost)
            {
                var weaponTargets = new List<BattleCharactor>(
                    TargetingHelper.GetValidTargets(actor, PendingActionType.WeaponSkill));
                if (weaponTargets.Count > 0)
                    candidates.Add((PendingActionType.WeaponSkill, weaponTargets));
            }
        }

        // 후보가 없으면 턴 스킵
        if (candidates.Count == 0)
        {
            Debug.Log($"[AutoBattle] {actor.UnitName}: 유효한 액션 없음 → 턴 스킵");
            inputHandler?.ResolveAutoBattleAction(actor, null);
            if (inputHandler != null) inputHandler.IsAutoBattleActive = false;
            yield break;
        }

        // 랜덤 액션·타겟 선택
        var selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        PendingActionType actionType = selected.actionType;
        BattleCharactor target = selected.targets[UnityEngine.Random.Range(0, selected.targets.Count)];

        Debug.Log($"[AutoBattle] {actor.UnitName}: {actionType} → {target.UnitName}");

        actor.ResolveSelectedSkill();
        SkillData highlightSkill = actionType == PendingActionType.ClassSkill
            ? actor.SelectedSkillData
            : actor.EquippedWeaponData?.ToSkillData();

        // 타겟 발판 하이라이트 표시 후 대기
        flowManager?.ShowTargetHighlight(actor, target, highlightSkill);
        float speed = BattleManager.Instance != null ? BattleManager.Instance.CurrentBattleSpeed : 1f;
        yield return new WaitForSeconds(thinkDelay / Mathf.Max(0.01f, speed));

        // 대기 전 전제를 다시 확인한다. 대기 중에 액터가 죽거나 기절하거나, 턴이 이미 넘어갔을 수 있다.
        if (actor == null || actor.IsDead || actor.IsStunned
            || flowManager == null || flowManager.CurrentUnit != actor)
        {
            Debug.LogWarning($"[AutoBattle] 대기 중 상태가 바뀌어 실행을 취소한다: actor={actor?.UnitName}");
            flowManager?.ClearTargetHighlight();
            if (inputHandler != null) inputHandler.IsAutoBattleActive = false;
            yield break;
        }

        // 이번 턴의 행동 권한 확보. 수동 입력이 먼저 가져갔으면 자동전투는 물러난다.
        if (!flowManager.TryClaimPlayerAction(actor))
        {
            Debug.Log($"[AutoBattle] 이번 턴의 행동이 이미 진행 중이라 자동전투를 건너뛴다: actor={actor.UnitName}");
            flowManager.ClearTargetHighlight();
            if (inputHandler != null) inputHandler.IsAutoBattleActive = false;
            yield break;
        }

        // 실행
        bool executed = false;
        switch (actionType)
        {
            case PendingActionType.ClassSkill:
                yield return StartCoroutine(
                    battleManager.ExecuteGridSkill(actor, target, highlightSkill,
                        success => executed = success));
                break;

            case PendingActionType.WeaponSkill:
                yield return StartCoroutine(
                    battleManager.ExecuteGridSkill(actor, target, highlightSkill,
                        success => executed = success));
                break;
        }

        flowManager?.ClearTargetHighlight();

        if (executed)
            inputHandler?.ResolveAutoBattleAction(actor, target);
        else
        {
            // 실행 실패 시 소프트락 방지를 위해 스킵
            Debug.LogWarning($"[AutoBattle] {actor.UnitName}: 실행 실패 → 턴 스킵");
            inputHandler?.ResolveAutoBattleAction(actor, null);
        }

        if (inputHandler != null) inputHandler.IsAutoBattleActive = false;
    }
}
