using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// Schema=1 AttackBeat 콤보를 순서대로 재생한다. Beat 사이 Animator transition 대신
    /// 시퀀서가 각 Beat의 Cue 컨텍스트를 등록한 뒤 CrossFade를 실행한다.
    /// </summary>
    public sealed class ComboSkillAction : BattleSequenceAction
    {
        private readonly CharactorAnimationController _anim;
        private readonly List<AttackBeat> _beats;
        private readonly Func<AttackBeat, int, string> _resolveState;
        private readonly Action<AttackBeat, string> _activateContext;
        private readonly Func<MonoBehaviour, IEnumerator> _resolveFirstHit;
        private readonly float _battleSpeed;
        private readonly float _eventTimeout;
        private readonly Action<float> _onElapsed;

        public ComboSkillAction(CharactorAnimationController anim, List<AttackBeat> beats,
            Func<AttackBeat, int, string> resolveState, Action<AttackBeat, string> activateContext,
            Func<MonoBehaviour, IEnumerator> resolveFirstHit, float battleSpeed, float eventTimeout,
            Action<float> onElapsed)
        {
            _anim = anim;
            _beats = beats;
            _resolveState = resolveState;
            _activateContext = activateContext;
            _resolveFirstHit = resolveFirstHit;
            _battleSpeed = Mathf.Max(0.01f, battleSpeed);
            _eventTimeout = Mathf.Max(0.01f, eventTimeout);
            _onElapsed = onElapsed;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            if (_anim == null || _beats == null || _beats.Count == 0) yield break;

            bool resolvedHit = false;
            for (int i = 0; i < _beats.Count; i++)
            {
                AttackBeat beat = _beats[i];
                if (beat == null) continue;

                string stateName = _resolveState?.Invoke(beat, i);
                if (string.IsNullOrWhiteSpace(stateName))
                {
                    Debug.LogWarning($"[ComboSkillAction] Beat {i} has no valid AnimationStateName. Combo stopped.");
                    yield break;
                }

                _activateContext?.Invoke(beat, stateName);
                _anim.ResetHitEvent();
                _anim.BeginHitWait();
                _anim.BeginComboWait();
                _anim.PlayState(stateName, Mathf.Max(0f, beat.BlendInSeconds));

                if (beat.WaitForHitEvent)
                {
                    float elapsed = 0f;
                    while (!_anim.HasHitSinceWaitBegan && elapsed < _eventTimeout)
                    {
                        elapsed += Time.deltaTime * _battleSpeed;
                        yield return null;
                    }
                    _onElapsed?.Invoke(Mathf.Min(elapsed, _eventTimeout));
                }

                if (!resolvedHit)
                {
                    resolvedHit = true;
                    IEnumerator hitRoutine = _resolveFirstHit?.Invoke(host);
                    if (hitRoutine != null) yield return hitRoutine;
                }

                if (i < _beats.Count - 1)
                {
                    float elapsed = 0f;
                    while (!_anim.HasComboAdvancedSinceWaitBegan && elapsed < _eventTimeout)
                    {
                        elapsed += Time.deltaTime * _battleSpeed;
                        yield return null;
                    }
                    _onElapsed?.Invoke(Mathf.Min(elapsed, _eventTimeout));
                    if (!_anim.HasComboAdvancedSinceWaitBegan)
                    {
                        Debug.LogWarning($"[ComboSkillAction] Beat {i} did not send AniEvent_AdvanceCombo before timeout. Remaining beats skipped.");
                        yield break;
                    }
                }
                else
                {
                    yield return _anim.WaitForSkillClipEnd(stateName);
                    _onElapsed?.Invoke(_anim.LastClipWaitBattleSeconds);
                }
            }
        }
    }
}
