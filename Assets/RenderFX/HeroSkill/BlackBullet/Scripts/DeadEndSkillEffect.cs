using System.Linq;
using UnityEngine;

namespace JC.VFX
{
    public sealed class DeadEndSkillEffect : MonoBehaviour, ISkillEffectBehaviour,
        ISkillEffectHandle, ISelfManagedEffectLifetime
    {
        public GameObject aimPrefab, projectilePrefab, orbPrefab, explosionPrefab;
        public GameObject condensationAccentPrefab;
        public float condensationScale = 1.7f;
        public float flightSeconds = .18f;
        public float condensationSeconds = .4f;
        public float chargedHoldSeconds = .5f;
        public enum Phase { Aiming, Flying, Condensing, Exploding, Charged }
        public Phase CurrentPhase { get; private set; }
        public float PhaseTime { get; private set; }
        public Vector3 TargetCenter { get; private set; }
        public int ExplosionCount { get; private set; }

        private SkillEffectContext context;
        private GameObject aim, projectile, orb, explosion;
        private GameObject condensationAccent;
        private SkinnedMeshRenderer targetBody;
        private Vector3 shotOrigin;
        private float lifetime;
        private bool playing;
        private Transform[] aimStrokes;
        private ParticleSystem gatheringParticles;
        private float gatheringSeconds;
        private const float CondensationPlaybackMultiplier = 2f;
        private ParticleSystem[] fadingAccentSystems;
        private ParticleSystem.Particle[][] fadingAccentBuffers;
        private float accentOpacity;

        public void Play(SkillEffectContext ctx)
        {
            context = ctx;
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;
            if (ctx?.PrimaryTarget != null)
                targetBody = ctx.PrimaryTarget.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Where(r => r.sharedMesh != null)
                    .OrderByDescending(r => r.bounds.size.sqrMagnitude).FirstOrDefault();
            TargetCenter = ResolveTargetCenter();
            CurrentPhase = Phase.Aiming;
            PhaseTime = lifetime = 0f;
            ExplosionCount = 0;
            playing = true;
            aim = Spawn(aimPrefab, TargetCenter, Quaternion.identity, 1f);
            aimStrokes = aim != null ? aim.GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith("outline_")).ToArray() : new Transform[0];
        }

        public bool Signal(SkillEffectContext ctx)
        {
            if (!playing || CurrentPhase != Phase.Aiming) return false;
            shotOrigin = ctx?.SocketTransform != null ? ctx.SocketTransform.position
                : context.Caster.transform.position + Vector3.up;
            Remove(ref aim);
            TargetCenter = ResolveTargetCenter();
            projectile = Spawn(projectilePrefab, shotOrigin,
                Quaternion.FromToRotation(Vector3.forward, (TargetCenter - shotOrigin).normalized), .45f);
            CurrentPhase = Phase.Flying;
            PhaseTime = 0f;
            return false;
        }

        private void Update()
        {
            if (!playing) return;
            float speed = Mathf.Max(.01f, context?.PlaybackSpeed ?? 1f);
            float dt = Time.deltaTime * speed;
            lifetime += dt;
            PhaseTime += CurrentPhase == Phase.Condensing ? dt * CondensationPlaybackMultiplier : dt;
            TargetCenter = ResolveTargetCenter();
            if (CurrentPhase == Phase.Aiming)
            {
                if (aim != null)
                {
                    aim.transform.position = TargetCenter;
                    var camera = Camera.main;
                    if (camera != null)
                    {
                        Vector3 facing = camera.transform.position - TargetCenter;
                        facing.y = 0f;
                        if (facing.sqrMagnitude > .001f)
                            aim.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
                    }
                    float pulse = .85f + .25f * Mathf.Sin(PhaseTime * Mathf.PI * 4f);
                    foreach (var stroke in aimStrokes)
                        if (stroke != null) stroke.localScale = Vector3.one * pulse;
                }
            }
            else if (CurrentPhase == Phase.Flying)
            {
                if (projectile != null)
                    projectile.transform.position = Vector3.Lerp(shotOrigin, TargetCenter,
                        Mathf.Clamp01(PhaseTime / Mathf.Max(.01f, flightSeconds)));
                if (PhaseTime >= flightSeconds)
                {
                    Remove(ref projectile);
                    orb = Spawn(orbPrefab, TargetCenter, Quaternion.identity, .45f * condensationScale);
                    condensationAccent = Spawn(condensationAccentPrefab, TargetCenter, Quaternion.identity, .45f);
                    foreach (var group in new[] { orb, condensationAccent })
                        if (group != null)
                            foreach (var particles in group.GetComponentsInChildren<ParticleSystem>(true))
                            {
                                var main = particles.main;
                                main.simulationSpeed *= CondensationPlaybackMultiplier;
                            }
                    gatheringParticles = condensationAccent != null
                        ? condensationAccent.GetComponentsInChildren<ParticleSystem>(true)
                            .FirstOrDefault(p => p.name == "HCFX_Appear_01")
                        : null;
                    gatheringSeconds = condensationSeconds;
                    if (gatheringParticles != null)
                    {
                        var main = gatheringParticles.main;
                        var emission = gatheringParticles.emission;
                        float lastBurst = 0f;
                        for (int i = 0; i < emission.burstCount; i++)
                        {
                            var burst = emission.GetBurst(i);
                            lastBurst = Mathf.Max(lastBurst, burst.time
                                + Mathf.Max(0, burst.cycleCount - 1) * burst.repeatInterval);
                        }
                        gatheringSeconds = Mathf.Max(gatheringSeconds,
                            main.startDelay.constantMax + lastBurst + main.startLifetime.constantMax);
                    }
                    CurrentPhase = Phase.Condensing;
                    PhaseTime = 0f;
                }
            }
            else if (CurrentPhase == Phase.Condensing)
            {
                if (orb != null)
                {
                    orb.transform.position = TargetCenter;
                    float t = Mathf.Clamp01(PhaseTime / Mathf.Max(.01f, gatheringSeconds));
                    orb.transform.localScale = Vector3.one * (Mathf.Lerp(.45f, .13f, t * t) * condensationScale);
                }
                if (condensationAccent != null) condensationAccent.transform.position = TargetCenter;
                if (PhaseTime >= gatheringSeconds &&
                    (gatheringParticles == null || gatheringParticles.particleCount == 0))
                {
                    BeginAccentFade();
                    CurrentPhase = Phase.Charged;
                    PhaseTime = 0f;
                }
            }
            else if (CurrentPhase == Phase.Charged)
            {
                FadeAccent();
                if (orb != null)
                {
                    orb.transform.position = TargetCenter;
                    float pulse = 1f + .04f * Mathf.Sin(PhaseTime * Mathf.PI * 12f);
                    orb.transform.localScale = Vector3.one * (.13f * condensationScale * pulse);
                }
                if (PhaseTime >= chargedHoldSeconds) Explode();
            }
            else if (PhaseTime > 2f) Stop();
            if (lifetime > 6f) Stop();
        }

