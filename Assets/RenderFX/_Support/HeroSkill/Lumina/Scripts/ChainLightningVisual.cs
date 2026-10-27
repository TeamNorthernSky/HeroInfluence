using UnityEngine;
using UnityEngine.VFX;

namespace JC.VFX
{
    // Visual replacement only. The existing ChainLightningVfx/LightningShock
    // still own activation, endpoints, contacts, timing and completion.
    [DefaultExecutionOrder(1000)]
    public sealed class ChainLightningVisual : MonoBehaviour
    {
        public enum VisualKind { Muzzle, Bolt, Hit }
        [SerializeField] private VisualKind kind;
        [SerializeField] private GameObject visualPrefab;
        [SerializeField] private Color tint = new Color(0.2f, 0.55f, 1f, 1f);
        [SerializeField] private float width = 0.45f;

        private GameObject visual;
        private Renderer source;
        private ParticleSystem[] particles;
        private VisualEffect[] graphs;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private float speed = -1f;
        private static readonly int Opacity = Shader.PropertyToID("_Opacity");

        private void OnEnable()
        {
            source = GetComponent<Renderer>();
            if (source != null) source.forceRenderingOff = true;
            if (visualPrefab == null) return;
            visual = Instantiate(visualPrefab);
            visual.name = visualPrefab.name + " (Chain Visual)";
            particles = visual.GetComponentsInChildren<ParticleSystem>(true);
            graphs = visual.GetComponentsInChildren<VisualEffect>(true);
            renderers = visual.GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            foreach (var ps in particles)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.startColor = Recolor(main.startColor);
                // Hit is one burst per contact; muzzle lasts with its host.
                if (kind == VisualKind.Hit) main.loop = false;
            }
            UpdateTransform();
            UpdateSpeed();
            foreach (var ps in particles) ps.Play(false);
        }

        private ParticleSystem.MinMaxGradient Recolor(ParticleSystem.MinMaxGradient value)
        {
            if (value.mode == ParticleSystemGradientMode.Color)
                return new ParticleSystem.MinMaxGradient(Colorize(value.color));
            if (value.mode == ParticleSystemGradientMode.TwoColors)
                return new ParticleSystem.MinMaxGradient(Colorize(value.colorMin), Colorize(value.colorMax));
            if (value.gradientMin != null) value.gradientMin = RecolorGradient(value.gradientMin);
            if (value.gradientMax != null) value.gradientMax = RecolorGradient(value.gradientMax);
            if (value.mode == ParticleSystemGradientMode.Gradient || value.mode == ParticleSystemGradientMode.RandomColor)
                value.gradient = RecolorGradient(value.gradient);
            return value;
        }

        private Gradient RecolorGradient(Gradient original)
        {
            if (original == null) return null;
            var keys = original.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color = Colorize(keys[i].color);
            var result = new Gradient { mode = original.mode };
            result.SetKeys(keys, original.alphaKeys);
            return result;
        }

        private Color Colorize(Color original)
        {
            float brightness = Mathf.Max(original.r, original.g, original.b);
            return new Color(tint.r * brightness, tint.g * brightness, tint.b * brightness, original.a);
        }

        private void LateUpdate()
        {
            if (visual == null) return;
            if (source != null) source.forceRenderingOff = true;
            UpdateTransform();
            UpdateSpeed();
            if (source == null) return;
            source.GetPropertyBlock(block);
            float alpha = block.HasFloat(Opacity) ? block.GetFloat(Opacity) : 1f;
            foreach (var r in renderers)
            {
                // Existing shader fade is applied to the replacement renderers,
                // without modifying imported materials or lifecycle code.
                r.GetPropertyBlock(block);
                block.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
                block.SetColor("_BaseColor", new Color(1f, 1f, 1f, alpha));
                r.SetPropertyBlock(block);
            }
        }

        private void UpdateTransform()
        {
            visual.transform.position = transform.position;
            if (kind == VisualKind.Bolt)
            {
                // Imported LightningMesh spans y=0..-8; the existing bolt's X
                // axis and scale still supply the exact start/end positions.
                float length = transform.TransformVector(Vector3.right).magnitude;
                visual.transform.rotation = transform.rotation * Quaternion.FromToRotation(Vector3.down, Vector3.right);
                visual.transform.localScale = new Vector3(width, length / 8f, width);
            }
            else
            {
                visual.transform.rotation = transform.rotation;
                float size = kind == VisualKind.Muzzle ? Mathf.Max(.01f, transform.lossyScale.x) : width;
                visual.transform.localScale = Vector3.one * size;
            }
        }

        private void UpdateSpeed()
        {
            float value = 1f;
            foreach (var owner in GetComponentsInParent<VfxEffect>()) value = Mathf.Max(value, owner.PlaybackSpeed);
            if (Mathf.Approximately(value, speed)) return;
            speed = value;
            foreach (var ps in particles) { var main = ps.main; main.simulationSpeed = speed; }
            foreach (var graph in graphs) graph.playRate = speed;
        }

        private void OnDisable()
        {
            if (visual != null) Destroy(visual);
            visual = null;
            speed = -1f;
        }
    }
}
