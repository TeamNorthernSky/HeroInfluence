using System.Collections.Generic;
using ASB.Work.Battle.Sequence;
using UnityEngine;

// The working fireball keeps one visual center from charging through launch.
public sealed class LuminaFireProjectileVisual : MonoBehaviour
{
    [SerializeField] private GameObject[] chargeVisualRoots;
    [SerializeField] private GameObject fireVisual;
    [SerializeField, Min(.01f)] private float fireAppearSeconds = .24f;
    [SerializeField, Range(0f, 1f)] private float fireStartScale = .08f;

    private ParticleSystem[] fireSystems, allSystems;
    private ParticleSystem.MinMaxCurve[] flightSpeeds;
    private float[] simulationSpeeds;
    private ParticleSystem.Particle[][] buffers;
    private Dictionary<uint, Color32>[] originalColors;
    private Vector3 fireScale;
    private Quaternion fireRotation;
    private float elapsed, playbackSpeed = 1f;
    private bool flying, finished;

    private void Initialize()
    {
        if (fireSystems != null || fireVisual == null) return;
        fireScale = fireVisual.transform.localScale;
        fireRotation = fireVisual.transform.localRotation;
        fireSystems = fireVisual.GetComponentsInChildren<ParticleSystem>(true);
        allSystems = GetComponentsInChildren<ParticleSystem>(true);
        simulationSpeeds = new float[allSystems.Length];
        for (int i = 0; i < allSystems.Length; i++)
            simulationSpeeds[i] = allSystems[i].main.simulationSpeed;
        flightSpeeds = new ParticleSystem.MinMaxCurve[fireSystems.Length];
        buffers = new ParticleSystem.Particle[fireSystems.Length][];
        originalColors = new Dictionary<uint, Color32>[fireSystems.Length];
        for (int i = 0; i < fireSystems.Length; i++)
        {
            flightSpeeds[i] = fireSystems[i].main.startSpeed;
            buffers[i] = new ParticleSystem.Particle[Mathf.Max(1, fireSystems[i].main.maxParticles)];
            originalColors[i] = new Dictionary<uint, Color32>();
        }
    }

    private void OnEnable()
    {
        Initialize();
        if (fireVisual == null) return;
        var battle = FindFirstObjectByType<BattleManager>();
        playbackSpeed = Mathf.Max(.01f, battle != null ? battle.CurrentBattleSpeed : 1f);
        for (int i = 0; i < allSystems.Length; i++)
        {
            var main = allSystems[i].main;
            main.simulationSpeed = simulationSpeeds[i] * playbackSpeed;
        }
        elapsed = 0f;
        flying = finished = false;
        // Old mesh orbs must not overlap the replacement fireball. Keep the gathering effect.
        foreach (var root in chargeVisualRoots)
            if (root != null)
            {
                root.transform.localPosition = Vector3.zero;
                if (root.name == "FlareBombChargeOrb" || root.name == "FB_M2_Volume")
                    root.SetActive(false);
            }
        fireVisual.transform.localPosition = Vector3.zero;
        fireVisual.transform.localRotation = Quaternion.identity;
        for (int i = 0; i < fireSystems.Length; i++)
        {
            originalColors[i].Clear();
            var ps = fireSystems[i];
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            // Charge particles stay at the shared center instead of drifting away at flight speed.
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            var shape = ps.shape;
            shape.position = Vector3.zero;
            ps.Play(false);
        }
        SetFireVisible(true);
        ApplyAppearance(0f);
        ProjectileImpactAction.FlightStarted += OnFlightStarted;
        ProjectileImpactAction.FlightFinished += OnFlightFinished;
    }

    private void OnDisable()
    {
        ProjectileImpactAction.FlightStarted -= OnFlightStarted;
        ProjectileImpactAction.FlightFinished -= OnFlightFinished;
    }

    private void LateUpdate()
    {
        if (fireVisual == null || finished) return;
        if (!flying) elapsed += Time.deltaTime * playbackSpeed;
        float amount = flying ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fireAppearSeconds));
        ApplyAppearance(amount);
    }

    private void ApplyAppearance(float amount)
    {
        fireVisual.transform.localScale = fireScale * Mathf.Lerp(fireStartScale, 1f, amount);
        for (int i = 0; i < fireSystems.Length; i++)
        {
            var ps = fireSystems[i];
            if (ps == null) continue;
            int count = ps.GetParticles(buffers[i]);
            for (int j = 0; j < count; j++)
            {
                var particle = buffers[i][j];
                if (!originalColors[i].TryGetValue(particle.randomSeed, out Color32 color))
                {
                    color = particle.startColor;
                    originalColors[i][particle.randomSeed] = color;
                }
                color.a = (byte)Mathf.RoundToInt(color.a * amount);
                particle.startColor = color;
                buffers[i][j] = particle;
            }
            ps.SetParticles(buffers[i], count);
        }
    }

    private void OnFlightStarted(BattleCharactor actor, Transform projectile)
    {
        if (projectile != transform || fireVisual == null) return;
        flying = true;
        foreach (var root in chargeVisualRoots)
            if (root != null) root.SetActive(false);
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            if (!renderer.transform.IsChildOf(fireVisual.transform)) renderer.enabled = false;
        fireVisual.transform.localRotation = fireRotation;
        for (int i = 0; i < fireSystems.Length; i++)
        {
            var main = fireSystems[i].main;
            main.startSpeed = flightSpeeds[i];
        }
        // Keep the already formed particles: no clear/restart flash at launch.
        ApplyAppearance(1f);
        SetFireVisible(true);
    }

    private void OnFlightFinished(BattleCharactor actor, Transform projectile, Vector3 point, bool arrived)
    {
        if (projectile != transform || fireVisual == null) return;
        finished = true;
        foreach (var ps in fireSystems)
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetFireVisible(false);
    }

    private void SetFireVisible(bool visible)
    {
        if (fireVisual == null) return;
        foreach (var renderer in fireVisual.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = visible;
    }
}