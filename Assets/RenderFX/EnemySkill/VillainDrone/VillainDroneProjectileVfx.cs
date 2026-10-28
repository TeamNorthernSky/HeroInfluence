using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ASB.Work.Battle.Sequence;
using UnityEngine;

public sealed class VillainDroneProjectileVfx : MonoBehaviour
{
    public GameObject muzzlePrefab;
    public GameObject projectilePrefab;
    public GameObject impactPrefab;
    public GameObject additionalHitPrefab;
    public GameObject barrageTargetExplosionPrefab;
    [Min(.01f)] public float barrageTargetExplosionScale = 1f;
    public float additionalHitScale = .4f;
    public int AdditionalHitCount { get; private set; }
    public bool coneAcrossSkillArea;
    [Range(1, 8)] public int coneVolleySize = 4;
    public int skillIndex;
    public bool muzzleEveryShot;
    public bool recoilEveryShot;
    public bool useGlobalCommonHit;
    [Range(1, 64)] public int shotCount = 9;
    [Min(.01f)] public float shotInterval = .045f;
    public Vector2 hitSpread = new Vector2(1f, .65f);
    [Min(.01f)] public float projectileSpeed = 32f;
    [Min(.01f)] public float muzzleScale = .6f;
    [Min(.01f)] public float projectileScale = .6f;
    [Min(.01f)] public float impactScale = .18f;
    public Color projectileColor = new Color(1f, .38f, .06f, 1f);
    public int EmittedCount { get; private set; }
    public int ArrivedCount { get; private set; }
    public int ImpactCount { get; private set; }
    private BattleCharactor victim;
    private bool launched;
    private Transform muzzleSocket;
    private GunRecoilMotion recoil;
    private BattleCharactor shooter;
    private readonly List<Vector3> areaPoints = new List<Vector3>();
    private readonly List<GameObject> activeShots = new List<GameObject>();
    private readonly List<GameObject> activeEffects = new List<GameObject>();
    private readonly List<GameObject> activeMuzzles = new List<GameObject>();
    private readonly List<BattleCharactor> barrageTargets = new List<BattleCharactor>();
    private readonly List<(Vector3 position, BattleCharactor target)> barrageSlots = new List<(Vector3, BattleCharactor)>();
    public static bool IsFiringFor(BattleCharactor actor) => FindObjectsOfType<VillainDroneProjectileVfx>()
        .Any(v => v.shooter == actor && v.launched && v.EmittedCount < v.shotCount);
    public bool HasPendingPresentation => launched && (ArrivedCount < shotCount || activeEffects.Any(e => e != null && e.GetComponentsInChildren<ParticleSystem>(true).Any(p => p.IsAlive(false))));
    public static bool HasPendingFor(BattleCharactor actor) => FindObjectsOfType<VillainDroneProjectileVfx>().Any(v => v.shooter == actor && v.HasPendingPresentation);
    private void OnEnable()
    {
        ProjectileImpactAction.FlightDestinationResolved += Launch;
        ProjectileImpactAction.FlightTargetResolved += SetTarget;
    }
    private void SetTarget(Transform projectile, BattleCharactor target)
    { if (projectile == transform) victim = target; }
    private void OnDisable()
    {
        ProjectileImpactAction.FlightDestinationResolved -= Launch;
        ProjectileImpactAction.FlightTargetResolved -= SetTarget;
        StopAllCoroutines();
        ClearMuzzleEffects();
        foreach (var shot in activeShots) if (shot != null) Destroy(shot);
        activeShots.Clear();
        activeEffects.Clear();
        launched = false;
        EmittedCount = ArrivedCount = ImpactCount = AdditionalHitCount = 0;
        victim = null;
        areaPoints.Clear();
        barrageTargets.Clear();
        barrageSlots.Clear();
    }

