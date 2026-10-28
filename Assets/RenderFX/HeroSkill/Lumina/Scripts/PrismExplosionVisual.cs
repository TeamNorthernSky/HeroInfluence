using UnityEngine;

namespace JC.VFX
{
    // Visuals follow the existing charge/flight handoff and impact signals.
    [DefaultExecutionOrder(1000)]
    public sealed class PrismExplosionVisual : MonoBehaviour
    {
        public PrismExplosionVfx owner;
        public Transform crystal;
        public TrailRenderer flightSignal;
        public GameObject summonPrefab;
        public GameObject flightPrefab;
        public GameObject chargeStarsPrefab;
        public bool flightOnly;
        public float summonScale = 1.8f;
        public float flightScale = 1f;
        public Renderer[] hiddenRenderers;

        private GameObject summon;
        private GameObject flight;
        private GameObject chargeStars;
        private ParticleSystem[] summonSystems;
        private ParticleSystem[] flightSystems;
        private ParticleSystem[] chargeStarSystems;
        private bool starsStarted;
        private bool appeared;
        private bool launched;
        private bool ended;
        private bool flightEnded;
        private Vector3 previous;

        private void OnEnable()
        {
            appeared = launched = ended = false;
            flightEnded = false;
            starsStarted = false;
            previous = crystal != null ? crystal.position : transform.position;
            if (!flightOnly && summonPrefab != null)
            {
                summon = Instantiate(summonPrefab, transform);
                summonSystems = summon.GetComponentsInChildren<ParticleSystem>(true);
                Clear(summonSystems);
            }
            if (flightPrefab != null)
            {
                flight = Instantiate(flightPrefab, transform);
                flightSystems = flight.GetComponentsInChildren<ParticleSystem>(true);
                Clear(flightSystems);
            }
            if (chargeStarsPrefab != null)
            {
                chargeStars = Instantiate(chargeStarsPrefab, transform);
                chargeStarSystems = chargeStars.GetComponentsInChildren<ParticleSystem>(true);
                Clear(chargeStarSystems);
            }
        }

        private void LateUpdate()
        {
            if (owner == null || crystal == null) return;
            if (hiddenRenderers != null)
                foreach (var r in hiddenRenderers) if (r != null) r.forceRenderingOff = true;
            SetSpeed(summonSystems);
            SetSpeed(flightSystems);
            SetSpeed(chargeStarSystems);
            if (launched && !flightEnded && flightSignal != null && !flightSignal.emitting)
            {
                flightEnded = true;
                foreach (var ps in flightSystems)
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            if (!crystal.gameObject.activeInHierarchy)
            {
                if (!ended)
                {
                    Clear(summonSystems);
                    Clear(chargeStarSystems);
                    if (flightSystems != null)
                        foreach (var ps in flightSystems)
                            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
                ended = true;
                return;
            }

            float pop = Mathf.Clamp01(crystal.localScale.x / Mathf.Max(owner.PrismScale, .001f));
            if (chargeStars != null)
            {
                chargeStars.transform.position = crystal.position;
                chargeStars.transform.localScale = Vector3.one * pop;
                if (!starsStarted && !launched && pop > .001f)
                {
                    starsStarted = true;
                    foreach (var ps in chargeStarSystems) ps.Play(false);
                }
            }
            if (summon != null)
            {
                summon.transform.SetPositionAndRotation(crystal.position, crystal.rotation * Quaternion.Euler(-90f, 0f, 0f));
                summon.transform.localScale = Vector3.one * summonScale * pop;
                if (!appeared && pop > .001f)
                {
                    appeared = true;
                    foreach (var ps in summonSystems) ps.Play(false);
                }
            }
            Vector3 velocity = crystal.position - previous;
            previous = crystal.position;
            if (flight != null)
            {
                flight.transform.position = crystal.position;
                // The original meteor travels along local -Y; its tail faces +Y.
                if (velocity.sqrMagnitude > .000001f)
                    flight.transform.rotation = Quaternion.FromToRotation(Vector3.down, velocity.normalized);
                flight.transform.localScale = Vector3.one * flightScale;
                if (!launched && flightSignal != null && flightSignal.emitting)
                {
                    launched = true;
                    if (chargeStarSystems != null)
                        foreach (var ps in chargeStarSystems)
                            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                    foreach (var ps in flightSystems) ps.Play(false);
                }
            }
        }

        private void SetSpeed(ParticleSystem[] systems)
        {
            if (systems == null) return;
            foreach (var ps in systems)
            {
                var main = ps.main;
                main.simulationSpeed = Mathf.Max(.01f, owner.PlaybackSpeed);
            }
        }

        private static void Clear(ParticleSystem[] systems)
        {
            if (systems != null)
                foreach (var ps in systems)
                    if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable()
        {
            Clear(summonSystems);
            Clear(flightSystems);
            Clear(chargeStarSystems);
            if (summon != null) Destroy(summon);
            if (flight != null) Destroy(flight);
            if (chargeStars != null) Destroy(chargeStars);
            summon = flight = null;
            summonSystems = flightSystems = null;
            chargeStars = null;
            chargeStarSystems = null;
        }
    }
}
