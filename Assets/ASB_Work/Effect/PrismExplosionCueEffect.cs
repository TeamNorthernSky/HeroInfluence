using System.Collections;
        using System.Collections.Generic;
        using JC.VFX;
        using UnityEngine;
        
        [DisallowMultipleComponent]
        public sealed class PrismExplosionCueEffect : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle
        {
            [SerializeField] private PrismExplosionVfx _prismExplosion;
        
            private SkillEffectContext _context;
            private Coroutine _impactSignalRoutine;
            private readonly List<Transform> _targets = new List<Transform>();
            private Transform _launchOrigin;
            private bool _launched;
            private bool _impactNotified;
        
            private void Awake()
            {
                if (_prismExplosion == null)
                {
                    _prismExplosion = GetComponent<PrismExplosionVfx>();
                }
            }
        
            public void Play(SkillEffectContext context)
            {
                _context = context;
                _launched = false;
                _impactNotified = false;
                _targets.Clear();
        
                if (_prismExplosion == null || context == null)
                {
                    return;
                }
        
                _launchOrigin = context.SocketTransform != null
                    ? context.SocketTransform
                    : context.Caster != null
                        ? context.Caster.transform
                        : transform;
        
                Transform target = context.PrimaryTarget != null ? context.PrimaryTarget.transform : null;
                _prismExplosion.Play(_launchOrigin, target);
                // Launch is deliberately deferred. BattleManager calls Signal() on AniEvent_OnHit.
            }
        
            public bool Signal(SkillEffectContext context)
            {
                if (_prismExplosion == null || _launched)
                {
                    return false;
                }
        
                Launch(context ?? _context);
                return _launched;
            }
        
            public void Stop()
            {
                if (_impactSignalRoutine != null)
                {
                    StopCoroutine(_impactSignalRoutine);
                    _impactSignalRoutine = null;
                }
        
                if (!_impactNotified && _context != null)
                {
                    SkillImpactSignalBus.PublishCancelled(
                        _context.ActionInstanceId,
                        _targets,
                        transform.position);
                }
        
                _prismExplosion?.Stop();
                Destroy(gameObject);
            }
        
            private void Launch(SkillEffectContext context)
            {
                if (_launched || _prismExplosion == null)
                {
                    return;
                }
        
                _launched = true;
                BuildTargets(context);
                _prismExplosion.Launch(_targets);
                _impactSignalRoutine = StartCoroutine(PublishImpactWhenPlaybackStarts());
            }
        
            private void BuildTargets(SkillEffectContext context)
            {
                _targets.Clear();
                if (context != null && context.Targets != null)
                {
                    foreach (BattleCharactor target in context.Targets)
                    {
                        if (target != null)
                        {
                            _targets.Add(target.transform);
                        }
                    }
                }
        
                if (_targets.Count == 0 && context != null && context.PrimaryTarget != null)
                {
                    _targets.Add(context.PrimaryTarget.transform);
                }
            }
        
            private IEnumerator PublishImpactWhenPlaybackStarts()
                    {
                        if (_prismExplosion == null)
                        {
                            yield break;
                        }
        
                        Transform impactTransform = _prismExplosion.transform.Find("Impact");
                        FlareBombImpact impactEffect = impactTransform != null ? impactTransform.GetComponent<FlareBombImpact>() : null;
                        float elapsed = 0f;
                        float timeout = _context != null ? 5f : 0.1f;
                        while (impactEffect != null && !impactEffect.IsPlaying && elapsed < timeout)
                        {
                            elapsed += Time.deltaTime;
                            yield return null;
                        }
        
                        _impactSignalRoutine = null;
                        if (_impactNotified || _context == null)
                        {
                            yield break;
                        }
        
                        _impactNotified = true;
                        Vector3 position = impactTransform != null ? impactTransform.position : ResolveImpactPosition(transform.position);
                        SkillImpactSignalBus.PublishImpact(new ImpactKey(_context.ActionInstanceId), position, _targets);
                    }
        
                    private Vector3 ResolveFlightStart()
            {
                if (_prismExplosion == null)
                {
                    return transform.position;
                }
        
                // PrismExplosionVfx captures PrismUnit's current position when Launch starts,
                // then rises by RiseHeight before flight. Reading the same child keeps the
                // adapter's impact signal aligned without modifying the VFX source.
                Transform prismUnit = _prismExplosion.transform.Find("PrismUnit");
                if (prismUnit != null)
                {
                    return prismUnit.position + Vector3.up * _prismExplosion.RiseHeight;
                }
        
                if (_launchOrigin == null)
                {
                    return transform.position;
                }
        
                Vector3 forward = _launchOrigin.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f)
                {
                    forward = Vector3.forward;
                }
        
                return _launchOrigin.position
                    + forward.normalized * _prismExplosion.ForwardOffset
                    + Vector3.up * (_prismExplosion.HoverHeight + _prismExplosion.RiseHeight);
            }
        
            private Vector3 ResolveImpactPosition(Vector3 fallback)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                foreach (Transform target in _targets)
                {
                    if (target == null)
                    {
                        continue;
                    }
        
                    sum += target.position;
                    count++;
                }
        
                return count > 0
                    ? sum / count + Vector3.up * _prismExplosion.TargetHeight
                    : fallback;
            }
        }
        