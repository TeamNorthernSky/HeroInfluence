using System.Collections.Generic;
using System.Linq;

namespace ASB.Work.Battle.Core
{
    public interface ICombatEvent
    {
        string ExecutionId { get; }
        string ParentExecutionId { get; }
        string TriggeredByEventId { get; }
        int SkillIndex { get; }
    }

    public abstract class CombatEventBase : ICombatEvent
    {
        public string ExecutionId { get; }
        public string ParentExecutionId { get; }
        public string TriggeredByEventId { get; }
        public int SkillIndex { get; }

        protected CombatEventBase(
            string executionId,
            int skillIndex,
            string parentExecutionId = null,
            string triggeredByEventId = null)
        {
            ExecutionId = executionId;
            SkillIndex = skillIndex;
            ParentExecutionId = parentExecutionId;
            TriggeredByEventId = triggeredByEventId;
        }
    }

    public sealed class SkillCastEvent : CombatEventBase
    {
        public BattleCharactor Caster { get; }
        public SkillData Skill { get; }

        public SkillCastEvent(
            string executionId,
            BattleCharactor caster,
            SkillData skill,
            string parentExecutionId = null,
            string triggeredByEventId = null)
            : base(executionId, skill != null ? skill.skillIndex : 0, parentExecutionId, triggeredByEventId)
        {
            Caster = caster;
            Skill = skill;
        }
    }

    public sealed class DamageEvent : CombatEventBase
    {
        public BattleCharactor Caster { get; }
        public BattleCharactor Target { get; }
        public float Amount { get; }
        public bool IsCritical { get; }
        public bool WasDeadBefore { get; }
        public bool IsDeadAfter { get; }
        public bool CausedDeath { get; }

        public DamageEvent(
            string executionId,
            int skillIndex,
            BattleCharactor caster,
            BattleCharactor target,
            float amount,
            bool isCritical,
            bool wasDeadBefore,
            bool isDeadAfter,
            bool causedDeath,
            string parentExecutionId = null,
            string triggeredByEventId = null)
            : base(executionId, skillIndex, parentExecutionId, triggeredByEventId)
        {
            Caster = caster;
            Target = target;
            Amount = amount;
            IsCritical = isCritical;
            WasDeadBefore = wasDeadBefore;
            IsDeadAfter = isDeadAfter;
            CausedDeath = causedDeath;
        }
    }

    public sealed class HealEvent : CombatEventBase
    {
        public BattleCharactor Caster { get; }
        public BattleCharactor Target { get; }
        public float Amount { get; }

        public HealEvent(
            string executionId,
            int skillIndex,
            BattleCharactor caster,
            BattleCharactor target,
            float amount,
            string parentExecutionId = null,
            string triggeredByEventId = null)
            : base(executionId, skillIndex, parentExecutionId, triggeredByEventId)
        {
            Caster = caster;
            Target = target;
            Amount = amount;
        }
    }

    public sealed class StatusEffectAppliedEvent : CombatEventBase
    {
        public BattleCharactor Caster { get; }
        public BattleCharactor Target { get; }
        public StatusEffectType StatusType { get; }
        public int TurnCount { get; }

        public StatusEffectAppliedEvent(
            string executionId,
            int skillIndex,
            BattleCharactor caster,
            BattleCharactor target,
            StatusEffectType statusType,
            int turnCount,
            string parentExecutionId = null,
            string triggeredByEventId = null)
            : base(executionId, skillIndex, parentExecutionId, triggeredByEventId)
        {
            Caster = caster;
            Target = target;
            StatusType = statusType;
            TurnCount = turnCount;
        }
    }

    public sealed class DeathEvent : CombatEventBase
    {
        public BattleCharactor Source { get; }
        public BattleCharactor Target { get; }

        public DeathEvent(
            string executionId,
            int skillIndex,
            BattleCharactor source,
            BattleCharactor target,
            string parentExecutionId = null,
            string triggeredByEventId = null)
            : base(executionId, skillIndex, parentExecutionId, triggeredByEventId)
        {
            Source = source;
            Target = target;
        }
    }

    public sealed class CombatEventStream
    {
        private readonly List<ICombatEvent> _events = new List<ICombatEvent>();

        public IReadOnlyList<ICombatEvent> Events => _events;

        public void Add(ICombatEvent combatEvent)
        {
            if (combatEvent != null)
            {
                _events.Add(combatEvent);
            }
        }

        public int CountOf<TEvent>() where TEvent : ICombatEvent
        {
            return _events.Count(e => e is TEvent);
        }
    }
}
