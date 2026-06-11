using System.Collections;

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

        public PlaySkillAnimAction(CharactorAnimationController anim, SkillData skill, bool playBasicAttack)
        {
            _anim = anim;
            _skill = skill;
            _playBasicAttack = playBasicAttack;
        }

        public override IEnumerator ExecuteRoutine()
        {
            _anim?.ResetHitEvent();
            _anim?.PlaySkillAnimation(_playBasicAttack ? null : _skill);
            yield break;
        }
    }
}
