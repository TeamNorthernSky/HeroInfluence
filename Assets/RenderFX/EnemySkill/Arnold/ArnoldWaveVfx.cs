using System.Collections;
using ASB.Work.Battle.Sequence;
using UnityEngine;

// Presentation only: the projectile action still owns flight and damage delivery.
public sealed class ArnoldWaveVfx : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle
{
    public GameObject orb;
    public GameObject charge;
    public GameObject trail;
    public GameObject impactPrefab;
    [Min(.01f)] public float chargeGrowSeconds = .9f;
    [Min(1f)] public float flightSpinMultiplier = 2.5f;
    private float speed = 1f;
    private Vector3 orbScale;
    private Vector3 chargeScale;
    private Transform leftHand, rightHand;
    private SkillPresentationData presentation;
    private bool launched;
    private bool orbStarted;
    private BattleCharactor impactTarget;

    private void OnEnable()
    {
        ProjectileImpactAction.FlightStarted += OnFlightStarted;
        ProjectileImpactAction.FlightTargetResolved += OnFlightTargetResolved;
        ProjectileImpactAction.FlightFinished += OnFlightFinished;
    }
    private void OnDisable()
    {
        ProjectileImpactAction.FlightStarted -= OnFlightStarted;
        ProjectileImpactAction.FlightTargetResolved -= OnFlightTargetResolved;
        ProjectileImpactAction.FlightFinished -= OnFlightFinished;
        if (charge != null && charge.transform.parent != transform) Destroy(charge);
    }
    public void Play(SkillEffectContext ctx)
    {
        launched = orbStarted = false;
        presentation = ctx?.Presentation;
        speed = ctx != null ? Mathf.Max(.01f, ctx.PlaybackSpeed) : 1f;
        var animator = ctx?.Caster != null ? ctx.Caster.GetComponentInChildren<Animator>() : null;
        if (animator != null && animator.isHuman)
        {
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        if (leftHand != null && rightHand != null)
        {
            transform.SetParent(null, true);
            transform.position = (leftHand.position + rightHand.position) * .5f;
        }
        else if (ctx?.SocketTransform != null)
        {
            transform.SetParent(ctx.SocketTransform, true);
            if (ctx.Caster != null && ctx.SocketTransform == ctx.Caster.transform)
                transform.position = ctx.Caster.transform.position + Vector3.up * 1.2f + ctx.Caster.transform.forward * .5f;
        }
        if (trail != null) trail.SetActive(false);
        if (charge != null) charge.SetActive(true);
        if (orb != null)
        {
            orbScale = orb.transform.localScale;
            orb.transform.localScale = orbScale * .2f;
            var droplets = orb.transform.Find("particle");
            if (droplets != null) droplets.gameObject.SetActive(false);
            orb.SetActive(false);
        }
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main; main.simulationSpeed = speed;
        }
        if (charge != null)
        {
            charge.transform.SetParent(transform, false);
            charge.transform.localPosition = Vector3.zero;
            chargeScale = charge.transform.localScale;
        }
        Signal(ctx);
    }
    private void LateUpdate()
    {
        if (!launched && leftHand != null && rightHand != null)
            transform.position = (leftHand.position + rightHand.position) * .5f;
    }
    private IEnumerator Grow()
    {
        float elapsed = 0f;
        while (!launched && elapsed < chargeGrowSeconds)
        {
            elapsed += Time.deltaTime * speed;
            if (orb != null) orb.transform.localScale = orbScale * Mathf.Lerp(.2f, 1f, Mathf.Clamp01(elapsed / chargeGrowSeconds));
            if (charge != null) charge.transform.localScale = chargeScale * Mathf.Lerp(1.2f, .6f, Mathf.Clamp01(elapsed / chargeGrowSeconds));
            yield return null;
        }
    }
    private void OnFlightStarted(BattleCharactor actor, Transform projectile)
    {
        if (projectile != transform) return;
        launched = true;
        if (charge != null) charge.SetActive(false);
        if (orb != null)
        {
            orb.transform.localScale = orbScale;
            var droplets = orb.transform.Find("particle");
            if (droplets != null)
            {
                foreach (var ps in droplets.GetComponentsInChildren<ParticleSystem>(true))
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                droplets.gameObject.SetActive(false);
            }
            var spinningRing = orb.transform.Find("mesh_circle");
            if (spinningRing != null)
            {
                var ps = spinningRing.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.simulationSpeed = speed * flightSpinMultiplier;
                }
            }
        }
        if (trail != null)
        {
            trail.SetActive(true);
            foreach (var ps in trail.GetComponentsInChildren<ParticleSystem>(true))
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (ps.gameObject.activeInHierarchy && renderer != null && renderer.enabled)
                {
                    var main = ps.main;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    ps.Clear(false);
                    ps.Play(false);
                }
            }
            var streak = trail.GetComponent<TrailRenderer>();
            if (streak != null) { streak.Clear(); streak.emitting = true; }
        }
    }
    private void OnFlightTargetResolved(Transform projectile, BattleCharactor target)
    {
        if (projectile == transform) impactTarget = target;
    }
    private void OnFlightFinished(BattleCharactor actor, Transform projectile, Vector3 position, bool arrived)
    {
        if (projectile != transform) return;
        if (orb != null) orb.SetActive(false);
        ClearTrail();
        if (!arrived) return;
        PresentationSfxEvents.PlayEffectImpact(presentation, 0);
        if (impactPrefab == null) return;
        var impact = Instantiate(impactPrefab, position, Quaternion.identity);
        impact.transform.localScale *= .42f;
        // Follow the camera-facing side of the victim, just like the other ranged impacts.
        var anchor = impact.AddComponent<VillainDroneImpactAnchor>();
        anchor.Target = impactTarget;
        anchor.UpdatePosition();
        var body = impactTarget != null ? impactTarget.GetComponentInChildren<SkinnedMeshRenderer>() : null;
        if (body != null)
            foreach (var renderer in impact.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                renderer.sortingLayerID = body.sortingLayerID;
                renderer.sortingOrder = body.sortingOrder + 1;
            }
        foreach (var ps in impact.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.simulationSpeed = speed; main.startDelay = 0f;
            var emission = ps.emission;
            var bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(bursts);
            for (int i = 0; i < bursts.Length; i++) { bursts[i].time = 0f; bursts[i].cycleCount = 1; }
            emission.SetBursts(bursts);
            ps.Play(false);
        }
        Destroy(impact, 2f / speed);
    }
    private void ClearTrail()
    {
        if (trail != null)
        {
            foreach (var ps in trail.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var streak in trail.GetComponentsInChildren<TrailRenderer>(true))
            {
                streak.emitting = false;
                streak.Clear();
            }
            trail.SetActive(false);
        }
    }
    public bool Signal(SkillEffectContext ctx)
    {
        if (!orbStarted && !launched)
        {
            orbStarted = true;
            if (orb != null) orb.SetActive(true);
            StartCoroutine(Grow());
        }
        return false; // Keep the prepared projectile handle until the launch marker takes ownership.
    }
    public void Stop() => Destroy(gameObject);
}
