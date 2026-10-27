using UnityEngine;

namespace JC.VFX
{
    // The existing impact controller continues to own impact timing/completion.
    [DefaultExecutionOrder(1000)]
    public sealed class SolarPrismImpactParticles : MonoBehaviour
    {
        private FlareBombImpact owner;
        private ParticleSystem[] particles;
        private bool playing;

        private void Awake()
        {
            owner = GetComponent<FlareBombImpact>();
            particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        private void LateUpdate()
        {
            if (owner == null) return;
            foreach (var ps in particles)
            {
                var main = ps.main;
                main.simulationSpeed = Mathf.Max(.01f, owner.PlaybackSpeed);
            }
            if (owner.IsPlaying && !playing)
                foreach (var ps in particles) ps.Play(false);
            else if (!owner.IsPlaying && playing)
                Clear();
            playing = owner.IsPlaying;
        }

        private void OnDisable() => Clear();

        private void Clear()
        {
            if (particles != null)
                foreach (var ps in particles)
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            playing = false;
        }
    }
}
