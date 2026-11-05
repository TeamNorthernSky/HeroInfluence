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
        // JC | 2026-11-04 | 도로·차선을 부분별로 조명할 수 있게 해 가로등 하나의 영향이 도로 전체로 퍼지는 현상을 줄입니다.
        readonly List<JcLampSurfaceChunks> litSurfaces = new List<JcLampSurfaceChunks>();
        readonly HashSet<int> checkedSurfaces = new HashSet<int>();
        int surfaceReadyFrame;

        protected override void OnEnable()
        {
            // 레벨 로드 직후 베이크 외형이 원본을 교체한 다음 보이는 표면만 분할합니다.
            surfaceReadyFrame = Time.frameCount + 2;
            base.OnEnable();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSources() => sourceRegistry.Clear();
        public static bool TryGetOriginal(MeshRenderer renderer, out Original original) => sourceRegistry.TryGetValue(renderer, out original);
        static bool Authored(string name) => name == "CenterLane_ThinDense_VisualOnly" || name == "NorthBackStreet_CenterLane";
        static string TextureName(Material material) => material != null && material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null ? material.GetTexture("_BaseMap").name : "";

        protected override void RefreshTargets(bool changed)
        {
            var all = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
                .Where(f => f.name != JcLampSurfaceChunks.VisualName && f.sharedMesh != null && f.gameObject.activeInHierarchy && f.GetComponentInParent<JcRoadMarkingControl>() == null).ToArray();
            var filters = all.Where(f =>
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
                bool keepOriginal = Values == Vector3.one && lineColor == JcRoadMarkingProfile.DefaultColor;
                foreach (var pair in originals)
                {
                    if (pair.Key == null) continue;
                    pair.Key.sharedMaterials = keepOriginal ? pair.Value.materials : pair.Value.adjusted;
                    pair.Key.enabled = pair.Value.enabled && (keepOriginal || !pair.Value.authored);
                }
                lines.SetRoadValues(Values, lineColor);
            }
            if (Application.isPlaying && gameObject.scene.name == "DHScene_3" && Time.frameCount >= surfaceReadyFrame)
                RefreshLampSurfaces(all, changed);
            Status = $"탐사 차선 {originals.Count}개 대상 / " + (Values == Vector3.one && lineColor == JcRoadMarkingProfile.DefaultColor ? "원본 외형 유지" : "원본 대비 배율 적용");
        }

        void RefreshLampSurfaces(MeshFilter[] all, bool changed)
        {
            List<Light> lamps = null;
            foreach (var f in all)
            {
                if (!IsLampSurface(f.name) || checkedSurfaces.Contains(f.GetInstanceID())) continue;
                var r = f.GetComponent<MeshRenderer>();
                if (!r || !r.enabled || r.forceRenderingOff) continue;
                if (lamps == null) lamps = gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true))
                    .Where(l => l.name == "LampSpotLight" && l.type == LightType.Spot).ToList();
                if (lamps.Count == 0) break;
                checkedSurfaces.Add(f.GetInstanceID());
                JcLampSurfaceChunks.Build(f, lamps);
                var split = f.GetComponent<JcLampSurfaceChunks>();
                if (split) litSurfaces.Add(split);
            }
            litSurfaces.RemoveAll(s => !s);
            foreach (var surface in litSurfaces)
            {
                if (changed) surface.SyncAppearance();
                surface.SyncVisibility();
            }
        }

        static bool IsLampSurface(string name)
        {
            if (name == "연속 연석: 보도·야지 경계") return true;
            if (!(name.StartsWith("TileChunk_", StringComparison.Ordinal) || name.StartsWith("Detail_TileChunk_", StringComparison.Ordinal))) return false;
            return name.Contains("_Road_") || name.EndsWith("_tilemap_01_RaisedCube", StringComparison.Ordinal) ||
                name.EndsWith("_tilemap_04_FlatCube", StringComparison.Ordinal);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            foreach (var surface in litSurfaces) if (surface) Dispose(surface);
            litSurfaces.Clear();
            checkedSurfaces.Clear();
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
