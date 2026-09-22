using System.Collections.Generic;
using JC.VFX;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SolarPrismCueEffect : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle
{
    [Tooltip("독립 차징·비행·착탄을 연결할 솔라 프리즘입니다.")]
    [SerializeField] private SolarPrismVfx _solarPrism;

    private SkillEffectContext _context;
    private bool _launched;
    private bool _impactNotified;

    private void Awake()
    {
        if (_solarPrism == null)
        {
            _solarPrism = GetComponent<SolarPrismVfx>();
        }
    }

    public void Play(SkillEffectContext context)
    {
        _context = context;
        _launched = false; _impactNotified = false;

        if (_solarPrism == null || context == null)
        {
            return;
        }

        Transform origin = context.SocketTransform != null
            ? context.SocketTransform
            : context.Caster != null
                ? context.Caster.transform
                : transform;
        Transform target = context.PrimaryTarget != null ? context.PrimaryTarget.transform : null;

        _solarPrism.Impacted -= OnImpact;
        _solarPrism.Impacted += OnImpact;
        _solarPrism.PlaybackSpeed = context.PlaybackSpeed;
        if (context.Targets != null && context.Targets.Count > 0) _solarPrism.UnitCount = context.Targets.Count;
        _solarPrism.Play(origin, target);
    }

    public bool Signal(SkillEffectContext context)
    {
        if (_solarPrism == null)
        {
            return false;
        }

        if (_launched)
        {
            return true;
        }

        _launched = true;
        SkillEffectContext activeContext = context ?? _context;
        var targets = new List<Transform>();

        if (activeContext != null && activeContext.Targets != null)
        {
            foreach (BattleCharactor target in activeContext.Targets)
            {
                if (target != null)
                {
                    targets.Add(target.transform);
                }
            }
        }

        if (targets.Count == 0 && activeContext != null && activeContext.PrimaryTarget != null)
        {
            targets.Add(activeContext.PrimaryTarget.transform);
        }

        _solarPrism.Launch(targets);

        return true;
    }

    private void OnImpact(Vector3 position)
    {
        if (_impactNotified || _context == null) return;
        _impactNotified = true;
        SkillImpactSignalBus.PublishImpact(new ImpactKey(_context.ActionInstanceId), position);
    }
    public void Stop()
    {
        if (_solarPrism != null)
        {
            _solarPrism.Impacted -= OnImpact;
            _solarPrism.Stop();
        }

        Destroy(gameObject);
    }
}
