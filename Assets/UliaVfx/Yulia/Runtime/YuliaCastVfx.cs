using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.VFX;

namespace UliaVfx
{
    // Visual recording rail. No damage, turn progression, or combat summons.
    public sealed class YuliaCastVfx : MonoBehaviour
    {
        public GameObject castPrefab, portalPrefab, orbPrefab, rushPrefab, shieldPrefab;
        public AnimationClip startMotion, maintainMotion, idleMotion;
        [Header("Optional tile energy gather variant")]
        public GameObject summonGatherPrefab;
        [Min(.01f)] public float summonGatherScaleByHeight = .13f;
        [Min(.01f)] public float summonGatherNativeBurstSeconds = 2.7f;
        [Range(1f, 1.5f)] public float summonBurstPeak = 1.22f;
        private Material summonBodyMaterial;
        private readonly List<ParticleSystem> summonReleaseSystems = new List<ParticleSystem>();
        public float leadIn = 1.5f;
        public float chargeScale = .30f;
        public float portalScale = .34f;
        public float orbScale = .17f;
        public float riseSeconds = .8f;
        public float hoverHeight = .85f;
        public float spinSeconds = .912f;
        public float spinDegreesPerSecond = 1800f;
        public float splitTravelSeconds = .354667f;
        public float portalHoldSeconds = 0f;
        public float portalMorphSeconds = .083333f;
        public float portalFadeSeconds = .15f;
        public bool matchCharacterHeight = true;
        [SerializeField] private float characterHeight;
        private bool orbiting;
        private GameObject energyCore;
        private GameObject[] splitOrbs;
        private float orbitRadius, spinAngle;
        private Vector3 energyCenter;
        public bool replay;
        public bool rush;
        public bool autoRush;
        public float waitBeforeRush = 2.5f;

        private Transform boss, hand;
        private Animator animator;
        private Vector3[] slots;
        private System.Action<GameObject[]> onSummoned;
        private GameObject effects, charge, barrier;
        private GameObject[] orbs;
        private GameObject idleOrb;
        private Vector3 handOffset;
        private Quaternion handRotation;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable[] motions;
        private Vector3[] home;

        public void Play(Transform unit,Animator actorAnimator,Transform handSocket,Vector3[] destinations,float height,System.Action<GameObject[]> completed){
            if(!unit||!actorAnimator||!handSocket||destinations==null||destinations.Length!=3||!startMotion||!maintainMotion||!idleMotion)
                throw new System.ArgumentException("Assign unit, Animator, hand socket, three summon positions, and casting clips.");
            Stop();boss=unit;animator=actorAnimator;hand=handSocket;slots=(Vector3[])destinations.Clone();characterHeight=height;onSummoned=completed;
            handRotation=Quaternion.Euler(-28.183f,35.378f,124.165f);
            effects=new GameObject("DespairRing_Cast_Effects");effects.transform.SetParent(null);orbiting=false;
            spinSeconds=startMotion.length*.72f;splitTravelSeconds=startMotion.length-spinSeconds;
            portalMorphSeconds=maintainMotion.length*.25f;riseSeconds=maintainMotion.length*.75f;portalHoldSeconds=0;
            orbScale=characterHeight/5.3f;hoverHeight=characterHeight*.55f;portalScale=characterHeight*1.12f;
            CreateMotionGraph();StartCoroutine(CastAndSummon());
        }
        public void Stop(){
            StopAllCoroutines();orbiting=false;
            if(effects){effects.SetActive(false);Destroy(effects);}if(graph.IsValid())graph.Destroy();
            onSummoned=null;
        }
        private void LateUpdate()
        {
            if (!hand) return;
            energyCenter = hand.position + Vector3.up * (characterHeight * .18f);
            if (charge && charge.activeSelf)
            {
                charge.transform.position = energyCenter;
                charge.transform.rotation = Quaternion.AngleAxis(spinAngle, Vector3.up) * handRotation;
            }
            if (energyCore && orbiting)
            {
                energyCore.transform.position = energyCenter;
                energyCore.transform.rotation = Quaternion.AngleAxis(spinAngle, Vector3.forward);
            }
            if (!orbiting || splitOrbs == null) return;
            spinAngle += Time.unscaledDeltaTime * spinDegreesPerSecond;
            for (int i = 0; i < 3; i++)
            {
                float angle = (spinAngle + i * 120) * Mathf.Deg2Rad;
                splitOrbs[i].transform.position = energyCenter + new Vector3(Mathf.Cos(angle), .28f * Mathf.Sin(angle * 2), Mathf.Sin(angle)) * orbitRadius;
                splitOrbs[i].transform.rotation = Quaternion.AngleAxis(spinAngle, Vector3.up);
            }
        }

