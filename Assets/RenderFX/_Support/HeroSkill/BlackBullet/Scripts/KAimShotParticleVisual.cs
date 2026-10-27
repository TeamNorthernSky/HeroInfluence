using System;
using UnityEngine;

namespace JC.VFX
{
    // Existing renderer hosts provide the skill's position and timing signals.
    [DefaultExecutionOrder(1000)]
    public sealed class KAimShotParticleVisual : MonoBehaviour, ISelfManagedEffectLifetime
    {
        [Serializable]
        public sealed class Layer
        {
            public Renderer host;
            public GameObject prefab;
            public bool projectile;
            public bool muzzle;
            public float scale = .6f;
            [NonSerialized] public GameObject instance;
            [NonSerialized] public ParticleSystem[] systems;
            [NonSerialized] public bool started;
            [NonSerialized] public bool stopped;
            [NonSerialized] public ParticleSystem.Particle[][] buffers;
        }

        public KAimShotVfx owner;
        public Layer[] layers;
        public Renderer[] hiddenRenderers;
        public TrailRenderer longTrail;
        public bool useDamageHitEffect;
        public bool followMuzzleSocket;
        public GameObject repeatedProjectilePrefab;
        public float repeatedProjectileScale = .4f;
        public GameObject repeatedImpactPrefab;
        public float repeatedImpactScale = .22f;
        public bool centerRepeatedHitsOnMesh;
        [Min(1)] public int repeatedImpactEvery = 1;
        public float repeatedTrailTime = .22f;
        public Vector2 repeatedProjectileSpread;
        public int EmittedProjectileCount { get; private set; }
        public int ArrivedProjectileCount { get; private set; }
        public int SpawnedImpactCount { get; private set; }
        private sealed class FlyingProjectile
        {
            public GameObject instance;
            public ParticleSystem[] systems;
            public TrailRenderer[] trails;
            public Vector3 origin, target, spreadOffset;
            public float bornAt;
        }
        private readonly System.Collections.Generic.List<FlyingProjectile> flyingProjectiles =
            new System.Collections.Generic.List<FlyingProjectile>();
        private sealed class ImpactVisual
        {
            public GameObject instance;
            public ParticleSystem[] systems;
            public Vector3 spreadOffset;
            public float bornAt;
        }
        private sealed class FadingTrail
        {
            public TrailRenderer renderer;
            public float bornAt;
        }
        private readonly System.Collections.Generic.List<ImpactVisual> repeatedImpacts =
            new System.Collections.Generic.List<ImpactVisual>();
        private readonly System.Collections.Generic.List<FadingTrail> fadingTrails =
            new System.Collections.Generic.List<FadingTrail>();
        public GameObject splitPrefab;
        [Range(.1f, .9f)] public float splitFraction = .5f;
        public float splitScale = .65f;
        public bool randomizeSplitPellets;
        public float splitPelletLifetimeMultiplier = 3.2f;
        private System.Random scatterRandom;
        public bool SplitTriggered { get; private set; }
        public Vector3 SplitPosition => Vector3.Lerp(owner.ShotOrigin, owner.ImpactPosition, splitFraction);
        private GameObject splitInstance;
        private ParticleSystem[] splitSystems;
        private bool wasPlaying;

        private void OnEnable()
        {
            wasPlaying = false;
            scatterRandom = new System.Random(GetInstanceID());
            EmittedProjectileCount = ArrivedProjectileCount = SpawnedImpactCount = 0;
            SplitTriggered = false;
            if (splitPrefab != null && owner != null &&
                (owner.ActiveSegment == KAimShotVfx.Segment.All || owner.ActiveSegment == KAimShotVfx.Segment.Flight))
            {
                splitInstance = Instantiate(splitPrefab, transform);
                splitInstance.transform.localScale = Vector3.one * splitScale;
                splitSystems = splitInstance.GetComponentsInChildren<ParticleSystem>(true);
                ClearSplit();
            }
            foreach (var layer in layers)
            {
                layer.started = layer.stopped = false;
                if (useDamageHitEffect && !layer.projectile && !layer.muzzle) continue;
                if (layer.prefab == null) continue;
                layer.instance = Instantiate(layer.prefab, transform);
                layer.instance.transform.localScale = Vector3.one * layer.scale;
                layer.systems = layer.instance.GetComponentsInChildren<ParticleSystem>(true);
                layer.buffers = new ParticleSystem.Particle[layer.systems.Length][];
                for (int i = 0; i < layer.systems.Length; i++)
                    layer.buffers[i] = new ParticleSystem.Particle[layer.systems[i].main.maxParticles];
                Clear(layer);
            }
        }

