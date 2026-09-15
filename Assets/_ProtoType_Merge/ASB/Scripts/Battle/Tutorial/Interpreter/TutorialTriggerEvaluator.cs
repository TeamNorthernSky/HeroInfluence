using System;
using System.Linq;
using ASB.Work.Battle.Core;

/// <summary>
/// 모든 트리거 판정을 한 곳에. DataDrivenTutorialFlow가 규칙마다 Matches를 호출한다.
/// 새 트리거 종류 = 여기 switch에 case 하나. (순수 — ctx에 담긴 상태만으로 판정)
/// </summary>
public static class TutorialTriggerEvaluator
{
    public static bool Matches(TutorialTrigger t, TutorialEventContext ctx)
    {
        if (t == null || ctx == null || t.type != ctx.eventType)
        {
            return false;
        }

        if (!MatchesStep(t, ctx) || !MatchesFlag(t, ctx))
        {
            return false;
        }

        switch (t.type)
        {
            case TutorialTriggerType.Immediate:
            case TutorialTriggerType.BattleEntered:
            case TutorialTriggerType.TurnResolved:
            case TutorialTriggerType.BattleResultShown:
            case TutorialTriggerType.BattleResultAccepted:
                return true;

            case TutorialTriggerType.RoundStarted:
                return MatchesRound(t, ctx.round);

            case TutorialTriggerType.UnitTurnStarted:
                return MatchesRound(t, ctx.round) &&
                       TutorialUnitMatcher.MatchesTemplateId(ctx.unit, t.side, t.unitTemplateId);

            case TutorialTriggerType.SkillResolved:
                return MatchesSkill(t, ctx.skill);

            case TutorialTriggerType.UnitDied:
            case TutorialTriggerType.ParticipantRegistered:
                return TutorialUnitMatcher.MatchesTemplateId(ctx.unit, t.side, t.unitTemplateId);

            case TutorialTriggerType.UnitHpAtOrBelow:
                if (!TutorialUnitMatcher.MatchesTemplateId(ctx.unit, t.side, t.unitTemplateId) || ctx.maxHp <= 0f)
                {
                    return false;
                }
                return ctx.currentHp / ctx.maxHp <= t.hpRatioAtOrBelow;

            case TutorialTriggerType.AliveCountAtOrBelow:
                if (t.aliveCountAtOrBelow < 0)
                {
                    return false;
                }
                int count = t.side == TutorialUnitSide.Player ? ctx.alivePlayerCount : ctx.aliveEnemyCount;
                return count <= t.aliveCountAtOrBelow;

            case TutorialTriggerType.UIAction:
                return string.IsNullOrWhiteSpace(t.uiActionId) ||
                       string.Equals(t.uiActionId.Trim(), ctx.actionId, StringComparison.Ordinal);

            default:
                return false;
        }
    }

    private static bool MatchesRound(TutorialTrigger t, int round)
        => (t.minRound < 0 || round >= t.minRound) && (t.maxRound < 0 || round <= t.maxRound);

    private static bool MatchesStep(TutorialTrigger t, TutorialEventContext ctx)
    {
        if (t.requiredStep >= 0)
        {
            return ctx.currentStep == t.requiredStep;
        }
        if (t.minStep >= 0 && ctx.currentStep < t.minStep)
        {
            return false;
        }
        if (t.maxStep >= 0 && ctx.currentStep > t.maxStep)
        {
            return false;
        }
        return true;
    }

    private static bool MatchesFlag(TutorialTrigger t, TutorialEventContext ctx)
    {
        if (string.IsNullOrWhiteSpace(t.requireFlag))
        {
            return true;
        }
        return ctx.flags != null && ctx.flags.Contains(t.requireFlag.Trim());
    }

    private static bool MatchesSkill(TutorialTrigger t, SkillResolutionContext skill)
    {
        if (skill == null || !skill.Success ||
            !TutorialUnitMatcher.MatchesTemplateId(skill.Actor, t.side, t.unitTemplateId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(t.skillKey))
        {
            string resolved = skill.Skill != null ? skill.Skill.skillKey : string.Empty;
            if (!string.Equals(t.skillKey.Trim(), resolved, StringComparison.Ordinal))
            {
                return false;
            }
        }

        // 대상 지정 시: 지정 대상만 판정(AoE에서 다른 대상 사망 오판 방지)
        if (!string.IsNullOrWhiteSpace(t.targetTemplateId))
        {
            var hits = skill.Hits;
            bool ok = false;
            for (int i = 0; hits != null && i < hits.Count; i++)
            {
                BattleHitResult hit = hits[i];
                if (hit == null || !TutorialUnitMatcher.MatchesTemplateId(hit.Target, TutorialUnitSide.Any, t.targetTemplateId))
                {
                    continue;
                }
                ok = !t.requireTargetDeath || hit.CausedDeath;
                if (ok)
                {
                    break;
                }
            }
            return ok;
        }

        return !t.requireTargetDeath || skill.CausedAnyDeath;
    }
}