        private GameObject Spawn(GameObject prefab, Vector3 position, float scale, Quaternion rotation)
        {
            var go = Instantiate(prefab, position, rotation, effects.transform);
            go.transform.localScale = Vector3.one * scale;
            foreach (var loop in go.GetComponentsInChildren<UliaEnergyGapLoop>(true)) loop.enabled = false;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.useUnscaledTime = true;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                if (ps.gameObject.activeInHierarchy) ps.Play(false);
            }
            foreach (var vfx in go.GetComponentsInChildren<VisualEffect>(true))
            {
                vfx.Reinit();
                vfx.Play();
            }
            return go;
        }

        private void StartTileGather()
        {
            summonReleaseSystems.Clear();
            if (!summonGatherPrefab) return;
            // Run the original gather/release cue on three destination tiles in parallel
            // with the unmodified animation. Release matches the first orb-rise frame.
            float burstAt = spinSeconds + splitTravelSeconds + portalMorphSeconds + portalHoldSeconds;
            float speed = summonGatherNativeBurstSeconds / Mathf.Max(.1f, burstAt);
            float scale = characterHeight * summonGatherScaleByHeight;
            for (int i = 0; i < slots.Length; i++)
            {
                var go = Spawn(summonGatherPrefab, slots[i] + Vector3.up * (hoverHeight - 1.77f * scale),
                    scale, Quaternion.identity);
                go.name = "Summon_Tile_EnergyGather_" + i;
                // Native charge and burst center at y=1.77: align it with the final orb center.
                foreach (var floor in go.GetComponentsInChildren<Transform>(true))
                    if (floor.name == "target_glow" || floor.name == "target_shadow")
                        floor.position = slots[i] + Vector3.up * .04f;
                float end = 0;
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = ps.main;
                    main.loop = false;
                    main.simulationSpeed = speed;
                    main.useUnscaledTime = true;
                    main.stopAction = ParticleSystemStopAction.None;
                    main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                    end = Mathf.Max(end, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
                    var releaseRoot = go.transform.Find("explosion");
                    bool release = (releaseRoot && (ps.transform == releaseRoot || ps.transform.IsChildOf(releaseRoot)))
                        || main.startDelay.constantMin >= summonGatherNativeBurstSeconds - .1f;
                    if (release)
                    {
                        // Release is an explicit cue shared with orb birth, not a second timer.
                        main.startDelay = 0;
                        if (ps.gameObject.activeInHierarchy) summonReleaseSystems.Add(ps);
                    }
                    else if (ps.gameObject.activeInHierarchy) ps.Play(false);
                }
                // Replays destroy the whole effects root; the native tail otherwise expires once.
                Destroy(go, end / speed + .25f);
            }
            Debug.Log("[Yulia summon gather] Three tile cues; native burst=" + summonGatherNativeBurstSeconds
                + "s; animation burst=" + burstAt + "s; particle speed=" + speed + "; scale=" + scale);
        }

        private void CreateMotionGraph()
        {
            graph = PlayableGraph.Create("Yulia Ring Cast Recording");
            graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
            var output = AnimationPlayableOutput.Create(graph, "Yulia", animator);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            output.SetSourcePlayable(mixer);
            var clips = new[] { startMotion, maintainMotion, idleMotion };
            motions = new AnimationClipPlayable[3];
            for (int i = 0; i < 3; i++)
            {
                motions[i] = AnimationClipPlayable.Create(graph, clips[i]);
                motions[i].SetSpeed(1.0);
                motions[i].SetApplyFootIK(false);
                motions[i].SetApplyPlayableIK(false);
                graph.Connect(motions[i], 0, mixer, i);
            }
            SetMotion(2);
            graph.Play();
            Debug.Log("[Yulia cast preview] Animator=" + animator.name + ", start=" + startMotion.length + "s; summon slots=" + string.Join(", ", slots.Select(t => t.ToString())));
        }