    private void Launch(BattleCharactor actor, Transform projectile, Vector3 destination)
    {
        if (projectile != transform || launched) return;
        launched = true;
        shooter = actor;
        var battle = FindObjectOfType<BattleManager>();
        float speed = battle != null ? Mathf.Max(.01f, battle.CurrentBattleSpeed) : 1f;
        var targets = new List<BattleCharactor>();
        if (skillIndex == 200021 || skillIndex == 200022)
        {
            muzzleSocket = actor.GetComponentsInChildren<WeaponSocketHolder>(true).Select(w => w.Muzzle).FirstOrDefault(t => t != null);
            if (muzzleSocket == null) muzzleSocket = actor.GetComponentInChildren<UnitSocketHolder>()?.ResolveWeaponOrigin(WeaponSocket.Muzzle, true);
            if (muzzleSocket == null) muzzleSocket = actor.GetComponent<UnitVisualProfile>()?.AttackEffectSocket;
            recoil = actor.GetComponentInChildren<GunRecoilMotion>();
        }
        if (coneAcrossSkillArea && victim != null)
        {
            var skill = actor.availableSkills.FirstOrDefault(s => s.skillIndex == skillIndex);
            if (skill != null && ASB.Work.Battle.SkillExecution.SkillAreaPreviewHelper.TryGetAreaCells(actor, victim, skill, out var main, out var splash))
            {
                if (main != null && main.OccupyingUnit != null) targets.Add(main.OccupyingUnit);
                if (splash != null) targets.AddRange(splash.Where(c => c != null && c.OccupyingUnit != null).Select(c => c.OccupyingUnit));
                if (main != null) areaPoints.Add(main.transform.position);
                if (splash != null) areaPoints.AddRange(splash.Where(c => c != null).Select(c => c.transform.position));
                if (skillIndex == 200022)
                {
                    if (main != null) barrageSlots.Add((main.transform.position, main.OccupyingUnit));
                    if (splash != null)
                        foreach (var cell in splash)
                            if (cell != null) barrageSlots.Add((cell.transform.position, cell.OccupyingUnit));
                    barrageSlots.Sort((a, b) => a.position.z.CompareTo(b.position.z));
                }
            }
        }
        if (targets.Count == 0 && victim != null) targets.Add(victim);
        targets = targets.Where(t => !t.IsDead && t.IsPlayer != actor.IsPlayer).Distinct().OrderBy(t => t.transform.position.z).ToList();
        if (skillIndex == 200022)
        {
            // Use the committed AoE targets rather than the random cosmetic bullet lanes.
            var committedTargets = actor.GetComponent<PresentationRuntimeContext>()?.Current?.Targets;
            barrageTargets.AddRange((committedTargets ?? targets)
                .Where(t => t != null && t.IsPlayer != actor.IsPlayer).Distinct());
        }
        StartCoroutine(Burst(transform.position, destination, speed, targets));
    }
    private IEnumerator Burst(Vector3 origin, Vector3 destination, float speed, List<BattleCharactor> targets)
    {
        var direction = (destination - origin).normalized;
        var right = Vector3.Cross(Vector3.up, direction).normalized;
        if (right.sqrMagnitude < .001f) right = Vector3.right;
        var up = Vector3.Cross(direction, right).normalized;
        var schedule = new List<(float time, int lane)>();
        if (coneAcrossSkillArea)
        {
            int lanes = Mathf.Max(1, coneVolleySize);
            for (int lane = 0; lane < lanes; lane++)
            {
                float due = 0f;
                for (int n = lane; n < shotCount; n += lanes)
                {
                    schedule.Add((due, lane));
                    due += shotInterval * Random.Range(.6f, 1.4f);
                }
            }
            schedule = schedule.OrderBy(s => s.time).ToList();
        }
        float clock = 0f;
        for (int i = 0; i < shotCount; i++)
        {
            if (coneAcrossSkillArea)
                while (clock < schedule[i].time) { clock += Time.deltaTime * speed; yield return null; }
            var shotVictim = targets.Count > 0 ? targets[coneAcrossSkillArea ? i % targets.Count : 0] : victim;
            Vector3 endpoint = destination;
            if ((skillIndex == 200021 || coneAcrossSkillArea) && shotVictim != null)
            {
                var meshes = shotVictim.GetComponentsInChildren<SkinnedMeshRenderer>();
                endpoint = meshes.Length > 0 ? meshes[0].bounds.center : shotVictim.transform.position + Vector3.up * .8f;
            }
            if (muzzleSocket != null) origin = muzzleSocket.position;
            if (coneAcrossSkillArea && areaPoints.Count > 0)
            {
                var center = areaPoints.Aggregate(Vector3.zero, (sum, point) => sum + point) / areaPoints.Count;
                center.y += 1f;
                var horizontal = center - origin; horizontal.y = 0f;
                var fanRight = Vector3.Cross(Vector3.up, horizontal.normalized);
                float halfWidth = areaPoints.Max(point => Mathf.Abs(Vector3.Dot(point - center, fanRight))) + 1f;
                int lanes = Mathf.Max(1, coneVolleySize);
                float fraction = (schedule[i].lane + Random.Range(.15f, .85f)) / lanes;
                endpoint = center + fanRight * Mathf.Lerp(-halfWidth, halfWidth, fraction) + Vector3.up * Random.Range(-.35f, .35f);
                shotVictim = targets.OrderBy(t => Vector3.ProjectOnPlane(t.transform.position - endpoint, Vector3.up).sqrMagnitude).FirstOrDefault();
                if (shotVictim != null && Vector3.ProjectOnPlane(shotVictim.transform.position - endpoint, Vector3.up).sqrMagnitude > .8f * .8f) shotVictim = null;
            }
            // Keep the same full-column distribution even when some cells are empty.
            if (skillIndex == 200022 && barrageSlots.Count > 0)
            {
                var slot = barrageSlots[i % barrageSlots.Count];
                shotVictim = slot.target;
                var body = shotVictim != null ? shotVictim.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                endpoint = body != null ? body.bounds.center : slot.position + Vector3.up * .8f;
            }
            bool volleyStart = !coneAcrossSkillArea || i == 0 || schedule[i].time > schedule[i - 1].time + .0001f;
            if ((i == 0 || muzzleEveryShot) && volleyStart && muzzlePrefab != null)
            {
                if (shooter != null && skillIndex == 200021)
                {
                    Vector3 aim = endpoint - shooter.transform.position; aim.y = 0f;
                    if (aim.sqrMagnitude > .001f) shooter.transform.rotation = Quaternion.LookRotation(aim);
                    if (muzzleSocket != null) origin = muzzleSocket.position;
                }
                Vector3 muzzleDirection = endpoint - origin;
                if (coneAcrossSkillArea && areaPoints.Count > 0)
                {
                    Vector3 aimCenter = areaPoints.Aggregate(Vector3.zero, (sum, point) => sum + point) / areaPoints.Count;
                    aimCenter.y += 1f;
                    muzzleDirection = aimCenter - origin;
                }
                var muzzle = Instantiate(muzzlePrefab, origin, Quaternion.LookRotation(muzzleDirection.normalized));
                muzzle.transform.localScale *= muzzleScale;
                if (muzzleSocket != null) muzzle.transform.SetParent(muzzleSocket, true);
                ScaleSpeed(muzzle, speed);
                activeEffects.Add(muzzle);
                if (skillIndex == 200022) activeMuzzles.Add(muzzle);
                Destroy(muzzle, (skillIndex == 200022 ? .08f : 2f) / speed);
            }
            float angle = Random.value * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(Random.Range(.15f, 1f));
            var offset = right * (Mathf.Cos(angle) * radius * hitSpread.x) + up * (Mathf.Sin(angle) * radius * hitSpread.y);
            if (coneAcrossSkillArea) offset = Vector3.zero;
            if (recoilEveryShot && i > 0 && recoil != null) recoil.Kick();
            var hitPoint = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * .85f;
            if (skillIndex == 200022 && barrageSlots.Count > 0)
            {
                int targetShot = i / barrageSlots.Count;
                float spreadAngle = targetShot * 2.399963f + (i % barrageSlots.Count) * .7f;
                float spreadRadius = targetShot % 2 == 0 ? .75f : .45f;
                hitPoint = new Vector2(Mathf.Cos(spreadAngle), Mathf.Sin(spreadAngle)) * spreadRadius;
                var body = shotVictim != null ? shotVictim.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                if (Camera.main != null)
                    endpoint += Camera.main.transform.right * (hitPoint.x * (body != null ? body.bounds.extents.x : .5f))
                        + Camera.main.transform.up * (hitPoint.y * (body != null ? body.bounds.extents.y : .8f));
            }
            float bulletSpeed = coneAcrossSkillArea ? speed * Random.Range(.75f, 1.3f) : speed;
            StartCoroutine(Fly(origin, endpoint + offset, speed, hitPoint, shotVictim, bulletSpeed));
            EmittedCount++;
            if (skillIndex == 200022 && EmittedCount == shotCount) ClearMuzzleEffects();
            if (!coneAcrossSkillArea)
            {
                float elapsed = 0f;
                while (elapsed < shotInterval) { elapsed += Time.deltaTime * speed; yield return null; }
            }
        }
    }
    private void ClearMuzzleEffects()
    {
        foreach (var muzzle in activeMuzzles)
        {
            if (muzzle == null) continue;
            muzzle.SetActive(false);
            Destroy(muzzle);
        }
        activeMuzzles.Clear();
    }

