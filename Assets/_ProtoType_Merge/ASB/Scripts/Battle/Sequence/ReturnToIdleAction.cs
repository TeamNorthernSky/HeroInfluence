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

            // HoldArrow 복원 (TargetArrowPrefab은 Instantiate 후 자동 Destroy됨)
            UnitVisualProfile profile = _unit.GetComponent<UnitVisualProfile>();
            profile?.HoldArrow?.SetActive(true);

            _unit.EnsureAnimationController();
            _unit.Anim?.PlayIdleAnimation();
            yield break;
        }
    }
}