        private void SetMotion(int index)
        {
            for (int i = 0; i < 3; i++) mixer.SetInputWeight(i, i == index ? 1 : 0);
            motions[index].SetTime(0);
        }

        private GameObject EnergyOrb(Vector3 position, float scale, bool trail)
        {
            var orb = Spawn(orbPrefab, position, scale, Quaternion.identity);
            var vfx = orb.GetComponent<VisualEffect>();
            if (vfx.HasVector4("Color")) vfx.SetVector4("Color", new Vector4(2.2f, .35f, 5, 1));
            if (vfx.HasVector4("Color02")) vfx.SetVector4("Color02", new Vector4(3.4f, .35f, 6, 1));
            if (trail)
            {
                var source = castPrefab.GetComponentsInChildren<ParticleSystemRenderer>(true).FirstOrDefault(r => r.name == "orbs");
                var ribbon = orb.AddComponent<TrailRenderer>();
                ribbon.sharedMaterial = source.sharedMaterial;
                ribbon.time = .16f;
                ribbon.minVertexDistance = .02f;
                ribbon.startWidth = characterHeight * .045f;
                ribbon.endWidth = 0;
                ribbon.startColor = new Color(.75f, .2f, 1, .8f);
                ribbon.endColor = new Color(.35f, .05f, 1, 0);
                ribbon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ribbon.receiveShadows = false;
            }
            return orb;
        }

        private GameObject BurstReadyOrb(Vector3 position)
        {
            var orb = EnergyOrb(position, orbScale, false);
            if (!summonBodyMaterial)
            {
                summonBodyMaterial = new Material(Shader.Find("UliaVfx/VFX/UliaOrbDenseBody"));
                summonBodyMaterial.SetColor("_BaseColor", new Color(.20f, .012f, .38f, .62f));
            }
            // Immediate visible shell; GPU particles are warmed before the first render,
            // so the release never waits for the native continuous emission to fill in.
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Dense_Purple_Inner_Body";
            body.transform.SetParent(orb.transform, false);
            body.transform.localScale = Vector3.one * 4.5f;
            body.GetComponent<Collider>().enabled = false;
            var renderer = body.GetComponent<Renderer>();
            renderer.sharedMaterial = summonBodyMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            foreach (var v in orb.GetComponentsInChildren<VisualEffect>(true))
            {
                if (v.HasVector4("Color")) v.SetVector4("Color", new Vector4(6, .65f, 12, 1));
                if (v.HasVector4("Color02")) v.SetVector4("Color02", new Vector4(9, .6f, 16, 1));
                v.Reinit(); v.Play(); v.Simulate(1f / 60f, 60);
            }
            return orb;
        }

        private GameObject GroundPortal(Vector3 position)
        {
            var portal = Spawn(portalPrefab, position, 1, Quaternion.identity);
            portal.name = "Ground_Summon_Circle";
            ParticleSystem circle = null;
            foreach (var ps in portal.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (ps.name != "circles")
                {
                    if (ps.gameObject != portal) ps.gameObject.SetActive(false);
                    continue;
                }
                circle = ps;
                ps.transform.localPosition = Vector3.zero;
                ps.transform.localRotation = Quaternion.identity;
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.startSpeed = 0;
                main.startLifetime = 10000;
                main.startSize = portalScale;
                main.startRotation3D = true;
                main.startRotationX = Mathf.PI * .5f;
                main.startRotationY = 0;
                main.startRotationZ = 0;
                main.startColor = new Color(.72f, .22f, 1, 1);
                var shape = ps.shape; shape.enabled = false;
                var size = ps.sizeOverLifetime; size.enabled = false;
                var color = ps.colorOverLifetime; color.enabled = false;
                var emission = ps.emission; emission.enabled = false;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.alignment = ParticleSystemRenderSpace.Local;
                ps.Emit(1);
                ps.Pause(false);
            }
            // A second ring makes the destination readable from the battle camera.
            if (circle)
            {
                var inner = Instantiate(circle.gameObject, portal.transform);
                inner.name = "Inner_Summon_Circle";
                inner.transform.localScale = Vector3.one * .72f;
                var ps = inner.GetComponent<ParticleSystem>();
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Emit(1);
                ps.Pause(false);
            }
            portal.transform.localScale = Vector3.one * .02f;
            return portal;
        }

