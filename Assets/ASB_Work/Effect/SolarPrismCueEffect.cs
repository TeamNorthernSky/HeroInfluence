using System.Collections;
using System.Collections.Generic;
using JC.VFX;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SolarPrismCueEffect : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle
{
    [SerializeField] private SolarPrismVfx _solarPrism;

    private SkillEffectContext _context;
    private bool _launched;
    private Coroutine _impactSignalRoutine;

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
        _launched = false;

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
        if (_impactSignalRoutine != null) StopCoroutine(_impactSignalRoutine);
        _impactSignalRoutine = StartCoroutine(PublishWhenImpactStarts());
        return true;
    }

    private IEnumerator PublishWhenImpactStarts()
            {
                float elapsed = 0f;
                const float timeout = 5f;
                while (elapsed < timeout)
                {
                    FlareBombImpact[] impacts = GetComponentsInChildren<FlareBombImpact>(true);
                    foreach (FlareBombImpact impact in impacts)
                    {
                        if (impact == null || !impact.IsPlaying) continue;
                        if (_context != null && _context.ActionInstanceId > 0)
                            SkillImpactSignalBus.PublishImpact(new ImpactKey(_context.ActionInstanceId), impact.transform.position, new[] { impact.transform });
                        _impactSignalRoutine = null;
                        yield break;
                    }
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                _impactSignalRoutine = null;
            }
        
            public void Stop()
    {
        if (_impactSignalRoutine != null)
        {
            StopCoroutine(_impactSignalRoutine);
            _impactSignalRoutine = null;
        }

        if (_solarPrism != null)
        {
            _solarPrism.Stop();
        }

        Destroy(gameObject);
    }
}