    private IEnumerator Fly(Vector3 origin, Vector3 target, float speed, Vector2 hitPoint, BattleCharactor hitVictim, float flightSpeed)
    {
        if (projectilePrefab == null) yield break;
        var shot = Instantiate(projectilePrefab, origin, Quaternion.LookRotation((target - origin).normalized));
        activeShots.Add(shot);
        shot.transform.localScale *= projectileScale;
        foreach (var ps in shot.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startColor = projectileColor; main.simulationSpeed = speed;
            ps.Play(false);
        }
        float duration = Mathf.Max(.01f, Vector3.Distance(origin, target) / projectileSpeed);
        for (float t = 0f; t < duration; t += Time.deltaTime * flightSpeed)
        {
            if (shot == null) yield break;
            shot.transform.position = Vector3.Lerp(origin, target, t / duration);
            yield return null;
        }
        Destroy(shot);
        activeShots.Remove(shot);
        ArrivedCount++;
        PlayBarrageTargetExplosion(hitVictim, speed, hitPoint, target);
        if (impactPrefab != null && (!coneAcrossSkillArea || hitVictim != null))
        {
            Vector3 center = target;
            Renderer body = null;
            Bounds bodyBounds = new Bounds(target, Vector3.zero);
            if (hitVictim != null)
            {
                var bodies = hitVictim.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (bodies.Length > 0)
                {
                    Bounds bounds = bodies[0].bounds;
                    foreach (var mesh in bodies) bounds.Encapsulate(mesh.bounds);
                    center = bounds.center;
                    bodyBounds = bounds;
                    body = bodies[0];
                }
                else center = hitVictim.transform.position + Vector3.up * .8f;
            }
            var camera = Camera.main;
            if (camera != null)
            {
                Vector3 forward = camera.transform.forward;
                Vector3 extent = bodyBounds.extents;
                float depth = Mathf.Abs(forward.x) * extent.x + Mathf.Abs(forward.y) * extent.y + Mathf.Abs(forward.z) * extent.z;
                center -= forward * Mathf.Max(.12f, depth + .08f);
            }
            var impact = useGlobalCommonHit ? new GameObject("RifleHitAnchor") : Instantiate(impactPrefab, center, Quaternion.identity);
            impact.transform.position = center;
            if (hitVictim != null)
            {
                var anchor = impact.AddComponent<VillainDroneImpactAnchor>();
                anchor.Target = hitVictim;
                anchor.NormalizedOffset = hitPoint;
                anchor.UpdatePosition();
            }
            impact.transform.localScale *= impactScale;
            foreach (var renderer in impact.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sortingLayerID = body != null ? body.sortingLayerID : 0;
                renderer.sortingOrder = (body != null ? body.sortingOrder : 0) + 1;
            }
            if (!useGlobalCommonHit) PlaySingleImpact(impact, speed);
            activeEffects.Add(impact);
            ImpactCount++;
            if (additionalHitPrefab != null)
            {
                var extra = Instantiate(additionalHitPrefab, impact.transform.position, Quaternion.identity);
                extra.transform.localScale *= additionalHitScale;
                if (hitVictim != null)
                {
                    var anchor = extra.AddComponent<VillainDroneImpactAnchor>();
                    anchor.Target = hitVictim;
                    anchor.NormalizedOffset = hitPoint;
                    anchor.UpdatePosition();
                }
                foreach (var renderer in extra.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sortingLayerID = body != null ? body.sortingLayerID : 0;
                    renderer.sortingOrder = (body != null ? body.sortingOrder : 0) + 1;
                }
                PlaySingleImpact(extra, speed);
                activeEffects.Add(extra);
                AdditionalHitCount++;
                Destroy(extra, 3f / speed);
            }
            Destroy(impact, 3f / speed);
        }
    }
    private void PlayBarrageTargetExplosion(BattleCharactor target, float speed, Vector2 hitPoint, Vector3 impactPosition)
    {
        if ((skillIndex != 200021 && skillIndex != 200022) ||
            (target == null && skillIndex != 200022) || barrageTargetExplosionPrefab == null) return;
        var explosion = Instantiate(barrageTargetExplosionPrefab,
            impactPosition, Quaternion.identity);
        explosion.transform.localScale *= barrageTargetExplosionScale;
        if (target != null)
        {
            var anchor = explosion.AddComponent<VillainDroneImpactAnchor>();
            anchor.Target = target;
            anchor.NormalizedOffset = hitPoint;
            anchor.UpdatePosition();
        }
        // A brief hit flash for this bullet, without the source's prolonged emission.
        PlaySingleImpact(explosion, speed * 2f);
        activeEffects.Add(explosion);
        StartCoroutine(ReleaseBarrageExplosion(explosion));
    }