        private void LateUpdate()
        {
            if (owner == null) return;
            foreach (var r in hiddenRenderers) if (r != null) r.forceRenderingOff = true;
            if (longTrail != null)
                longTrail.time = owner.tailReachSeconds / Mathf.Max(.01f, owner.PlaybackSpeed);
            if (!owner.IsPlaying)
            {
                if (wasPlaying) { foreach (var layer in layers) Clear(layer); ClearSplit(); ClearRepeatedProjectiles(); }
                wasPlaying = false;
                return;
            }
            if (!wasPlaying)
            {
                foreach (var layer in layers) { Clear(layer); layer.started = layer.stopped = false; }
                ClearSplit();
                ClearRepeatedProjectiles();
                EmittedProjectileCount = ArrivedProjectileCount = SpawnedImpactCount = 0;
                SplitTriggered = false;
            }
            wasPlaying = true;
            if (splitInstance != null)
            {
                if (!SplitTriggered && owner.SequenceTime >= owner.launchDelay + owner.travelTime * splitFraction)
                    PlaySplit();
                foreach (var ps in splitSystems)
                {
                    var main = ps.main;
                    main.simulationSpeed = Mathf.Max(.01f, owner.PlaybackSpeed);
                }
                if (SplitTriggered && longTrail != null)
                {
                    longTrail.transform.position = SplitPosition;
                    longTrail.emitting = false;
                }
            }
            foreach (var layer in layers)
            {
                if (layer.instance == null || layer.host == null) continue;
                foreach (var ps in layer.systems)
                {
                    var main = ps.main;
                    main.simulationSpeed = Mathf.Max(.01f, owner.PlaybackSpeed);
                }
                var life = layer.projectile ? owner.bulletHead : layer.muzzle ? owner.muzzleLife : owner.impactBurst;
                var segment = layer.projectile ? KAimShotVfx.Segment.Flight : layer.muzzle ? KAimShotVfx.Segment.Muzzle : KAimShotVfx.Segment.Impact;
                bool applicable = owner.ActiveSegment == KAimShotVfx.Segment.All || owner.ActiveSegment == segment;
                if (!layer.started && applicable && life.enabled && owner.SequenceTime >= life.startDelay)
                {
                    layer.started = true;
                    Position(layer);
                    foreach (var ps in layer.systems) ps.Play(false);
                }
                if (layer.projectile && layer.started && !layer.stopped)
                {
                    if (SplitTriggered || owner.SequenceTime >= owner.launchDelay + owner.travelTime)
                    { Clear(layer); layer.stopped = true; }
                    else { Position(layer); OrientProjectile(layer); }
                }
                if (layer.muzzle && layer.started && followMuzzleSocket) Position(layer);
            }
            UpdateRepeatedProjectiles();
        }

