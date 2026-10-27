using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JC.Indicators
{
    // 원본 FBX/재질/콜라이더를 유지하며 연결된 기둥 면만 실행 중에 분리합니다.
    internal sealed class JcBridgePillarFade : IDisposable
    {
        MeshFilter source;
        Mesh original, remainder, pillar;
        GameObject visual;
        Material[] materials;
        Color[] colors;
        float opacity = 1;
        public bool IsValid => source != null && visual != null;

        sealed class Islands
        {
            public int[] roots;
            public Dictionary<int, Bounds> bounds = new Dictionary<int, Bounds>();
        }

        static Islands FindIslands(MeshFilter filter)
        {
            var vertices = filter.sharedMesh.vertices;
            var roots = new int[vertices.Length];
            for (int i = 0; i < roots.Length; i++) roots[i] = i;
            int Root(int i) { while (roots[i] != i) { roots[i] = roots[roots[i]]; i = roots[i]; } return i; }
            void Join(int a, int b) { roots[Root(a)] = Root(b); }
            var weld = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var key = Vector3Int.RoundToInt(vertices[i] * 1000000);
                if (weld.TryGetValue(key, out int other)) Join(i, other); else weld[key] = i;
            }
            var indices = filter.sharedMesh.triangles;
            for (int i = 0; i < indices.Length; i += 3) { Join(indices[i], indices[i + 1]); Join(indices[i], indices[i + 2]); }
            var result = new Islands { roots = roots };
            for (int i = 0; i < vertices.Length; i++)
            {
                int root = Root(i); roots[i] = root;
                var point = filter.transform.TransformPoint(vertices[i]);
                if (!result.bounds.TryGetValue(root, out var bounds)) bounds = new Bounds(point, Vector3.zero);
                bounds.Encapsulate(point); result.bounds[root] = bounds;
            }
            return result;
        }

        public static JcBridgePillarFade TryCreate(Scene scene, Vector3 entrance, float cellSize)
        {
            MeshFilter best = null; Islands chosen = null; Bounds body = default;
            float distance = float.MaxValue;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                // 이 교량 모델에만 적용하며 주변 건물이나 다른 인디케이터는 탐색하지 않습니다.
                if (!filter.name.StartsWith("cableStayedBridge", StringComparison.Ordinal) || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled) continue;
                bool supported = renderer.sharedMaterials.Length > 0;
                foreach (var mat in renderer.sharedMaterials) supported &= mat != null && mat.shader.name == "Universal Render Pipeline/Lit";
                if (!supported) continue;
                var islands = FindIslands(filter);
                foreach (var bounds in islands.bounds.Values)
                {
                    // 화면 위가 +Z인 탐사씬: 출발점 남쪽의 넓고 높은 입구 기둥만 선택합니다.
                    if (bounds.center.z >= entrance.z || bounds.size.y < cellSize * 1.5f
                        || bounds.size.x < cellSize * .3f || bounds.size.x > cellSize * 1.5f
                        || bounds.size.z > cellSize * 1.5f) continue;
                    float d = new Vector2(bounds.center.x - entrance.x, bounds.center.z - entrance.z).sqrMagnitude;
                    if (d > cellSize * cellSize * 9 || d >= distance) continue;
                    distance = d; best = filter; chosen = islands; body = bounds;
                }
            }
            if (best == null) return null;
            var selected = new HashSet<int>();
            foreach (var pair in chosen.bounds)
            {
                var b = pair.Value;
                // 기둥 몸체와 그 위 장식만 포함합니다. 길게 이어진 케이블·난간은 제외합니다.
                if (b.min.x >= body.min.x - .05f * cellSize && b.max.x <= body.max.x + .05f * cellSize
                    && b.min.z >= body.min.z - .05f * cellSize && b.max.z <= body.max.z + .05f * cellSize
                    && b.min.y >= body.min.y - .05f * cellSize) selected.Add(pair.Key);
            }
            var state = new JcBridgePillarFade { source = best, original = best.sharedMesh };
            state.remainder = UnityEngine.Object.Instantiate(state.original);
            state.pillar = UnityEngine.Object.Instantiate(state.original);
            state.remainder.name = "교량 · 입구 기둥 제외"; state.pillar.name = "교량 · 입구 남쪽 기둥";
            state.remainder.hideFlags = state.pillar.hideFlags = HideFlags.HideAndDontSave;
            for (int sub = 0; sub < state.original.subMeshCount; sub++)
            {
                var indices = state.original.GetTriangles(sub);
                var keep = new List<int>(); var fade = new List<int>();
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var target = selected.Contains(chosen.roots[indices[i]]) ? fade : keep;
                    target.Add(indices[i]); target.Add(indices[i + 1]); target.Add(indices[i + 2]);
                }
                state.remainder.SetTriangles(keep, sub); state.pillar.SetTriangles(fade, sub);
            }
            state.visual = new GameObject("입구 남쪽 기둥 · 반투명") { hideFlags = HideFlags.HideAndDontSave, layer = best.gameObject.layer };
            state.visual.transform.SetParent(best.transform, false);
            state.visual.AddComponent<MeshFilter>().sharedMesh = state.pillar;
            var originalRenderer = best.GetComponent<MeshRenderer>();
            var fadeRenderer = state.visual.AddComponent<MeshRenderer>();
            fadeRenderer.shadowCastingMode = originalRenderer.shadowCastingMode;
            fadeRenderer.receiveShadows = originalRenderer.receiveShadows;
            fadeRenderer.lightProbeUsage = originalRenderer.lightProbeUsage;
            fadeRenderer.reflectionProbeUsage = originalRenderer.reflectionProbeUsage;
            var originals = originalRenderer.sharedMaterials;
            state.materials = new Material[originals.Length]; state.colors = new Color[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                var m = new Material(originals[i]) { name = "기둥 반투명 · " + originals[i].name, hideFlags = HideFlags.HideAndDontSave, renderQueue = 3100 };
                state.colors[i] = m.GetColor("_BaseColor"); state.materials[i] = m;
                m.SetOverrideTag("RenderType", "Transparent"); m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0); m.SetFloat("_AlphaClip", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetShaderPassEnabled("DepthOnly", false); m.SetShaderPassEnabled("DepthNormals", false);
                // 주변 조명은 유지하고 인디케이터 뒤의 깊이만 비웁니다.
                m.SetShaderPassEnabled("ShadowCaster", true);
            }
            fadeRenderer.sharedMaterials = state.materials;
            state.visual.SetActive(false);
            return state;
        }

        public void Tick(float target, float seconds, float deltaTime)
        {
            if (!IsValid) return;
            opacity = seconds <= 0 ? Mathf.Clamp01(target) : Mathf.MoveTowards(opacity, Mathf.Clamp01(target), Mathf.Max(0, deltaTime) / seconds);
            bool faded = opacity < .9999f;
            source.sharedMesh = faded ? remainder : original;
            visual.SetActive(faded);
            for (int i = 0; i < materials.Length; i++) { var c = colors[i]; c.a *= opacity; materials[i].SetColor("_BaseColor", c); }
        }

        public void Dispose()
        {
            if (source != null && source.sharedMesh == remainder) source.sharedMesh = original;
            if (visual != null) { visual.SetActive(false); Destroy(visual); }
            Destroy(remainder); Destroy(pillar);
            if (materials != null) foreach (var material in materials) Destroy(material);
        }
        static void Destroy(UnityEngine.Object obj) { if (obj == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj); }
    }
}
