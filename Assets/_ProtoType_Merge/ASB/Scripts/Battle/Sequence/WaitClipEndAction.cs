using System;
using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 스킬 클립이 끝날 때까지 대기합니다.
    /// onElapsed 콜백으로 경과 시간을 보고합니다.
    /// </summary>
    public class WaitClipEndAction : BattleSequenceAction
    {
        private readonly CharactorAnimationController _anim;
        private readonly string _stateName;
        private readonly Action<float> _onElapsed;

        public WaitClipEndAction(
            CharactorAnimationController anim,
            string stateName,
            Action<float> onElapsed)
        {
            _anim = anim;
            _stateName = stateName;
            _onElapsed = onElapsed;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            if (_anim == null || string.IsNullOrEmpty(_stateName))
            {
                yield break;
            }

            yield return _anim.StartCoroutine(_anim.WaitForSkillClipEnd(_stateName));
            _onElapsed?.Invoke(_anim.LastClipWaitBattleSeconds);
        }
    }
}