        private void UpdateRepeatedProjectiles()
        {
            if (repeatedProjectilePrefab == null) return;
            Vector3 impactPosition = centerRepeatedHitsOnMesh ? owner.TargetMeshCenter : owner.ImpactPosition;
            // Read the actual muzzle burst schedule so every flash has exactly one bullet.
            foreach (var layer in layers)
            {
                if (!layer.muzzle || !layer.started || layer.systems == null || layer.systems.Length == 0) continue;
                var muzzle = layer.systems[0];
                if (muzzle.emission.burstCount == 0) break;
                var burst = muzzle.emission.GetBurst(0);
                while (EmittedProjectileCount < burst.cycleCount &&
                       muzzle.time + .00001f >= burst.time + EmittedProjectileCount * burst.repeatInterval)
                {
                    float due = burst.time + EmittedProjectileCount * burst.repeatInterval;
                    Vector3 origin = owner.LiveMuzzlePosition;
                    Vector3 direction = (impactPosition - origin).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
                    if (right.sqrMagnitude < .001f) right = Vector3.right;
                    Vector3 up = Vector3.Cross(direction, right).normalized;
                    float angle = (float)scatterRandom.NextDouble() * Mathf.PI * 2f;
                    float radius = Mathf.Sqrt(.15f + .85f * (float)scatterRandom.NextDouble());
                    Vector3 spreadOffset = right * (Mathf.Cos(angle) * radius * repeatedProjectileSpread.x)
                        + up * (Mathf.Sin(angle) * radius * repeatedProjectileSpread.y);
                    Vector3 target = impactPosition + spreadOffset;
                    var instance = Instantiate(repeatedProjectilePrefab, transform);
                    instance.transform.SetPositionAndRotation(origin,
                        Quaternion.FromToRotation(Vector3.forward, (target - origin).normalized));
                    instance.transform.localScale = Vector3.one * repeatedProjectileScale;
                    var trails = instance.GetComponentsInChildren<TrailRenderer>(true);
                    foreach (var trailRenderer in trails)
                    {
                        trailRenderer.Clear();
                        trailRenderer.time = repeatedTrailTime / Mathf.Max(.01f, owner.PlaybackSpeed);
                        trailRenderer.emitting = true;
                        trailRenderer.AddPosition(origin);
                    }
                    var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var ps in systems)
                    {
                        var main = ps.main;
                        main.simulationSpeed = Mathf.Max(.01f, owner.PlaybackSpeed);
                        ps.Play(false);
                    }
                    flyingProjectiles.Add(new FlyingProjectile
                    {
                        instance = instance, systems = systems, trails = trails, origin = origin, target = target,
                        spreadOffset = spreadOffset,
                        bornAt = owner.SequenceTime - Mathf.Max(0f, muzzle.time - due)
                    });
                    EmittedProjectileCount++;
                }
                break;
            }
            for (int i = flyingProjectiles.Count - 1; i >= 0; i--)
            {
                var shot = flyingProjectiles[i];
                if (centerRepeatedHitsOnMesh) shot.target = impactPosition + shot.spreadOffset;
                float progress = Mathf.Clamp01((owner.SequenceTime - shot.bornAt) / Mathf.Max(.01f, owner.travelTime));
                shot.instance.transform.position = Vector3.Lerp(shot.origin, shot.target, progress);
                foreach (var trailRenderer in shot.trails)
                    trailRenderer.time = repeatedTrailTime / Mathf.Max(.01f, owner.PlaybackSpeed);
                foreach (var ps in shot.systems)
                {
                    var main = ps.main;
                    main.simulationSpeed = Mathf.Max(.01f, owner.PlaybackSpeed);
                }
                if (progress < 1f) continue;
                if (repeatedImpactPrefab != null &&
                    (ArrivedProjectileCount + 1) % Mathf.Max(1, repeatedImpactEvery) == 0)
                {
                    var hit = Instantiate(repeatedImpactPrefab, transform);
                    hit.transform.SetPositionAndRotation(shot.target,
                        Quaternion.FromToRotation(Vector3.up, (shot.origin - shot.target).normalized));
                    hit.transform.localScale = Vector3.one * repeatedImpactScale;
                    var systems = hit.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var ps in systems)
                    {
                        var main = ps.main;
                        main.simulationSpeed = Mathf.Max(.01f, owner.PlaybackSpeed);
                        ps.Play(false);
                    }
                    repeatedImpacts.Add(new ImpactVisual { instance = hit, systems = systems,
                        spreadOffset = shot.spreadOffset, bornAt = owner.SequenceTime });
                    SpawnedImpactCount++;
                }
                foreach (var trailRenderer in shot.trails)
                {
                    trailRenderer.emitting = false;
                    trailRenderer.transform.SetParent(transform, true);
                    fadingTrails.Add(new FadingTrail { renderer = trailRenderer, bornAt = owner.SequenceTime });
                }
                foreach (var ps in shot.systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                Destroy(shot.instance);
                flyingProjectiles.RemoveAt(i);
                ArrivedProjectileCount++;
            }
            for (int i = repeatedImpacts.Count - 1; i >= 0; i--)
            {
                var hit = repeatedImpacts[i];
                if (centerRepeatedHitsOnMesh) hit.instance.transform.position = impactPosition + hit.spreadOffset;
                bool alive = false;
                foreach (var ps in hit.systems)
                {
                    var main = ps.main;
                    main.simulationSpeed = Mathf.Max(.01f, owner.PlaybackSpeed);
                    alive |= ps.IsAlive(false);
                }
                if (owner.SequenceTime - hit.bornAt < .1f || alive) continue;
                Destroy(hit.instance);
                repeatedImpacts.RemoveAt(i);
            }
            for (int i = fadingTrails.Count - 1; i >= 0; i--)
            {
                var trail = fadingTrails[i];
                trail.renderer.time = repeatedTrailTime / Mathf.Max(.01f, owner.PlaybackSpeed);
                if (owner.SequenceTime - trail.bornAt < repeatedTrailTime) continue;
                trail.renderer.Clear();
                Destroy(trail.renderer.gameObject);
                fadingTrails.RemoveAt(i);
            }
        }

        private void ClearRepeatedProjectiles()
        {
            foreach (var shot in flyingProjectiles)
            {
                foreach (var ps in shot.systems)
                    if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (shot.instance != null) Destroy(shot.instance);
            }
            flyingProjectiles.Clear();
            foreach (var hit in repeatedImpacts) if (hit.instance != null) Destroy(hit.instance);
            repeatedImpacts.Clear();
            foreach (var trail in fadingTrails)
                if (trail.renderer != null) { trail.renderer.Clear(); Destroy(trail.renderer.gameObject); }
            fadingTrails.Clear();
        }

