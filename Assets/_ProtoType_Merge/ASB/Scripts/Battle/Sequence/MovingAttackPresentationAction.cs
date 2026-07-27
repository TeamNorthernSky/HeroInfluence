using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    public sealed class MovingAttackHitTarget
    {
        public BattleCharactor Target;
        public Func<MonoBehaviour, IEnumerator> HitRoutine;
    }

    /// <summary>Entry → Mid → Exit만을 스핀 애니 normalizedTime으로 통과한다.</summary>
    public sealed class MovingAttackPresentationAction : BattleSequenceAction
    {
        private const float StateEnterTimeoutBattleSeconds = 1f;

        private readonly BattleCharactor _actor;
        private readonly CharactorAnimationController _animation;
        private readonly MovingAttackPresentation _presentation;
        private readonly MovingAttackPath.Points _points;
        private readonly Vector3 _originPosition;
        private readonly Action _setupPresentationContext;
        private readonly Func<MonoBehaviour, IEnumerator> _primaryHitRoutine;
        private readonly List<MovingAttackHitTarget> _sequentialHits;
        private readonly float _battleSpeed;
        private readonly float _nextBlendInSeconds;
        private readonly Action<float> _onElapsed;

        private Vector3[] _samples;
        private float[] _cumulativeLengths;
        private int _sampleCount;

        public MovingAttackPresentationAction(BattleCharactor actor, CharactorAnimationController animation,
            MovingAttackPresentation presentation, MovingAttackPath.Points points, Vector3 originPosition,
            Action setupPresentationContext, Func<MonoBehaviour, IEnumerator> primaryHitRoutine,
            List<MovingAttackHitTarget> sequentialHits, float battleSpeed, float nextBlendInSeconds,
            Action<float> onElapsed)
        {
            _actor = actor;
            _animation = animation;
            _presentation = presentation;
            _points = points;
            _originPosition = originPosition;
            _setupPresentationContext = setupPresentationContext;
            _primaryHitRoutine = primaryHitRoutine;
            _sequentialHits = sequentialHits;
            _battleSpeed = Mathf.Max(0.01f, battleSpeed);
            _nextBlendInSeconds = Mathf.Max(0f, nextBlendInSeconds);
            _onElapsed = onElapsed;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            if (_actor == null || _animation == null || _presentation == null
                || string.IsNullOrWhiteSpace(_presentation.AnimationStateName))
            {
                yield break;
            }

            BuildArcLengthLookup();
            if (_cumulativeLengths[_sampleCount] <= Mathf.Epsilon)
            {
                yield break;
            }

            _setupPresentationContext?.Invoke();
            string stateName = _presentation.AnimationStateName.Trim();
            _animation.BeginHitWait();
            _animation.PlayState(stateName, Mathf.Max(0f, _presentation.AnimationBlendInSeconds));

            float elapsed = 0f;
            float enterElapsed = 0f;
            float normalized = 0f;
            float stateLength = 0f;
            while (enterElapsed < StateEnterTimeoutBattleSeconds
                && !_animation.TryGetCurrentStateProgress(stateName, out normalized, out stateLength))
            {
                if (IsCancelled())
                {
                    CancelToOrigin();
                    _onElapsed?.Invoke(elapsed);
                    yield break;
                }

                yield return null;
                float dt = Time.deltaTime * _battleSpeed;
                elapsed += dt;
                enterElapsed += dt;
            }

            if (!_animation.TryGetCurrentStateProgress(stateName, out normalized, out stateLength))
            {
                _onElapsed?.Invoke(elapsed);
                yield break;
            }

            float exitNormalized = CalculateExitNormalized(stateLength);
            bool primaryHitApplied = false;
            bool[] sequentialApplied = _sequentialHits == null ? null : new bool[_sequentialHits.Count];
            float[] sequentialThresholds = BuildSequentialHitProgress();

            while (true)
            {
                if (IsCancelled())
                {
                    CancelToOrigin();
                    break;
                }

                if (!_animation.TryGetCurrentStateProgress(stateName, out normalized, out stateLength))
                {
                    break;
                }

                float pathProgress = Mathf.Clamp01(normalized / exitNormalized);
                SetActorPosition(pathProgress);

                if (_presentation.HitMode == MovingAttackHitMode.SequentialAoE)
                {
                    TriggerSequentialHits(host, pathProgress, sequentialThresholds, sequentialApplied);
                }
                else if (!primaryHitApplied && ShouldTriggerPrimaryHit(pathProgress))
                {
                    primaryHitApplied = true;
                    StartHitRoutine(host, _primaryHitRoutine);
                }

                if (normalized >= exitNormalized || pathProgress >= 0.999f)
                {
                    SetActorPosition(1f);
                    if (_presentation.HitMode != MovingAttackHitMode.SequentialAoE && !primaryHitApplied)
                    {
                        StartHitRoutine(host, _primaryHitRoutine);
                    }
                    else if (_presentation.HitMode == MovingAttackHitMode.SequentialAoE)
                    {
                        TriggerSequentialHits(host, 1f, sequentialThresholds, sequentialApplied);
                    }
                    break;
                }

                yield return null;
                elapsed += Time.deltaTime * _battleSpeed;
            }

            _onElapsed?.Invoke(elapsed);
        }

        private void BuildArcLengthLookup()
        {
            _sampleCount = Mathf.Max(48, _presentation.PathSampleCount);
            _samples = new Vector3[_sampleCount + 1];
            _cumulativeLengths = new float[_sampleCount + 1];
            MovingAttackPath.BuildArcLengthLut(_points, _sampleCount, _samples, _cumulativeLengths);
        }

        private bool IsCancelled()
        {
            return _actor == null || !_actor.gameObject.activeInHierarchy || _actor.IsDead;
        }

        private void CancelToOrigin()
        {
            if (_actor == null) return;
            _actor.transform.position = _originPosition;
            _animation?.PlayIdleAnimation();
        }

        private float CalculateExitNormalized(float stateLength)
        {
            if (_nextBlendInSeconds <= 0f || stateLength <= Mathf.Epsilon) return 1f;
            return Mathf.Clamp(1f - _nextBlendInSeconds / stateLength, 0.05f, 1f);
        }

        private bool ShouldTriggerPrimaryHit(float pathProgress)
        {
            return _presentation.HitMode == MovingAttackHitMode.SingleAoE
                && _presentation.SingleAoEHitPathProgress >= 0f
                ? pathProgress >= _presentation.SingleAoEHitPathProgress
                : _animation.HasHitSinceWaitBegan;
        }

        private void SetActorPosition(float progress)
        {
            Vector3 position = MovingAttackPath.EvaluateArcLength(_samples, _cumulativeLengths, _sampleCount, progress);
            _actor.transform.position = position;

            if (!_presentation.FaceCurveTangent) return;
            Vector3 before = MovingAttackPath.EvaluateArcLength(_samples, _cumulativeLengths, _sampleCount,
                Mathf.Max(0f, progress - 0.005f));
            Vector3 after = MovingAttackPath.EvaluateArcLength(_samples, _cumulativeLengths, _sampleCount,
                Mathf.Min(1f, progress + 0.005f));
            Vector3 direction = after - before;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                _actor.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        private float[] BuildSequentialHitProgress()
        {
            if (_sequentialHits == null || _sequentialHits.Count == 0) return Array.Empty<float>();
            var result = new float[_sequentialHits.Count];
            for (int i = 0; i < _sequentialHits.Count; i++)
            {
                Transform target = _sequentialHits[i]?.Target != null ? _sequentialHits[i].Target.transform : null;
                result[i] = target == null
                    ? 1f
                    : MovingAttackPath.FindNearestProgress(_samples, _cumulativeLengths, _sampleCount, target.position);
            }
            return result;
        }

        private static void StartHitRoutine(MonoBehaviour host, Func<MonoBehaviour, IEnumerator> routine)
        {
            if (host != null && routine != null) host.StartCoroutine(routine(host));
        }

        private void TriggerSequentialHits(MonoBehaviour host, float progress, float[] thresholds, bool[] applied)
        {
            if (_sequentialHits == null || thresholds == null || applied == null) return;
            for (int i = 0; i < _sequentialHits.Count; i++)
            {
                if (applied[i] || progress < thresholds[i]) continue;
                applied[i] = true;
                StartHitRoutine(host, _sequentialHits[i]?.HitRoutine);
            }
        }
    }
}
