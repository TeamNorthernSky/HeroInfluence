using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using JC.BattleTesting;

namespace JC.EnvironmentDetail
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class JcExploreRoadMarkings : JcRoadMarkingControl
    {
        [Tooltip("탐사 원본 대비 배율을 저장하는 프로필입니다. 모두 1이면 원본을 그대로 표시합니다.")]
        public JcExploreRoadProfile profile;
        public override bool IsBattle => false;
        public override JcRoadMarkingProfile CurrentProfile { get => profile; set => profile = value as JcExploreRoadProfile; }

        public sealed class Original
        {
            public Mesh mesh;
            public Material[] materials;
            public bool enabled;
            internal Material[] adjusted;
            internal bool authored;
        }
        static readonly Dictionary<MeshRenderer, Original> sourceRegistry = new Dictionary<MeshRenderer, Original>();
        readonly Dictionary<MeshRenderer, Original> originals = new Dictionary<MeshRenderer, Original>();
        readonly List<Material> owned = new List<Material>();
        HashSet<int> sourceIds = new HashSet<int>();
        GameObject output;
        JcBattleSurfaceDressing lines;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSources() => sourceRegistry.Clear();
        public static bool TryGetOriginal(MeshRenderer renderer, out Original original) => sourceRegistry.TryGetValue(renderer, out original);
        static bool Authored(string name) => name == "CenterLane_ThinDense_VisualOnly" || name == "NorthBackStreet_CenterLane";
        static string TextureName(Material material) => material != null && material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null ? material.GetTexture("_BaseMap").name : "";

        protected override void RefreshTargets(bool changed)
        {
            var filters = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
                .Where(f => f.sharedMesh != null && f.gameObject.activeInHierarchy && f.GetComponentInParent<JcRoadMarkingControl>() == null)
                .Where(f =>
                {
                    var renderer = f.GetComponent<MeshRenderer>();
                    if (renderer == null || renderer.forceRenderingOff) return false;
                    var mats = renderer.sharedMaterials;
                    bool enabled = renderer.enabled;
                    if (originals.TryGetValue(renderer, out var original)) { mats = original.materials; enabled = original.enabled; }
                    return enabled && (Authored(f.name) || mats.Any(m => TextureName(m) == "Road_01" || TextureName(m).StartsWith("Road_Centerline_", StringComparison.Ordinal)));
                }).ToArray();
            var ids = new HashSet<int>(filters.Select(f => f.GetInstanceID()));
            if (!sourceIds.SetEquals(ids))
            {
                RestoreTargets(); sourceIds = ids;
                if (filters.Length > 0)
                {
                    output = new GameObject("JC_ExploreRoadLines_Runtime") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
                    output.transform.SetParent(transform, false);
                    output.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    output.transform.localScale = new Vector3(1 / transform.lossyScale.x, 1 / transform.lossyScale.y, 1 / transform.lossyScale.z);
                    lines = output.AddComponent<JcBattleSurfaceDressing>();
                    lines.InitializeExplorationLines(filters);
                    foreach (var filter in filters)
                    {
                        var renderer = filter.GetComponent<MeshRenderer>();
                        var mats = renderer.sharedMaterials;
                        if (!Authored(filter.name) && !mats.Any(m => TextureName(m).StartsWith("Road_Centerline_", StringComparison.Ordinal))) continue;
                        var original = new Original { mesh = filter.sharedMesh, materials = mats, enabled = renderer.enabled,
                            authored = Authored(filter.name), adjusted = (Material[])mats.Clone() };
                        if (!original.authored)
                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (!TextureName(mats[i]).StartsWith("Road_Centerline_", StringComparison.Ordinal)) continue;
                            var material = new Material(mats[i]) { name = mats[i].name + "_ExploreRoadPlain", hideFlags = HideFlags.DontSave };
                            material.SetTexture("_BaseMap", JcBattleSurfaceDressing.Profile.plainAsphalt);
                            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", JcBattleSurfaceDressing.Profile.plainAsphalt);
                            owned.Add(material); original.adjusted[i] = material;
                        }
                        originals.Add(renderer, original); sourceRegistry[renderer] = original;
                    }
                }
                changed = true;
            }
            if (changed && lines != null)
            {
                bool keepOriginal = Values == Vector3.one;
                foreach (var pair in originals)
                {
                    if (pair.Key == null) continue;
                    pair.Key.sharedMaterials = keepOriginal ? pair.Value.materials : pair.Value.adjusted;
                    pair.Key.enabled = pair.Value.enabled && (keepOriginal || !pair.Value.authored);
                }
                lines.SetRoadValues(Values);
            }
            Status = $"탐사 차선 {originals.Count}개 대상 / " + (Values == Vector3.one ? "원본 외형 유지" : "원본 대비 배율 적용");
        }

        protected override void RestoreTargets()
        {
            foreach (var pair in originals)
            {
                if (pair.Key != null) { pair.Key.sharedMaterials = pair.Value.materials; pair.Key.enabled = pair.Value.enabled; }
                sourceRegistry.Remove(pair.Key);
            }
            originals.Clear(); sourceIds.Clear();
            if (output != null) { output.SetActive(false); Dispose(output); }
            output = null; lines = null;
            foreach (var item in owned) if (item != null) Dispose(item);
            owned.Clear();
        }
        static void Dispose(UnityEngine.Object item) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
    }
}