        private void PlaySplit()
        {
            SplitTriggered = true;
            splitInstance.transform.SetPositionAndRotation(SplitPosition,
                Quaternion.FromToRotation(Vector3.up, owner.ShotDirection));
            float remainingTime = Mathf.Max(.025f, owner.travelTime * (1f - splitFraction));
            float remainingDistance = Vector3.Distance(SplitPosition, owner.ImpactPosition);
            foreach (var ps in splitSystems)
            {
                var main = ps.main;
                main.simulationSpeed = Mathf.Max(.01f, owner.PlaybackSpeed);
                if (ps.name == "particle" || ps.name == "particle_stone")
                {
                    if (randomizeSplitPellets)
                    {
                        PlayRandomSplitPellets(ps, remainingDistance, remainingTime);
                        continue;
                    }
                    main.startSpeed = remainingDistance / (remainingTime * Mathf.Max(.01f, splitScale));
                    main.startLifetime = remainingTime * 1.2f;
                }
                if (ps.name == "sharp_out")
                {
                    // The cone mesh extends along local +Z, rotated onto the shot axis.
                    main.startSizeZ = remainingDistance / Mathf.Max(.01f, splitScale);
                }
                ps.Play(false);
            }
            if (longTrail != null)
            {
                longTrail.transform.position = SplitPosition;
                longTrail.AddPosition(SplitPosition);
                longTrail.emitting = false;
            }
        }

        private void PlayRandomSplitPellets(ParticleSystem ps, float distance, float travelTime)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            int count = emission.burstCount > 0 ? emission.GetBurst(0).maxCount : 1;
            emission.enabled = false;
            ps.Play(false);
            Vector3 forward = owner.ShotDirection;
            Vector3 right = Vector3.Cross(forward, Vector3.up);
            if (right.sqrMagnitude < .001f) right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(right, forward).normalized;
            float spread = Mathf.Tan(ps.shape.angle * Mathf.Deg2Rad);
            for (int i = 0; i < count; i++)
            {
                float radius = Mathf.Sqrt((float)scatterRandom.NextDouble());
                float angle = (float)scatterRandom.NextDouble() * Mathf.PI * 2f;
                Vector3 disk = right * (Mathf.Cos(angle) * radius) + up * (Mathf.Sin(angle) * radius);
                float speed = distance / travelTime * Mathf.Lerp(.55f, 1.05f, (float)scatterRandom.NextDouble());
                ps.Emit(new ParticleSystem.EmitParams
                {
                    position = SplitPosition + disk * ps.shape.radius * splitScale,
                    velocity = (forward + disk * spread) * speed,
                    startLifetime = travelTime * splitPelletLifetimeMultiplier *
                        Mathf.Lerp(.85f, 1.15f, (float)scatterRandom.NextDouble())
                }, 1);
            }
        }

        private void ClearSplit()
        {
            if (splitSystems == null) return;
            foreach (var ps in splitSystems)
                if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Position(Layer layer)
        {
            layer.instance.transform.position = layer.projectile ? owner.BulletPosition + owner.bulletHead.offset
                : layer.muzzle ? (followMuzzleSocket ? owner.LiveMuzzlePosition : owner.ShotOrigin)
                : owner.ImpactPosition - owner.ShotDirection * .35f;
            layer.instance.transform.rotation = Quaternion.FromToRotation(
                layer.muzzle || layer.projectile ? Vector3.forward : Vector3.up,
                layer.muzzle || layer.projectile ? owner.ShotDirection : -owner.ShotDirection);
        }

        private void OrientProjectile(Layer layer)
        {
            var camera = Camera.main;
            if (camera == null) return;
            Vector3 point = layer.instance.transform.position;
            Vector3 direction = camera.WorldToScreenPoint(point + owner.ShotDirection) - camera.WorldToScreenPoint(point);
            float angle = -Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            for (int i = 0; i < layer.systems.Length; i++)
            {
                var ps = layer.systems[i];
                if (ps.name != "Core" && ps.name != "Outline") continue;
                int count = ps.GetParticles(layer.buffers[i]);
                for (int j = 0; j < count; j++)
                {
                    var particle = layer.buffers[i][j];
                    particle.rotation3D = new Vector3(0f, 0f, angle);
                    layer.buffers[i][j] = particle;
                }
                ps.SetParticles(layer.buffers[i], count);
            }
        }

        private static void Clear(Layer layer)
        {
            if (layer.systems == null) return;
            foreach (var ps in layer.systems)
                if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable()
        {
            ClearRepeatedProjectiles();
            ClearSplit();
            if (splitInstance != null) Destroy(splitInstance);
            splitInstance = null;
            splitSystems = null;
            foreach (var layer in layers)
            {
                Clear(layer);
                if (layer.instance != null) Destroy(layer.instance);
                layer.instance = null;
                layer.systems = null;
            }
        }
    }
}
