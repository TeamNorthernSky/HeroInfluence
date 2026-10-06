// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillExecutionContext.cs
// 원본 객체: SkillExecutionContext -> JcSkillExecutionContext
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using System.Collections.Generic;
using ASB.Work.BattleGrid;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;

namespace ASB.Work.Battle.SkillExecution
{
    public sealed class JcSkillExecutionContext
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
