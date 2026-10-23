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
        private bool wasPlaying;

        private void OnEnable()
        {
            wasPlaying = false;
            foreach (var layer in layers)
            {
                layer.started = layer.stopped = false;
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
                if (wasPlaying) foreach (var layer in layers) Clear(layer);
                wasPlaying = false;
                return;
            }
            if (!wasPlaying)
                foreach (var layer in layers) { Clear(layer); layer.started = layer.stopped = false; }
            wasPlaying = true;
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
                    Position(layer);
                    OrientProjectile(layer);
                    if (owner.SequenceTime >= owner.launchDelay + owner.travelTime) { Clear(layer); layer.stopped = true; }
                }
            }
        }

        private void Position(Layer layer)
        {
            layer.instance.transform.position = layer.projectile ? owner.BulletPosition + owner.bulletHead.offset
                : layer.muzzle ? owner.ShotOrigin : owner.ImpactPosition - owner.ShotDirection * .35f;
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
