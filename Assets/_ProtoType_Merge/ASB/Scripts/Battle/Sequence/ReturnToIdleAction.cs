using System.Collections;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 생존 유닛을 Idle 상태로 복귀시킵니다.
    /// </summary>
    public class ReturnToIdleAction : BattleSequenceAction
    {
        private readonly BattleCharactor _unit;

        public ReturnToIdleAction(BattleCharactor unit)
        {
            _unit = unit;
        }

        public override IEnumerator ExecuteRoutine()
        {
            if (_unit == null || _unit.IsDead)
            {
                yield break;
            }

            _unit.EnsureAnimationController();
            _unit.Anim?.PlayIdleAnimation();
            yield break;
        }
    }
}
