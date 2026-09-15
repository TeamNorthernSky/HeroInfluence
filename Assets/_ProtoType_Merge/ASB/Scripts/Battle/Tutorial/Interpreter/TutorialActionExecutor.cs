using UnityEngine;

/// <summary>
/// 모든 액션 실행을 한 곳에. 기존 런타임 기능을 호출만 한다. 새 액션 종류 = 여기 switch에 case 하나.
/// </summary>
public static class TutorialActionExecutor
{
    public static TutorialActionResult Execute(TutorialAction a, TutorialExecutionContext ctx, TutorialHookRegistry hooks)
    {
        if (a == null || ctx == null || ctx.Flow == null)
        {
            return TutorialActionResult.FailedContinue;
        }

        IDataDrivenTutorialFlow flow = ctx.Flow;
        ITutorialBattleFlowHost host = flow.Host;
        if (host == null)
        {
            return TutorialActionResult.FailedContinue;
        }

        string scope = ctx.ScopeId;

        switch (a.type)
        {
            case TutorialActionType.ShowUi:
                return host.ShowUi(a.uiKey) ? TutorialActionResult.Success : TutorialActionResult.FailedContinue;

            case TutorialActionType.ShowBlockingUi:
                return ExecuteShowBlocking(a, flow, host, scope);

            case TutorialActionType.HideUi:
                host.HideUi();
                return TutorialActionResult.Success;

            case TutorialActionType.ReleaseRuleEffects:
                host.ReleaseEffects(scope);
                return TutorialActionResult.Success;

            case TutorialActionType.AcquireFlowLock:
                host.Track(host.FlowManager.AcquireFlowLock(flow), a.lifetime, scope);
                return TutorialActionResult.Success;

            case TutorialActionType.Spawn:
                return ExecuteSpawn(a, host);

            case TutorialActionType.SetHp:
            {
                BattleCharactor u = flow.FindUnit(a.targetSide, a.targetTemplateId);
                if (u == null) { return TutorialActionResult.FailedContinue; }
                u.SetHp(a.floatValue);
                return TutorialActionResult.Success;
            }

            case TutorialActionType.SetInfluence:
            {
                BattleCharactor u = flow.FindUnit(a.targetSide, a.targetTemplateId);
                if (u == null) { return TutorialActionResult.FailedContinue; }
                u.SetInfluence(a.floatValue);
                return TutorialActionResult.Success;
            }

            case TutorialActionType.AddMinimumHp:
            {
                BattleCharactor u = flow.FindUnit(a.targetSide, a.targetTemplateId);
                if (u == null) { return TutorialActionResult.FailedContinue; }
                host.Track(u.AddMinimumHpConstraint(a.floatValue, flow), a.lifetime, scope);
                return TutorialActionResult.Success;
            }

            case TutorialActionType.AddStatModifier:
            {
                BattleCharactor u = flow.FindUnit(a.targetSide, a.targetTemplateId);
                if (u == null) { return TutorialActionResult.FailedContinue; }
                host.Track(u.AddRuntimeStatModifier(a.statModifier, flow), a.lifetime, scope);
                return TutorialActionResult.Success;
            }

            case TutorialActionType.SetStep:
                flow.SetStep(a.intValue);
                return TutorialActionResult.Success;

            case TutorialActionType.SetFlag:
                flow.SetFlag(a.stringValue);
                return TutorialActionResult.Success;

            case TutorialActionType.CustomHook:
                if (hooks != null && hooks.TryInvoke(a.stringValue, ctx, out TutorialActionResult r))
                {
                    return r;
                }
                Debug.LogWarning($"[Tutorial] CustomHook 미등록: {a.stringValue}");
                return TutorialActionResult.FailedContinue;

            case TutorialActionType.CompleteTutorial:
                flow.CompleteTutorial();
                return TutorialActionResult.Success;

            default:
                return TutorialActionResult.FailedContinue;
        }
    }

    // ShowBlockingUi 원자화(§5-②): 바인딩 검사 → 표시 → 성공시에만 잠금 (fail-open)
    private static TutorialActionResult ExecuteShowBlocking(
        TutorialAction a, IDataDrivenTutorialFlow flow, ITutorialBattleFlowHost host, string scope)
    {
        if (!string.IsNullOrWhiteSpace(a.requiredActionId) &&
            host.UI != null && !host.UI.HasActionBinding(a.requiredActionId))
        {
            Debug.LogWarning(
                $"[Tutorial] ShowBlockingUi: requiredActionId '{a.requiredActionId}' 바인딩 없음 → 표시/잠금 안 함(데드락 방지)");
            return TutorialActionResult.FailedContinue;
        }

        if (!host.ShowUi(a.uiKey))
        {
            return TutorialActionResult.FailedContinue;   // 표시 실패 → 잠금 안 함
        }

        host.Track(host.FlowManager.AcquireFlowLock(flow), a.lifetime, scope);
        flow.SetActiveUiRequest(new TutorialUiRequest(scope, a.uiKey, a.requiredActionId));
        return TutorialActionResult.Success;
    }

    // 스폰 + 등록. 등록 실패 시 rollback(v1: Destroy — 완전 정리 API는 §5-⑦ TODO).
    private static TutorialActionResult ExecuteSpawn(TutorialAction a, ITutorialBattleFlowHost host)
    {
        if (a.spawnSide != TutorialUnitSide.Enemy)
        {
            Debug.LogWarning("[Tutorial] Spawn: v1은 적(Enemy) 스폰만 지원. (아군 스폰은 PlayerSpawner 배선 후)");
            return TutorialActionResult.FailedContinue;
        }

        if (host.EnemySpawner == null)
        {
            return TutorialActionResult.FailedContinue;
        }

        if (!host.EnemySpawner.TrySpawnUnitIfEmpty(a.spawnUnitId, a.spawnGridNumber, out GameObject spawned) || spawned == null)
        {
            return TutorialActionResult.FailedContinue;
        }

        BattleCharactor bc = spawned.GetComponentInChildren<BattleCharactor>(true);
        if (bc == null || host.FlowManager == null || !host.FlowManager.RegisterRuntimeParticipant(bc))
        {
            // rollback(§5-⑦): grid 점유·룩업까지 정리(단순 Destroy는 잔류 유발).
            host.EnemySpawner.TryDespawnRuntimeUnit(spawned);
            return TutorialActionResult.FailedContinue;
        }

        return TutorialActionResult.Success;
    }
}
