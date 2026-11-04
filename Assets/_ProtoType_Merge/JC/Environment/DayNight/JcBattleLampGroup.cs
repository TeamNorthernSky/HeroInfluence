using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JC.Environment
{
    // JC | 2026-11-04 | 시간대별 전장 조명을 조절하고 가로등의 중심광과 주변광을 배치에 맞게 적용합니다.
    public sealed class JcBattleLampGroup : MonoBehaviour
    {
        readonly List<(Light core, Light spill)> lamps = new List<(Light, Light)>();
        JcBattleLampSettings lastSettings;
        int lastSegment = -1, lastRevision = -1;

        readonly Dictionary<Material, Material> lenses = new Dictionary<Material, Material>();
        Light nearest, fieldFill;
        Vector3 formationCenter;
        Volume glowVolume;
        Bloom bloom;

        internal void BindLens(MeshRenderer renderer)
        {
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                var original = slots[i];
                if (!original || !original.name.StartsWith("Lamp lens", System.StringComparison.Ordinal) ||
                    !original.HasProperty("_EmissionColor")) continue;
                if (!lenses.TryGetValue(original, out var lens))
                {
                    lens = new Material(original) { name = "전투 가로등 렌즈", hideFlags = HideFlags.DontSave };
                    lenses.Add(original, lens);
                }
                slots[i] = lens;
                renderer.sharedMaterials = slots;
                break;
            }
        }

        internal void Add(Matrix4x4 bulb)
        {
            var head = new GameObject("전투 가로등 광원").transform;
            head.SetParent(transform, false);
            head.localPosition = bulb.MultiplyPoint3x4(Vector3.zero);
            head.localRotation = Quaternion.LookRotation(bulb.MultiplyVector(Vector3.forward), bulb.MultiplyVector(Vector3.up));
            var core = CreateLight(head, "중심광");
            var spill = CreateLight(head, "주변광");
            lamps.Add((core, spill));
            lastSegment = -1;
        }

        static Light CreateLight(Transform parent, string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.enabled = false;
            light.type = LightType.Spot;
            light.shadows = LightShadows.None;
            return light;
        }

        void LateUpdate()
        {
            var settings = JcDayNightController.LampsFor(gameObject.scene);
            int segment = settings && settings.Draft ? settings.Segment : -1;
            int revision = settings ? settings.Revision : -1;
            if (lastSettings == settings && lastSegment == segment && lastRevision == revision) return;
            lastSettings = settings;
            lastSegment = segment;
            lastRevision = revision;
            var look = segment >= 0 ? settings.Draft.segments[segment] : null;
            ApplyLook(look);
        }

        void ApplyLook(JcBattleLampProfile.Segment look)
        {
            if (look != null && !nearest) ResolveFormation();
            foreach (var lamp in lamps)
            {
                Apply(lamp.core, look, look?.core);
                Apply(lamp.spill, look, look?.spill);
                float strength = lamp.core == nearest && lamp.core.enabled ? Mathf.Clamp01(look.shadowStrength) : 0;
                lamp.core.shadows = strength > 0 ? LightShadows.Soft : LightShadows.None;
                lamp.core.shadowStrength = strength;
            }
            ApplyFieldEffects(look);
        }

        void ResolveFormation()
        {
            Transform allies = null, enemies = null;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name == "PlayerPlace") allies = root.transform;
                if (root.name == "EnemyPlace") enemies = root.transform;
            }
            formationCenter = allies && enemies ? (allies.position + enemies.position) * .5f : transform.position;
            float best = float.PositiveInfinity;
            foreach (var lamp in lamps)
            {
                var delta = lamp.core.transform.position - formationCenter;
                float distance = delta.x * delta.x + delta.z * delta.z;
                if (distance >= best) continue;
                best = distance;
                nearest = lamp.core;
            }
        }

        void ApplyFieldEffects(JcBattleLampProfile.Segment look)
        {
            bool lit = nearest && look != null && look.lit;
            var fill = look?.fill;
            bool useFill = lit && fill != null && fill.enabled && fill.intensity > 0;
            if (useFill)
            {
                if (!fieldFill)
                {
                    fieldFill = CreateLight(transform, "전장 보조광");
                    fieldFill.type = LightType.Point;
                }
                var anchor = nearest.transform.position;
                anchor.y = formationCenter.y;
                fieldFill.transform.position = Vector3.Lerp(formationCenter, anchor, fill.towardLamp) + fill.offset;
                fieldFill.color = fill.color;
                fieldFill.intensity = fill.intensity;
                fieldFill.range = Mathf.Max(.01f, fill.range);
            }
            if (fieldFill) fieldFill.enabled = useFill;

            var glow = look?.glow;
            foreach (var pair in lenses)
            {
                bool emit = lit && glow != null && glow.emission > 0;
                if (emit || pair.Key.IsKeywordEnabled("_EMISSION")) pair.Value.EnableKeyword("_EMISSION");
                else pair.Value.DisableKeyword("_EMISSION");
                pair.Value.SetColor("_EmissionColor", pair.Key.GetColor("_EmissionColor") +
                    (emit ? look.color.linear * glow.emission : Color.black));
            }

            bool useBloom = lit && glow != null && glow.intensity > 0;
            if (useBloom && !glowVolume)
            {
                var go = new GameObject("가로등 블룸");
                go.transform.SetParent(transform, false);
                var camera = Camera.main;
                if (camera && camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                {
                    int mask = data.volumeLayerMask.value;
                    for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) { go.layer = i; break; }
                }
                glowVolume = go.AddComponent<Volume>();
                glowVolume.isGlobal = true;
                glowVolume.priority = 1001;
                glowVolume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                glowVolume.sharedProfile.hideFlags = HideFlags.DontSave;
                bloom = glowVolume.sharedProfile.Add<Bloom>(false);
            }
            if (glowVolume) glowVolume.enabled = useBloom;
            if (useBloom)
            {
                bloom.intensity.Override(glow.intensity);
                bloom.threshold.Override(glow.threshold);
                bloom.scatter.Override(glow.scatter);
            }
        }

        void OnDestroy()
        {
            foreach (var lens in lenses.Values) Release(lens);
            if (glowVolume)
            {
                var profile = glowVolume.sharedProfile;
                glowVolume.sharedProfile = null;
                Release(bloom);
                Release(profile);
            }
        }

        static void Release(Object item)
        {
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }

        static void Apply(Light light, JcBattleLampProfile.Segment look, JcBattleLampProfile.Cone cone)
        {
            light.enabled = look != null && look.lit && cone != null && cone.intensity > 0;
            if (!light.enabled) return;
            light.color = look.color;
            light.range = Mathf.Max(.01f, look.range);
            light.intensity = cone.intensity;
            light.spotAngle = Mathf.Clamp(cone.angle, 1, 179);
            light.innerSpotAngle = light.spotAngle * (1 - Mathf.Clamp01(cone.softness));
        }
    }
}
