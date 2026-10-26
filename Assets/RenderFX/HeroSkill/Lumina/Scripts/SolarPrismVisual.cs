using UnityEngine;

namespace JC.VFX
{
    // Follows the existing prism and its FlightTrail launch signal. Does not
    // change summon motion, target selection, handoff, flight or impact timing.
    [DefaultExecutionOrder(1000)]
    public sealed class SolarPrismVisual : MonoBehaviour
    {
        [SerializeField] private SolarPrismVfx owner;
        [SerializeField] private Transform crystal;
        [SerializeField] private TrailRenderer flightTrail;
        [SerializeField] private GameObject flashPrefab;
        [SerializeField] private GameObject meteorFlightPrefab;
        [SerializeField] private float meteorFlightScale = .4f;
        [SerializeField] private bool flightOnly;
        [SerializeField] private Color tint = Color.white;
        [ColorUsage(true, true)]
        [SerializeField] private Color coreColor = Color.clear;
        [SerializeField] private float flashScale = 0.65f;
        [SerializeField] private Renderer[] hiddenRenderers;
        [SerializeField] private Renderer groundRenderer;

        private GameObject flash;
        private GameObject meteorFlight;
        private ParticleSystem[] meteorSystems;
        private Vector3 previousPosition;
        private ParticleSystem[] systems;
        private ParticleSystem sparks;
        private ParticleSystem shockwave;
        private Renderer bodyRenderer;
        private MaterialPropertyBlock block;
        private bool appeared;
        private bool launched;
        private bool ended;

        private void OnEnable()
        {
            appeared = launched = ended = false;
            previousPosition = crystal != null ? crystal.position : transform.position;
            block = new MaterialPropertyBlock();
            bodyRenderer = crystal != null ? crystal.GetComponent<Renderer>() : null;
            if (flashPrefab == null || crystal == null) return;
            flash = Instantiate(flashPrefab, transform);
            flash.name = flashPrefab.name;
            systems = flash.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (ps.name == "BigSparks") sparks = ps;
                else if (ps.name == "Shockwave") shockwave = ps;
            }
            if (meteorFlightPrefab != null && flightOnly)
            {
                meteorFlight = Instantiate(meteorFlightPrefab, transform);
                meteorSystems = meteorFlight.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in meteorSystems)
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (flightTrail != null)
            {
                var gradient = flightTrail.colorGradient;
                var keys = gradient.colorKeys;
                for (int i = 0; i < keys.Length; i++) keys[i].color = tint;
                gradient.SetKeys(keys, gradient.alphaKeys);
                flightTrail.colorGradient = gradient;
            }
        }

        private void LateUpdate()
        {
            if (flash == null || owner == null || crystal == null) return;
            foreach (var renderer in hiddenRenderers)
                if (renderer != null) renderer.forceRenderingOff = true;
            if (meteorFlight != null)
            {
                if (flightTrail != null) flightTrail.forceRenderingOff = true;
                meteorFlight.transform.position = crystal.position;
                Vector3 velocity = crystal.position - previousPosition;
                if (velocity.sqrMagnitude > .000001f)
                    meteorFlight.transform.rotation = Quaternion.FromToRotation(Vector3.down, velocity.normalized);
                meteorFlight.transform.localScale = Vector3.one * meteorFlightScale;
                foreach (var ps in meteorSystems)
                {
                    var main = ps.main;
                    main.simulationSpeed = Mathf.Max(.01f, owner.PlaybackSpeed);
                }
            }
            previousPosition = crystal.position;

            if (!crystal.gameObject.activeInHierarchy)
            {
                if (!ended)
                {
                    foreach (var ps in systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    if (meteorSystems != null)
                        foreach (var ps in meteorSystems) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
                ended = true;
                return;
            }

            float pop = Mathf.Clamp01(crystal.localScale.x / Mathf.Max(owner.PrismScale, .001f));
            flash.transform.SetPositionAndRotation(crystal.position, crystal.rotation * Quaternion.Euler(-90f, 0f, 0f));
            flash.transform.localScale = Vector3.one * flashScale * pop;
            foreach (var ps in systems)
            {
                var main = ps.main;
                main.simulationSpeed = Mathf.Max(.01f, owner.PlaybackSpeed);
            }

            if (!appeared && !flightOnly && pop > .001f)
            {
                appeared = true;
                // Summon includes the shine's child layers; flight bursts wait
                // for the existing FlightTrail launch signal below.
                foreach (var ps in systems)
                    if (ps != sparks && ps != shockwave) ps.Play(false);
            }
            if (!launched && flightTrail != null && flightTrail.emitting)
            {
                launched = true;
                if (sparks != null) sparks.Play(false);
                if (shockwave != null) shockwave.Play(false);
                if (meteorSystems != null)
                    foreach (var ps in meteorSystems) ps.Play(false);
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.GetPropertyBlock(block);
                block.SetColor("_ColorBody", coreColor.a > 0f ? coreColor : tint * .85f);
                block.SetColor("_ColorRim", tint * 1.6f);
                bodyRenderer.SetPropertyBlock(block);
            }
            if (groundRenderer != null)
            {
                groundRenderer.GetPropertyBlock(block);
                block.SetColor("_Color", tint);
                groundRenderer.SetPropertyBlock(block);
            }
        }

        private void OnDisable()
        {
            if (flash != null) Destroy(flash);
            if (meteorFlight != null) Destroy(meteorFlight);
            flash = null;
            meteorFlight = null;
            meteorSystems = null;
        }
    }
}