        private void BeginAccentFade()
        {
            if (condensationAccent == null) return;
            fadingAccentSystems = condensationAccent.GetComponentsInChildren<ParticleSystem>(true);
            fadingAccentBuffers = new ParticleSystem.Particle[fadingAccentSystems.Length][];
            accentOpacity = 1f;
            for (int i = 0; i < fadingAccentSystems.Length; i++)
            {
                var particles = fadingAccentSystems[i];
                particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                fadingAccentBuffers[i] = new ParticleSystem.Particle[particles.main.maxParticles];
            }
        }

        private void FadeAccent()
        {
            if (condensationAccent == null || fadingAccentSystems == null) return;
            condensationAccent.transform.position = TargetCenter;
            float duration = Mathf.Min(.3f, Mathf.Max(.01f, chargedHoldSeconds));
            float opacity = 1f - Mathf.SmoothStep(0f, 1f, PhaseTime / duration);
            float ratio = opacity / Mathf.Max(.0001f, accentOpacity);
            for (int i = 0; i < fadingAccentSystems.Length; i++)
            {
                var particles = fadingAccentSystems[i];
                if (particles == null) continue;
                var buffer = fadingAccentBuffers[i];
                int count = particles.GetParticles(buffer);
                for (int j = 0; j < count; j++)
                    buffer[j].startColor = (Color)buffer[j].startColor * ratio;
                particles.SetParticles(buffer, count);
            }
            accentOpacity = opacity;
            if (opacity <= 0f) Remove(ref condensationAccent);
        }

        private void Explode()
        {
            Remove(ref orb);
            Remove(ref condensationAccent);
            explosion = Spawn(explosionPrefab, TargetCenter, Quaternion.identity, .5f);
            CurrentPhase = Phase.Exploding;
            PhaseTime = 0f;
            ExplosionCount++;
            context?.Caster?.GetComponent<PresentationRuntimeContext>()?.RemoveHandle("dead_end");
            PresentationSfxEvents.PlayEffectImpact(context?.Presentation, 0);
            if (context != null && context.ActionInstanceId > 0)
            {
                var targets = context.Targets?.Where(u => u != null).Select(u => u.transform).ToArray()
                    ?? (context.PrimaryTarget != null ? new[] { context.PrimaryTarget.transform }
                        : System.Array.Empty<Transform>());
                SkillImpactSignalBus.PublishImpact(new ImpactKey(context.ActionInstanceId), TargetCenter, targets);
            }
        }

        private Vector3 ResolveTargetCenter()
        {
            if (targetBody != null) return targetBody.bounds.center;
            if (context?.PrimaryTarget != null)
                return context.PrimaryTarget.transform.position + Vector3.up;
            return context?.TargetPosition ?? transform.position;
        }

        private GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale)
        {
            if (prefab == null) return null;
            var instance = Instantiate(prefab, position, rotation, transform);
            instance.transform.localScale = Vector3.one * scale;
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.simulationSpeed = Mathf.Max(.01f, context?.PlaybackSpeed ?? 1f);
                ps.Play(false);
            }
            return instance;
        }

        private void Remove(ref GameObject instance)
        {
            if (instance == null) return;
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            Destroy(instance);
            instance = null;
        }

        public void Stop()
        {
            playing = false;
            Remove(ref aim);
            Remove(ref projectile);
            Remove(ref orb);
            Remove(ref condensationAccent);
            Remove(ref explosion);
            Destroy(gameObject);
        }
    }
}