        private IEnumerator CastAndSummon()
        {
            if (idleOrb) idleOrb.SetActive(true);
            yield return new WaitForSecondsRealtime(leadIn);
            if (idleOrb) idleOrb.SetActive(false);
            SetMotion(0);
            StartTileGather();
            charge = Spawn(castPrefab, hand.position, characterHeight * .08f, handRotation);
            foreach (var ps in charge.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.startColor = new Color(.55f, .15f, .9f, .8f);
            }
            energyCore = EnergyOrb(hand.position, .025f, false);
            splitOrbs = Enumerable.Range(0, 3).Select(i => EnergyOrb(hand.position, .025f, true)).ToArray();
            orbitRadius = characterHeight * .035f;
            spinAngle = 0;
            orbiting = true;
            float elapsed = 0;
            while (elapsed < spinSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / spinSeconds));
                energyCore.transform.localScale = Vector3.one * Mathf.Lerp(.025f, .21f, p);
                orbitRadius = characterHeight * Mathf.Lerp(.035f, .32f, p);
                for (int i = 0; i < 3; i++) splitOrbs[i].transform.localScale = Vector3.one * Mathf.Lerp(.025f, .075f, p);
                yield return null;
            }
            orbiting = false;
            charge.SetActive(false);
            var starts = splitOrbs.Select(o => o.transform.position).ToArray();
            var destinations = slots.Select(t => t + Vector3.up * .09f).ToArray();
            elapsed = 0;
            while (elapsed < splitTravelSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / splitTravelSeconds));
                energyCore.transform.localScale = Vector3.one * Mathf.Lerp(.21f, .001f, Mathf.Clamp01(p * 5));
                for (int i = 0; i < 3; i++)
                {
                    Vector3 control = Vector3.Lerp(starts[i], destinations[i], .45f) + Vector3.up * characterHeight * .35f;
                    splitOrbs[i].transform.position = (1-p)*(1-p)*starts[i] + 2*(1-p)*p*control + p*p*destinations[i];
                    splitOrbs[i].transform.localScale = Vector3.one * Mathf.Lerp(.075f, .12f, p);
                }
                yield return null;
            }
            energyCore.SetActive(false);
            SetMotion(1);
            var portals = destinations.Select(GroundPortal).ToArray();
            elapsed = 0;
            while (elapsed < portalMorphSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / portalMorphSeconds);
                for (int i = 0; i < 3; i++)
                {
                    splitOrbs[i].transform.localScale = new Vector3(.12f, Mathf.Lerp(.12f, .002f, p), .12f) * (1-p);
                    portals[i].transform.localScale = Vector3.one * Mathf.Lerp(.02f, 1, Mathf.SmoothStep(0, 1, p));
                }
                yield return null;
            }
            foreach (var o in splitOrbs) o.SetActive(false);
            if (portalHoldSeconds > 0) yield return new WaitForSecondsRealtime(portalHoldSeconds);
            home = slots.Select(t => t + Vector3.up * hoverHeight).ToArray();
            elapsed = 0;
            if (summonGatherPrefab)
            {
                foreach (var ps in summonReleaseSystems) if (ps) ps.Play(false);
                orbs = home.Select(BurstReadyOrb).ToArray();
                Debug.Log("[Yulia summon burst] Full-size visible shells on release; no rise or gradual birth.");
                while (elapsed < riseSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    // Already full sized on the first frame: 45ms outward punch, 115ms recoil.
                    float multiplier = elapsed < .045f
                        ? Mathf.Lerp(1, summonBurstPeak, Mathf.Clamp01(elapsed / .045f))
                        : Mathf.Lerp(summonBurstPeak, 1, Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - .045f) / .115f)));
                    foreach (var orb in orbs) orb.transform.localScale = Vector3.one * (orbScale * multiplier);
                    yield return null;
                }
                foreach (var orb in orbs) orb.transform.localScale = Vector3.one * orbScale;
            }
            else
            {
                orbs = destinations.Select(p => EnergyOrb(p, .015f, false)).ToArray();
                elapsed = 0;
                while (elapsed < riseSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float p = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / riseSeconds));
                    for (int i = 0; i < 3; i++)
                    {
                        orbs[i].transform.position = Vector3.Lerp(destinations[i], home[i], p);
                        orbs[i].transform.localScale = Vector3.one * Mathf.Lerp(.015f, orbScale, p);
                    }
                    yield return null;
                }
            }
            SetMotion(2);
            elapsed = 0;
            while (elapsed < portalFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                foreach (var portal in portals) portal.transform.localScale = Vector3.one * Mathf.Lerp(1, .001f, Mathf.Clamp01(elapsed / portalFadeSeconds));
                yield return null;
            }
            foreach (var portal in portals) portal.SetActive(false);
            if (idleOrb) idleOrb.SetActive(true);
            Debug.Log("[Yulia cast preview v3] Original clips at 1x; casting=" + (startMotion.length + maintainMotion.length) + "s; effects settled=" + (startMotion.length + maintainMotion.length + portalFadeSeconds) + "s. Character height=" + characterHeight + ", orb diameter=" + (orbScale * 5.3f) + ", portal diameter=" + portalScale);
            if(orbs!=null){
                var born=orbs;orbs=null;foreach(var orb in born)orb.transform.SetParent(null,true);
                var callback=onSummoned;onSummoned=null;callback?.Invoke(born);
            }
            if(graph.IsValid())graph.Destroy();
        }

        private IEnumerator Rush()
        {
            var from = orbs.Select(o => o.transform.position).ToArray();
            Vector3 direction = Vector3.back;
            var trails = orbs.Select(o => Spawn(rushPrefab, o.transform.position, .24f, Quaternion.LookRotation(direction))).ToArray();
            float elapsed = 0;
            while (elapsed < 1.1f)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / 1.1f);
                for (int i = 0; i < 3; i++)
                {
                    orbs[i].transform.position = from[i] + direction * (p * 6.5f);
                    trails[i].transform.position = orbs[i].transform.position;
                }
                yield return null;
            }
            foreach (var o in orbs) o.SetActive(false);
            foreach (var t in trails) t.SetActive(false);
        }

        private void CreateBarrier()
        {
            if (!shieldPrefab) return;
            var renderers = boss.GetComponentsInChildren<SkinnedMeshRenderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            float size = Mathf.Max(bounds.size.y, Mathf.Max(bounds.size.x, bounds.size.z)) * 1.2f / 2.3f;
            barrier = Spawn(shieldPrefab, bounds.center, size, Quaternion.identity);
            barrier.name = "Yulia_Shield_Maintained_During_Cast";
            foreach (var ps in barrier.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (ps.name != "shield_add" && ps.name != "shield_AB")
                {
                    if (ps.gameObject != barrier) ps.gameObject.SetActive(false);
                    continue;
                }
                ps.transform.localPosition = Vector3.zero;
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startSpeed = 0;
                main.startSize = 1;
                main.startLifetime = 10000;
                main.startColor = new Color(.48f, .12f, .8f, .24f);
                var shape = ps.shape; shape.enabled = false;
                var sizeModule = ps.sizeOverLifetime; sizeModule.enabled = false;
                var color = ps.colorOverLifetime; color.enabled = false;
                var emission = ps.emission; emission.enabled = false;
                ps.Emit(1);
                ps.Pause(false);
            }
        }

        private void OnDestroy() { if (summonBodyMaterial) Destroy(summonBodyMaterial); }

        private void OnDisable() { Stop(); }
    }

}
