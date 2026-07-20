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
            // 궁수라면 애니 시작 전 HoldArrow 활성화
            _actor?.GetComponent<UnitVisualProfile>()?.HoldArrow?.SetActive(true);

            _anim?.ResetHitEvent();
            _anim?.BeginHitWait(); // 애니 재생 전 HitEventCount baseline 기록 (첫 프레임 이벤트 누락 방지)
            _anim?.PlaySkillAnimation(_playBasicAttack ? null : _skill);
            yield break;
        }
    }
}
