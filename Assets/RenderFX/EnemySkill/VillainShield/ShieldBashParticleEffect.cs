using System.Collections;
using UnityEngine;

/// <summary>Shield bash particles; movement and damage remain owned by the skill Timeline.</summary>
public sealed class ShieldBashParticleEffect : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle, ISelfManagedEffectLifetime
{
    [SerializeField] private bool placeInFrontOfTarget;
    [SerializeField] private bool keepIdlePose;
    [SerializeField, Min(0f)] private float fitPlaybackSeconds;
    [SerializeField, Min(0f)] private float targetFrontOffset = .6f;
    [SerializeField, Min(.1f)] private float safetySeconds = 4f;
    private ParticleSystem[] particles;
    private float[] playbackMultipliers;
    private SkillEffectContext context;
    private bool stopped;

    public void Play(SkillEffectContext value)
    {
        context = value;
        if (keepIdlePose && context?.Caster?.Anim != null) context.Caster.Anim.PlayIdleAnimation();
        particles = GetComponentsInChildren<ParticleSystem>(true);
        playbackMultipliers = new float[particles.Length];
        if (placeInFrontOfTarget && context?.PrimaryTarget != null && context.Caster != null)
        {
            Vector3 towardCaster = context.Caster.transform.position - context.PrimaryTarget.transform.position;
            towardCaster.y = 0f;
            if (towardCaster.sqrMagnitude < .001f) towardCaster = context.PrimaryTarget.transform.forward;
            towardCaster.Normalize();
            transform.SetPositionAndRotation(context.PrimaryTarget.transform.position + towardCaster * targetFrontOffset,
                Quaternion.LookRotation(-towardCaster));
        }
        for (int i = 0; i < particles.Length; i++)
        {
            var particle = particles[i];
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particle.main;
            // Include the last emitted particle's lifetime so the effect finishes, rather than being cut off.
            playbackMultipliers[i] = fitPlaybackSeconds > 0f
                ? Mathf.Max(.01f, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax) / fitPlaybackSeconds
                : 1f;
            if (fitPlaybackSeconds > 0f) main.loop = false;
            main.simulationSpeed = Speed * playbackMultipliers[i];
            particle.Play(false);
        }
        StartCoroutine(ReleaseWhenFinished());
    }

    private float Speed => context?.Caster?.Anim != null
        ? Mathf.Max(.001f, context.Caster.Anim.CurrentAnimSpeed * context.Caster.Anim.HitStopSpeedMultiplier)
        : Mathf.Max(.001f, context?.PlaybackSpeed ?? 1f);

    private IEnumerator ReleaseWhenFinished()
    {
        float elapsed = 0f;
        while (!stopped && elapsed < safetySeconds)
        {
            if (context?.Caster != null && context.Caster.IsDead) break;
            bool alive = false;
            for (int i = 0; i < particles.Length; i++)
            {
                var particle = particles[i];
                if (particle == null) continue;
                var main = particle.main;
                main.simulationSpeed = Speed * playbackMultipliers[i];
                alive |= particle.IsAlive(false);
            }
            if (!alive) break;
            elapsed += Time.deltaTime * Speed;
            yield return null;
        }
        Stop();
    }

    public bool Signal(SkillEffectContext value) { Stop(); return true; }

    public void Stop()
    {
        if (stopped) return;
        stopped = true;
        if (particles != null)
            foreach (var particle in particles)
                if (particle != null) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        Destroy(gameObject);
    }
}
