using System.Collections.Generic;
using ASB.Work.Battle.Core;
using ASB.Work.Battle.SkillExecution;

/// <summary>
/// 최상위 스킬 하나가 연출과 후속 행동까지 끝난 뒤 전달되는 읽기 전용 결과입니다.
/// 기존 PlayerSkillActionResolved의 턴 종료 의미와 분리되어 있습니다.
/// </summary>
public sealed class SkillResolutionContext
{
    public BattleCharactor Actor { get; }
    public BattleCharactor PrimaryTarget { get; }
    public SkillData Skill { get; }
    public bool WasPlayerAction => Actor != null && Actor.IsPlayer;
    public bool Success { get; }
    public SkillExecutionResult ExecutionResult { get; }
    public string ExecutionId =>
        ExecutionResult != null ? ExecutionResult.ExecutionId : string.Empty;

    // OnSkillResolved는 ApplySkillExecutionResultRoutine의 후속 큐 드레인 이후 발행됩니다.
    public bool IncludesFollowUpActions => true;
    public IReadOnlyList<BattleHitResult> Hits =>
        ExecutionResult != null ? ExecutionResult.ResolvedHits : EmptyHits;

    public float TotalAttemptedDamage { get; }
    public float TotalAppliedDamage { get; }
    public bool CausedAnyDeath { get; }

    private static readonly IReadOnlyList<BattleHitResult> EmptyHits =
        new List<BattleHitResult>();

    public SkillResolutionContext(
        BattleCharactor actor,
        BattleCharactor primaryTarget,
        SkillData skill,
        bool success,
        SkillExecutionResult executionResult)
    {
        Actor = actor;
        PrimaryTarget = primaryTarget;
        Skill = skill;
        Success = success;
        ExecutionResult = executionResult;

        if (executionResult == null)
        {
            return;
        }

        IReadOnlyList<BattleHitResult> hits = executionResult.ResolvedHits;
        for (int i = 0; i < hits.Count; i++)
        {
            BattleHitResult hit = hits[i];
            if (hit == null || hit.IsHeal)
            {
                continue;
            }

            TotalAttemptedDamage += hit.Damage;
            TotalAppliedDamage += hit.AppliedDamage;
            CausedAnyDeath |= hit.CausedDeath;
        }
    }
}