    private static IEnumerator ReleaseBarrageExplosion(GameObject explosion)
    {
        var particles = explosion.GetComponentsInChildren<ParticleSystem>(true);
        yield return null;
        while (explosion != null && particles.Any(p => p != null && p.IsAlive(false)))
            yield return null;
        if (explosion != null) Destroy(explosion);
    }

    private static void PlaySingleImpact(GameObject instance, float speed)
    {
        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.simulationSpeed = speed;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var sub = ps.subEmitters; sub.enabled = false;
            var emission = ps.emission;
            emission.rateOverTime = 0f; emission.rateOverDistance = 0f;
            if (emission.burstCount > 0)
            {
                var burst = emission.GetBurst(0); burst.cycleCount = 1;
                if (ps.name == "impact" || ps.name == "explosion") burst.count = 1;
                emission.SetBursts(new[] { burst });
            }
        }
        foreach (var ps in systems) ps.Play(false);
    }
    private static void ScaleSpeed(GameObject instance, float speed)
    {
        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        var subSystems = new HashSet<ParticleSystem>();
        foreach (var ps in systems)
            for (int i = 0; i < ps.subEmitters.subEmittersCount; i++) subSystems.Add(ps.subEmitters.GetSubEmitterSystem(i));
        foreach (var ps in systems)
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.simulationSpeed = speed; main.loop = false;
        }
        foreach (var ps in systems) if (!subSystems.Contains(ps)) ps.Play(false);
    }
}

