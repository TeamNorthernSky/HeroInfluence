using System;
using System.Collections;
using UnityEngine;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// HitDelay 또는 AnimEvent 방식으로 피격 타이밍까지 대기합니다.
    /// onElapsed 콜백으로 경과 시간을 RunSkillSequenceCore에 보고합니다.
    /// </summary>
    public class WaitHitAction : BattleSequenceAction
    {
        private readonly CharactorAnimationController _anim;
        private readonly SkillData _skill;
        private readonly float _battleSpeed;
        private readonly float _animEventTimeout;
        private readonly Action<float> _onElapsed;

        public WaitHitAction(
            CharactorAnimationController anim,
            SkillData skill,
            float battleSpeed,
            Action<float> onElapsed,
            float animEventTimeout = 2f)
        {
            _anim = anim;
            _skill = skill;
            _battleSpeed = battleSpeed;
            _onElapsed = onElapsed;
            _animEventTimeout = animEventTimeout;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            float elapsed = 0f;

            if (_skill.UseAnimEvent)
            {
                while (_anim != null && !_anim.IsHitEventReached && elapsed < _animEventTimeout)
                {
                    elapsed += Time.deltaTime * _battleSpeed;
                    yield return null;
                }

                _onElapsed?.Invoke(Mathf.Min(elapsed, _animEventTimeout));
            }
            else
            {
                float hitDelay = Mathf.Max(0f, _skill.HitDelay);
                while (elapsed < hitDelay)
                {
                    elapsed += Time.deltaTime * _battleSpeed;
                    yield return null;
                }

                _onElapsed?.Invoke(hitDelay);
            }
        }
    }
}
