using System.Collections.Generic;
using JC.VFX;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PrismExplosionCueEffect : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle
{
    [Tooltip("독립 차징·비행·착탄을 연결할 프리즘 익스플로전입니다.")]
    [SerializeField] private PrismExplosionVfx _prismExplosion;

    private SkillEffectContext _context;
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
        _prismExplosion.Impacted -= OnImpact;
        _prismExplosion.Impacted += OnImpact;
        _prismExplosion.PlaybackSpeed = context.PlaybackSpeed;
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
        if (!_impactNotified && _context != null)
        {
            SkillImpactSignalBus.PublishCancelled(
                _context.ActionInstanceId,
                _targets,
                transform.position);
        }

        if (_prismExplosion != null) { _prismExplosion.Impacted -= OnImpact; _prismExplosion.Stop(); }
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

    private void OnImpact(Vector3 position)
    {
        if (_impactNotified || _context == null) return;
        _impactNotified = true;
        SkillImpactSignalBus.PublishImpact(new ImpactKey(_context.ActionInstanceId), position, _targets);
    }

}
