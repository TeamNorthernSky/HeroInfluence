using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 스킬 애니메이션 재생 + HitEvent 리셋.
    /// </summary>
    public class PlaySkillAnimAction : BattleSequenceAction
    {
        private readonly CharactorAnimationController _anim;
        private readonly SkillData _skill;
        private readonly bool _playBasicAttack;

        private readonly BattleCharactor _actor;

        public PlaySkillAnimAction(CharactorAnimationController anim, SkillData skill, bool playBasicAttack, BattleCharactor actor = null)
        {
            _anim = anim;
            _skill = skill;
            _playBasicAttack = playBasicAttack;
            _actor = actor;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            // 궁수라면 애니 시작 전 HoldArrow 활성화. 미할당(궁수 아님)이면 스킵.
            // 주의: Unity 오브젝트는 미할당 시 '가짜 null'이라 '?.'로는 걸러지지 않는다 → '!= null' 체크 필수.
            UnitVisualProfile actorProfile = _actor != null ? _actor.GetComponent<UnitVisualProfile>() : null;
            if (actorProfile != null && actorProfile.HoldArrow != null)
            {
                actorProfile.HoldArrow.SetActive(true);
            }

            _anim?.ResetHitEvent();
            _anim?.BeginHitWait(); // 애니 재생 전 HitEventCount baseline 기록 (첫 프레임 이벤트 누락 방지)
            _anim?.PlaySkillAnimation(_playBasicAttack ? null : _skill);
            yield break;
        }
    }
}
