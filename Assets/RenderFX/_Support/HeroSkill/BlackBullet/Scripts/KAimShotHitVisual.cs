using UnityEngine;

namespace JC.VFX
{
    // Spawned by BattleVisualDirector at the resolved damage target.
    public sealed class KAimShotHitVisual : VfxEffect, ISelfManagedEffectLifetime
    {
        private ParticleSystem[] systems;
        private float elapsed;

        private void Awake() => systems = GetComponentsInChildren<ParticleSystem>(true);

        public override void Play()
        {
            elapsed = 0f;
            IsPlaying = true;
            var camera = Camera.main;
            if (camera != null)
            {
                Vector3 direction = (camera.transform.position - transform.position).normalized;
                transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
                transform.position += direction * .2f;
            }
            foreach (var ps in systems)
            {
                var main = ps.main;
                main.simulationSpeed = Mathf.Max(.01f, PlaybackSpeed);
                ps.Play(false);
            }
        }

        private void Update()
        {
            if (!IsPlaying) return;
            elapsed += EffectDeltaTime;
            bool alive = false;
            foreach (var ps in systems)
            {
                var main = ps.main;
                main.simulationSpeed = Mathf.Max(.01f, PlaybackSpeed);
                alive |= ps.IsAlive(false);
            }
            if ((elapsed > .1f && !alive) || elapsed > 1.5f)
            {
                IsPlaying = false;
                RaiseFinished();
                Destroy(gameObject);
            }
        }

        public override void Stop()
        {
            IsPlaying = false;
            foreach (var ps in systems)
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            Destroy(gameObject);
        }
    }
}
