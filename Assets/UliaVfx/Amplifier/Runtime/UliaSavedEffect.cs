using System.Collections;
using System.Linq;
using UnityEngine;

namespace UliaVfx
{
    /// <summary>Reusable visual-only prefab. Instantiate or enable to play; no battle-scene dependency.</summary>
    public sealed class UliaSavedEffect : MonoBehaviour
    {
        public enum EffectKind { AmplifierCast, MutualDestructionTrajectory }
        public EffectKind effectKind;
        public bool playOnEnable = false;
        public Camera effectCamera;
        public UliaWormholeTrajectory ActiveTrajectory {get;private set;}
        public GameObject chargePrefab, coreGlowPrefab, arcPrefab, orbPrefab;
        public GameObject trajectoryPortalPrefab, trajectoryImpactPrefab, trajectoryVolumetricPortalPrefab;
        public GameObject trajectoryChargePrefab, trajectoryBranchPrefab, trajectoryLightningPrefab;
        public Shader wormholeShader, trajectoryCrispShader;
        public event System.Action ChargeReleased;
        [Min(.01f)] public float trajectoryChargeSeconds = .55f;
        [Min(.1f)] public float trajectoryWidthMultiplier = 1.85f;
        [Min(.01f)] public float trajectoryAfterglowSeconds = .1f;
        public float chargeSeconds = .55f, drawSeconds = .18f, holdSeconds = .6f;
        public Vector3 localEndPoint = new Vector3(0, 0, -12.95f);
        public Transform endTarget;
        private GameObject effects;
        void OnEnable()
        {
            if (Application.isPlaying && playOnEnable &&
                ((effectKind == EffectKind.AmplifierCast && chargePrefab && coreGlowPrefab) ||
                 (effectKind == EffectKind.MutualDestructionTrajectory && arcPrefab && orbPrefab))) Replay();
        }
        [ContextMenu("Replay Effect")]
        public void Replay()
        {
            if (!Application.isPlaying) return;
            StopAllCoroutines();
            if (effects) { effects.SetActive(false); Destroy(effects); }
            effects = new GameObject("Effect_Playback");
            effects.transform.SetParent(transform, false);
            StartCoroutine(Play());
        }
        IEnumerator Play()
        {
            if (effectKind == EffectKind.AmplifierCast)
            {
                yield return UliaAmplifierCastCharge.Play(effects.transform, transform, chargePrefab, coreGlowPrefab, chargeSeconds, () => ChargeReleased?.Invoke());
                yield break;
            }
            Vector3 start = transform.position;
            Vector3 end = endTarget ? endTarget.position : transform.TransformPoint(localEndPoint);
            if (trajectoryPortalPrefab && wormholeShader)
            {
                var wormhole = effects.AddComponent<UliaWormholeTrajectory>();
                ActiveTrajectory=wormhole;wormhole.effectCamera=effectCamera;
                wormhole.crispShader = trajectoryCrispShader;
                wormhole.chargeDuration = trajectoryChargeSeconds;
                wormhole.widthMultiplier = trajectoryWidthMultiplier;
                wormhole.afterglowSeconds = trajectoryAfterglowSeconds;
                yield return wormhole.Play(start, end, arcPrefab, trajectoryPortalPrefab, trajectoryImpactPrefab, wormholeShader, trajectoryVolumetricPortalPrefab, trajectoryChargePrefab, trajectoryBranchPrefab, trajectoryLightningPrefab);
                yield break;
            }
            float elapsed = 0;
            var orb = SpawnParticles(orbPrefab, start, .16f);
            orb.name = "Trajectory_Front_Orb";
            yield return new WaitForSecondsRealtime(.18f);
            var inner = CreateArcLayer("Trajectory_Bright_Inner_Lightning", .3f,
                new Vector4(36f, 28f, 45f, 1), start);
            var outer = CreateArcLayer("Trajectory_Deep_Purple_Afterglow", .9f,
                new Vector4(1.8f, .015f, 4.5f, 1), start);
            var graphs = new[] { inner, outer };

            elapsed = 0;
            while (elapsed < drawSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var tip = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / drawSeconds));
                foreach (var vfx in graphs) SetArc(vfx, start, tip);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(holdSeconds);
            inner.Stop();
            inner.gameObject.SetActive(false);
            orb.SetActive(false);
            // Purple sheath outlives the bright core, then fades away.
            elapsed = 0;
            while (elapsed < .4f)
            {
                elapsed += Time.unscaledDeltaTime;
                float strength = 1f - Mathf.Clamp01(elapsed / .4f);
                var color = new Vector4(1.8f * strength, .015f * strength, 4.5f * strength, 1);
                if (outer.HasVector4("ElectricityColor")) outer.SetVector4("ElectricityColor", color);
                if (outer.HasVector4("Color")) outer.SetVector4("Color", color);
                yield return null;
            }
            outer.Stop();
            outer.gameObject.SetActive(false);
        }

        UnityEngine.VFX.VisualEffect CreateArcLayer(string name, float thickness, Vector4 color, Vector3 start)
        {
            var arc = Instantiate(arcPrefab, effects.transform);
            arc.name = name;
            foreach (var script in arc.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            arc.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            arc.transform.localScale = Vector3.one;
            var graphs = arc.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true);
            // A single authored spline per layer keeps the core and halo aligned.
            foreach (var extra in graphs.Skip(1)) extra.gameObject.SetActive(false);
            var vfx = graphs[0];
            vfx.gameObject.SetActive(true);
            vfx.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            vfx.transform.localScale = Vector3.one;
            vfx.resetSeedOnPlay = false;
            vfx.startSeed = 1206;
            if (vfx.HasFloat("Thickness")) vfx.SetFloat("Thickness", thickness);
            if (vfx.HasVector4("ElectricityColor")) vfx.SetVector4("ElectricityColor", color);
            if (vfx.HasVector4("Color")) vfx.SetVector4("Color", color);
            SetArc(vfx, start, start + Vector3.back * .1f);
            vfx.Reinit();
            vfx.Play();
            return vfx;
        }

        GameObject SpawnParticles(GameObject prefab, Vector3 position, float scale)
        {
            var go = Instantiate(prefab, effects.transform);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * scale;
            foreach (var loop in go.GetComponentsInChildren<UliaEnergyGapLoop>(true)) loop.enabled = false;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.useUnscaledTime = true;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                if (ps.gameObject.activeInHierarchy) ps.Play(false);
            }
            return go;
        }

        static void SetArc(UnityEngine.VFX.VisualEffect vfx, Vector3 start, Vector3 end)
        {
            string[] names = { "Pos1", "Pos2", "Pos3", "Pos4" };
            for (int i = 0; i < 4; i++)
                if (vfx.HasVector3(names[i])) vfx.SetVector3(names[i], Vector3.Lerp(start, end, i / 3f));
        }

        void OnDisable() { StopAllCoroutines(); if (effects) effects.SetActive(false); }
    }
}
