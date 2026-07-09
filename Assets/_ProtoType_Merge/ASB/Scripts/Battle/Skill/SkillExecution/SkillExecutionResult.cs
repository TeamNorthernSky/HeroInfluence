using System;
using System.Collections.Generic;
using ASB.Work.Battle.Core;
using UnityEngine;

#nullable enable // 코드 최상단에 추가

namespace ASB.Work.Battle.SkillExecution
{
    public class SkillExecutionResult
    {
        public bool Success { get; private set; }
        public List<DamageContext> DamageContexts { get; private set; } = new List<DamageContext>();
        public List<HealContext> HealContexts { get; private set; } = new List<HealContext>();
        public List<StatusEffectContext> StatusEffectContexts { get; private set; } = new List<StatusEffectContext>();
        public CombatEventStream EventStream { get; private set; } = new CombatEventStream();
        public IReadOnlyList<ICombatEvent> Events => EventStream.Events;
        public bool HasEventTracking { get; private set; }
        public string ExecutionId { get; private set; } = string.Empty;
        public string? ParentExecutionId { get; private set; }
        public string? TriggeredByEventId { get; private set; }
        public BattleCharactor? Caster { get; private set; }
        public SkillData? Skill { get; private set; }

        /// <summary>스킬 실행에 사용된 핸들러. 광역(AoE) 연출 분기 등에 사용합니다.</summary>
        public ISkillEffectHandler Handler { get; set; }

        // 총 가해진 데미지(전투 계산 후 실제 적용된 값)를 전달합니다.
        // (예: 흡혈, 누적 반응 등 사후 처리)

        public Action<float>? OnPostExecution { get; set; } 

        public static SkillExecutionResult SuccessResult()
        {
            return new SkillExecutionResult { Success = true, HasEventTracking = false };
        }

        public static SkillExecutionResult SuccessResult(
            BattleCharactor caster,
            SkillData skill,
            string? parentExecutionId = null,
            string? triggeredByEventId = null)
        {
            var result = new SkillExecutionResult
            {
                Success = true,
                HasEventTracking = true,
                ExecutionId = Guid.NewGuid().ToString("N"),
                ParentExecutionId = parentExecutionId,
                TriggeredByEventId = triggeredByEventId,
                Caster = caster,
                Skill = skill
            };

            result.EventStream.Add(new SkillCastEvent(
                result.ExecutionId,
                caster,
                skill,
                parentExecutionId,
                triggeredByEventId));
            return result;
        }

        public static SkillExecutionResult Failed()
        {
            return new SkillExecutionResult { Success = false };
        }

        public SkillExecutionResult AddDamage(DamageContext context)
        {
            if (context.Caster != null && context.Target != null)
            {
                DamageContexts.Add(context);
                Success = true;
            }

            return this;
        }

        public void RecordDamageResult(DamageContext context, BattleHitResult hitResult)
        {
            if (!HasEventTracking || context == null || hitResult == null)
            {
                return;
            }

            EventStream.Add(new DamageEvent(
                ExecutionId,
                hitResult.SkillIndex,
                context.Caster,
                hitResult.Target,
                hitResult.Damage,
                hitResult.IsCritical,
                hitResult.WasDeadBefore,
                hitResult.IsDeadAfter,
                hitResult.CausedDeath,
                ParentExecutionId,
                TriggeredByEventId));

            // DeathEvent rule: create it only when this resolved hit changed a living target into a dead target.
            // AoE deaths belong to each target hit. Revive/ReviveEvent is intentionally left for a later phase.
            if (hitResult.CausedDeath)
            {
                EventStream.Add(new DeathEvent(
                    ExecutionId,
                    hitResult.SkillIndex,
                    context.Caster,
                    hitResult.Target,
                    ParentExecutionId,
                    TriggeredByEventId));
            }
        }

        public SkillExecutionResult AddHeal(BattleCharactor caster, BattleCharactor target, float amount, int skillIndex = 0)
        {
            if (caster != null && target != null && amount > 0f)
            {
                HealContexts.Add(new HealContext
                {
                    Caster = caster,
                    Target = target,
                    HealAmount = amount,
                    SkillIndex = skillIndex
                });
                if (HasEventTracking)
                {
                    EventStream.Add(new HealEvent(
                        ExecutionId,
                        skillIndex,
                        caster,
                        target,
                        amount,
                        ParentExecutionId,
                        TriggeredByEventId));
                }

                Success = true;
            }

            return this;
        }

        public SkillExecutionResult AddStatusEffect(BattleCharactor caster, BattleCharactor target, StatusEffectType effectType, int durationTurn)
        {
            if (target != null && effectType != StatusEffectType.none)
            {
                StatusEffectContexts.Add(new StatusEffectContext
                {
                    Caster = caster,
                    Target = target,
                    EffectType = effectType,
                    DurationTurn = durationTurn
                });
                if (HasEventTracking)
                {
                    EventStream.Add(new StatusEffectAppliedEvent(
                        ExecutionId,
                        Skill != null ? Skill.skillIndex : 0,
                        caster,
                        target,
                        effectType,
                        durationTurn,
                        ParentExecutionId,
                        TriggeredByEventId));
                }
            }

            return this;
        }

        public void LogEventTrackingSummary()
        {
            if (!HasEventTracking)
            {
                Debug.Log("[CombatEvent] Event tracking disabled for this SkillExecutionResult.");
                return;
            }

            Debug.Log(
                $"[CombatEvent] execution={ExecutionId}, damageContexts={DamageContexts.Count}, " +
                $"healContexts={HealContexts.Count}, statusContexts={StatusEffectContexts.Count}, " +
                $"events={Events.Count}, skillCast={EventStream.CountOf<SkillCastEvent>()}, " +
                $"damage={EventStream.CountOf<DamageEvent>()}, heal={EventStream.CountOf<HealEvent>()}, " +
                $"status={EventStream.CountOf<StatusEffectAppliedEvent>()}, death={EventStream.CountOf<DeathEvent>()}");
        }
    }
}