[DefaultExecutionOrder(10000)]
public sealed class VillainDroneImpactAnchor : MonoBehaviour
{
    public BattleCharactor Target;
    public Vector2 NormalizedOffset;
    private void LateUpdate() => UpdatePosition();
    public void UpdatePosition()
    {
        if (Target == null || Camera.main == null) return;
        var bodies = Target.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (bodies.Length == 0) return;
        Bounds bounds = bodies[0].bounds;
        foreach (var body in bodies) bounds.Encapsulate(body.bounds);
        Vector3 forward = Camera.main.transform.forward;
        Vector3 extent = bounds.extents;
        float depth = Mathf.Abs(forward.x) * extent.x + Mathf.Abs(forward.y) * extent.y + Mathf.Abs(forward.z) * extent.z;
        Vector3 right = Camera.main.transform.right;
        Vector3 up = Camera.main.transform.up;
        float width = Mathf.Abs(right.x) * extent.x + Mathf.Abs(right.y) * extent.y + Mathf.Abs(right.z) * extent.z;
        float height = Mathf.Abs(up.x) * extent.x + Mathf.Abs(up.y) * extent.y + Mathf.Abs(up.z) * extent.z;
        transform.position = bounds.center - forward * (depth + .08f)
            + right * (NormalizedOffset.x * width) + up * (NormalizedOffset.y * height);
    }
}
