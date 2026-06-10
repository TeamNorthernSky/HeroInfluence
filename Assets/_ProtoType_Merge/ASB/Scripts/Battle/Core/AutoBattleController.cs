using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자동 전투 컨트롤러.
/// 플레이어 턴에 랜덤 액션/타겟을 선택해 BattleManager로 실행합니다.
/// InputHandler.IsAutoBattleActive 플래그로 수동 입력을 차단합니다.
/// </summary>
[DisallowMultipleComponent]
public class AutoBattleController : MonoBehaviour
{
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
            isAutoBattle = value;
            if (inputHandler != null)
                inputHandler.IsAutoBattleActive = value;
        }
    }

    private void OnEnable()
    {
        if (flowManager != null)
            flowManager.OnTurnStarted += OnTurnStarted;
    }

    private void OnDisable()
    {
        if (flowManager != null)
            flowManager.OnTurnStarted -= OnTurnStarted;

        if (inputHandler != null)
            inputHandler.IsAutoBattleActive = false;
    }

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

        yield return new WaitForSeconds(thinkDelay);

        // 유효한 액션 후보 수집 (타겟 존재 + Influence 충분)
        var candidates = new List<(PendingActionType actionType, List<BattleCharactor> targets)>();

        // BasicAttack
        var basicTargets = new List<BattleCharactor>(
            TargetingHelper.GetValidTargets(actor, PendingActionType.BasicAttack));
        if (basicTargets.Count > 0)
            candidates.Add((PendingActionType.BasicAttack, basicTargets));

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

        // 랜덤 액션 선택
        var selected = candidates[Random.Range(0, candidates.Count)];
        PendingActionType actionType = selected.actionType;
        BattleCharactor target = selected.targets[Random.Range(0, selected.targets.Count)];

        Debug.Log($"[AutoBattle] {actor.UnitName}: {actionType} → {target.UnitName}");

        // 실행
        bool executed = false;
        switch (actionType)
        {
            case PendingActionType.BasicAttack:
                yield return StartCoroutine(
                    battleManager.ExecuteBasicAttack(actor, target, success => executed = success));
                break;

            case PendingActionType.ClassSkill:
                yield return StartCoroutine(
                    battleManager.ExecuteGridSkill(actor, target, actor.SelectedSkillData,
                        success => executed = success));
                break;

            case PendingActionType.WeaponSkill:
                var skill = actor.EquippedWeaponData.ToSkillData();
                yield return StartCoroutine(
                    battleManager.ExecuteGridSkill(actor, target, skill,
                        success => executed = success));
                break;
        }

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
