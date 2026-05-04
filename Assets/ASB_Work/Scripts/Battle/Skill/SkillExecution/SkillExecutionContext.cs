using System.Collections.Generic;
using ASB.Work.BattleGrid;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;

namespace ASB.Work.Battle.SkillExecution
{
    public sealed class SkillExecutionContext
    {
        public BattleCharactor Caster;
        public SkillData Skill;

        // 입력 단계
        public BattleCharactor SelectedTarget;
        public ASBGridCell SelectedCell;

        // 해석 단계
        public BattleCharactor PrimaryTarget;
        public ASBGridCell PrimaryCell;

        // 확장 단계
        public List<ASBGridCell> ResolvedCells = new List<ASBGridCell>();
        public List<BattleCharactor> ResolvedTargets = new List<BattleCharactor>();
    }
}
