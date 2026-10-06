// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/AutoBattleController.cs
// 원본 객체: AutoBattleController -> JcAutoBattleController
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자동 전투 컨트롤러.
/// 플레이어 턴에 랜덤 액션/타겟을 선택해 BattleManager로 실행합니다.
/// JcInputHandler.IsAutoBattleActive 플래그로 수동 입력을 차단합니다.
/// UI에서 OnAutoBattleToggleRequested 이벤트를 발생시켜 자동 전투를 제어합니다.
/// </summary>
[DisallowMultipleComponent]
public class JcAutoBattleController : MonoBehaviour
{
    /// <summary>UI 버튼 등 외부에서 자동 전투 토글을 요청할 때 발생시킵니다.</summary>
    public static event Action<bool> OnAutoBattleToggleRequested;

    [Tooltip("JC 전투의 현재 턴과 종료 상태를 읽는 흐름입니다. 본게임 흐름을 연결하지 않습니다.")]
    [SerializeField] private JcBattleFlowManager flowManager;
    [Tooltip("JC 전투의 스킬과 연출을 실행하는 컴포넌트입니다.")]
    [SerializeField] private JcBattleManager battleManager;
    [Tooltip("JC 대상 선택 입력입니다. 자동전투 중에는 수동 선택을 차단합니다.")]
    [SerializeField] private JcInputHandler inputHandler;

    [Header("Auto Battle")]
    [Tooltip("현재 JC 자동전투 상태입니다. 아군 행동을 자동 선택하며 자동전투 버튼으로 전환합니다.")]
    [SerializeField] private bool isAutoBattle = false;
    [Tooltip("자동전투가 대상 범위를 표시한 뒤 기다리는 초입니다. 배속에 따라 줄어들며 0은 지연 없음입니다.")]
    [SerializeField] private float thinkDelay = 0.5f;

    public bool IsAutoBattle
    {
        get => isAutoBattle;
        set
        {
            if (isAutoBattle == value)
            {
                JcBattleRuntimeSettings.SetAutoBattle(value);
                if (inputHandler != null)
                    inputHandler.IsAutoBattleActive = value;
                return;
            }

            isAutoBattle = value;
            JcBattleRuntimeSettings.SetAutoBattle(value);

            if (inputHandler != null)
                inputHandler.IsAutoBattleActive = value;

            // 타겟팅 선택 상태는 켤 때든 끌 때든 정리한다.
            // 끌 때 정리하지 않으면 자동전투 중 무장돼 있던 선택 상태가 되살아나 이중 행동으로 이어진다.
            inputHandler?.ClearSelectionState();

            if (value)
            {
                // 이미 플레이어 턴 WaitUntil 중이면 즉시 실행
                if (flowManager != null
                    && !flowManager.IsTurnPresentationPending
                    && flowManager.CurrentUnit != null
                    && flowManager.CurrentUnit.IsPlayer
                    && !flowManager.CurrentUnit.IsDead)
                {
                    StartCoroutine(RunAutoBattleTurn(flowManager.CurrentUnit));
                }
            }
        }
    }

    // 원본 함수 대응: AutoBattleController.OnEnable (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/AutoBattleController.cs)

    private void OnEnable()
    {
        if (flowManager != null)
            flowManager.OnTurnStarted += OnTurnStarted;

        OnAutoBattleToggleRequested += HandleToggleRequest;
        IsAutoBattle = JcBattleRuntimeSettings.IsAutoBattle;
    }

    // 원본 함수 대응: AutoBattleController.OnDisable (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/AutoBattleController.cs)

    private void OnDisable()
    {
        if (flowManager != null)
            flowManager.OnTurnStarted -= OnTurnStarted;

        OnAutoBattleToggleRequested -= HandleToggleRequest;

        if (inputHandler != null)
            inputHandler.IsAutoBattleActive = false;
    }

    // 원본 함수 대응: AutoBattleController.HandleToggleRequest (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/AutoBattleController.cs)

    private void HandleToggleRequest(bool enable)
    {
        IsAutoBattle = enable;
    }

    [ContextMenu("자동 전투 ON")]
    private void DebugEnableAutoBattle() => IsAutoBattle = true;

    [ContextMenu("자동 전투 OFF")]
    private void DebugDisableAutoBattle() => IsAutoBattle = false;

    // 원본 함수 대응: AutoBattleController.OnTurnStarted (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/AutoBattleController.cs)

    private void OnTurnStarted(int round, BattleCharactor unit)
    {
        if (!isAutoBattle) return;
        if (unit == null || !unit.IsPlayer || unit.IsDead) return;

        StartCoroutine(RunAutoBattleTurn(unit));
    }

    // 원본 함수 대응: AutoBattleController.RunAutoBattleTurn (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/AutoBattleController.cs)

    private IEnumerator RunAutoBattleTurn(BattleCharactor actor)
    {
        // 안내 중 자동 모드가 켜졌다면, 안내 후 OnTurnStarted가 정상적으로 시작한다.
        if (flowManager == null || flowManager.IsTurnPresentationPending || actor == null || actor.IsStunned)
            yield break;
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
        var selected = candidates[JC.BattleTesting.JcBattleRandom.Range(0, candidates.Count)];
        PendingActionType actionType = selected.actionType;
        BattleCharactor target = selected.targets[JC.BattleTesting.JcBattleRandom.Range(0, selected.targets.Count)];

        Debug.Log($"[AutoBattle] {actor.UnitName}: {actionType} → {target.UnitName}");

        actor.ResolveSelectedSkill();
        SkillData highlightSkill = actionType == PendingActionType.ClassSkill
            ? actor.SelectedSkillData
            : actor.EquippedWeaponData?.ToSkillData();

        // 타겟 발판 하이라이트 표시 후 대기
        flowManager?.ShowTargetHighlight(actor, target, highlightSkill);
        float speed = JcBattleManager.Instance != null ? JcBattleManager.Instance.CurrentBattleSpeed : 1f;
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

        if (!flowManager.IsPlayerActionAllowed(actor, actionType, target))
        {
            Debug.Log($"[AutoBattle] 현재 시나리오에서 허용되지 않은 행동이라 자동전투를 중단한다: action={actionType}");
            flowManager.ClearTargetHighlight();
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
