using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 궁수 전용: AniEvent_OnHit 직후 손의 화살을 숨기고 타겟 위치에 화살을 표시합니다.
    /// WaitHitAction과 ResolveHitAction 사이에 삽입됩니다.
    /// </summary>
    public class ArrowImpactAction : BattleSequenceAction
    {
        private readonly BattleCharactor _actor;
        private readonly BattleCharactor _target;

        public ArrowImpactAction(BattleCharactor actor, BattleCharactor target)
        {
            _actor = actor;
            _target = target;
        }

        public override IEnumerator ExecuteRoutine()
        {
            if (_target == null || _target.IsDead)
            {
                yield break;
            }

            UnitVisualProfile actorProfile = _actor?.GetComponent<UnitVisualProfile>();
            UnitVisualProfile targetProfile = _target.GetComponent<UnitVisualProfile>();

            actorProfile?.HoldArrow?.SetActive(false);

            if (actorProfile?.TargetArrow != null)
            {
                Transform hitPoint = targetProfile?.ArrowHitPoint ?? _target.transform;
                actorProfile.TargetArrow.transform.position = hitPoint.position;
                actorProfile.TargetArrow.transform.rotation = hitPoint.rotation;
                actorProfile.TargetArrow.SetActive(true);
            }

            yield break;
        }
    }
}
