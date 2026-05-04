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
    }
}