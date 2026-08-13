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
        ClassSkill,
        WeaponSkill
    }

    public class EnemyActionDecision
    {
        public BattleCharactor Target;
        public EnemyActionType ActionType;
        public SkillData SelectedSkill;

        public bool Skip;

        // 신규: 스킬 실행 없이 턴을 소비하는 자기행동(충전 시작/응축/불발).
        // Skip이 아니므로 BaseEnemyAI의 도발 폴백 재호출을 유발하지 않는다.
        public bool IsSelfAction;

        // 신규: 최종 결정 확정 후 EnemyScript.RunAITurn에서 정확히 1회 실행되는 상태변경.
        // DetermineSpecificAction을 순수 판정으로 유지하기 위한 커밋 훅.
        public System.Action Commit;

        public static EnemyActionDecision SkipTurn()
        {
            return new EnemyActionDecision
            {
                Skip = true,
                Target = null,
                ActionType = EnemyActionType.ClassSkill,
                SelectedSkill = null
            };
        }

        public static EnemyActionDecision Create(BattleCharactor target, EnemyActionType actionType, SkillData selectedSkill = null, System.Action commit = null)
        {
            return new EnemyActionDecision
            {
                Skip = false,
                Target = target,
                ActionType = actionType,
                SelectedSkill = selectedSkill,
                Commit = commit
            };
        }

        // 대상 없는 자기행동(충전 시작/응축/불발). 스킬 실행 없이 턴을 소비하며 커밋을 1회 실행한다.
        public static EnemyActionDecision SelfAction(System.Action commit)
        {
            return new EnemyActionDecision
            {
                Skip = false,
                IsSelfAction = true,
                Target = null,
                ActionType = EnemyActionType.ClassSkill,
                SelectedSkill = null,
                Commit = commit
            };
        }
    }
}