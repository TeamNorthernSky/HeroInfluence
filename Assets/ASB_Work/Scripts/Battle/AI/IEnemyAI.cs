using System.Collections.Generic;

namespace EnemyAI
{
    public interface IEnemyAI
    {
        int Index { get; }
        EnemyActionDecision DecideAction(BattleCharactor self, List<BattleCharactor> targets);
    }

    public enum EnemyActionType
    {
        BasicAttack,
        ClassSkill,
        WeaponSkill
    }

    public class EnemyActionDecision
    {
        public BattleCharactor Target;
        public EnemyActionType ActionType;
        public SkillData SelectedSkill;

        public bool Skip;

        public static EnemyActionDecision SkipTurn()
        {
            return new EnemyActionDecision
            {
                Skip = true,
                Target = null,
                ActionType = EnemyActionType.BasicAttack,
                SelectedSkill = null
            };
        }

        public static EnemyActionDecision Create(BattleCharactor target, EnemyActionType actionType, SkillData selectedSkill = null)
        {
            return new EnemyActionDecision
            {
                Skip = false,
                Target = target,
                ActionType = actionType,
                SelectedSkill = selectedSkill
            };
        }
    }
}