using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KJ_Work.Scripts
{
    public class KJ_OutlineRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class OutlineSettings
        {
            [Header("Stencil Pre-pass")]
            [Tooltip("Material that writes target objects into the stencil buffer.")]
            public Material stencilWriteMaterial;

            [Header("Outline Pass")]
            [Tooltip("Material that renders the inverted-hull outline.")]
            public Material outlineMaterial;

            [Header("Common Settings")]
            [Tooltip("Layers that should receive the outline pass.")]
            public LayerMask layerMask = -1;

            [Header("Root Object Separation")]
            [Tooltip("Use one stencil reference per root object. Meshes under the same root are merged, but different roots can draw outlines against each other.")]
            public bool usePerRootStencil = true;
        }

        public OutlineSettings settings = new OutlineSettings();

        private OutlineRenderPass outlinePass;

        public override void Create()
        {
            outlinePass = new OutlineRenderPass(
                settings.stencilWriteMaterial,
                settings.outlineMaterial,
                settings.layerMask,
                settings.usePerRootStencil);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.stencilWriteMaterial == null || settings.outlineMaterial == null)
                return;

            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            renderer.EnqueuePass(outlinePass);
        }

        protected override void Dispose(bool disposing)
        {
            outlinePass?.Dispose();
        }

        private class OutlineRenderPass : ScriptableRenderPass
        {
            private static readonly ShaderTagId[] ShaderTagIds =
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("LightweightForward"),
                new ShaderTagId("SRPDefaultUnlit")
            };

            private readonly Material stencilMaterial;
            private readonly Material outlineMaterial;
            private readonly LayerMask layerMask;
            private readonly bool usePerRootStencil;
            private FilteringSettings filteringSettings;
            private readonly ProfilingSampler profilingSampler;
            private readonly List<OutlineGroup> outlineGroups = new List<OutlineGroup>();
            private readonly Dictionary<Transform, int> groupLookup = new Dictionary<Transform, int>();
            private readonly Dictionary<int, Material> stencilMaterialByRef = new Dictionary<int, Material>();
            private readonly Dictionary<int, Material> outlineMaterialByRef = new Dictionary<int, Material>();

            public OutlineRenderPass(Material stencilMaterial, Material outlineMaterial, LayerMask layerMask, bool usePerRootStencil)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
                this.stencilMaterial = stencilMaterial;
                this.outlineMaterial = outlineMaterial;
                this.layerMask = layerMask;
                this.usePerRootStencil = usePerRootStencil;
                filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
                profilingSampler = new ProfilingSampler("KJ Stencil & Outline Pass");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (stencilMaterial == null || outlineMaterial == null)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, profilingSampler))
                {
                    if (usePerRootStencil)
                    {
                        DrawPerRootStencilOutlines(cmd, renderingData.cameraData.camera);
                    }
                    else
                    {
                        DrawSingleStencilOutline(context, ref renderingData);
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                DestroyCachedMaterials(stencilMaterialByRef);
                DestroyCachedMaterials(outlineMaterialByRef);
            }

            private void DrawSingleStencilOutline(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                SortingCriteria sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                DrawingSettings drawSettings = CreateDrawingSettings(ShaderTagIds[0], ref renderingData, sortFlags);
                for (int i = 1; i < ShaderTagIds.Length; i++)
                    drawSettings.SetShaderPassName(i, ShaderTagIds[i]);

                drawSettings.overrideMaterial = stencilMaterial;
                drawSettings.overrideMaterialPassIndex = 0;
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);

                drawSettings.overrideMaterial = outlineMaterial;
                drawSettings.overrideMaterialPassIndex = 0;
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);
            }

            private void DrawPerRootStencilOutlines(CommandBuffer cmd, Camera camera)
            {
                if (camera == null)
                    return;

                CollectOutlineGroups(camera);

                for (int i = 0; i < outlineGroups.Count; i++)
                {
                    int stencilRef = (i % 255) + 1;
                    Material stencilRefMaterial = GetMaterialForStencilRef(stencilMaterialByRef, stencilMaterial, stencilRef);
                    Material outlineRefMaterial = GetMaterialForStencilRef(outlineMaterialByRef, outlineMaterial, stencilRef);

                    DrawGroup(cmd, outlineGroups[i], stencilRefMaterial);
                    DrawGroup(cmd, outlineGroups[i], outlineRefMaterial);
                }
            }

            private void CollectOutlineGroups(Camera camera)
            {
                outlineGroups.Clear();
                groupLookup.Clear();

                Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
                Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

                foreach (Renderer targetRenderer in renderers)
                {
                    if (!ShouldDrawRenderer(targetRenderer, frustumPlanes))
                        continue;

                    Transform root = FindOutlineRoot(targetRenderer.transform);
                    if (!groupLookup.TryGetValue(root, out int groupIndex))
                    {
                        groupIndex = outlineGroups.Count;
                        groupLookup.Add(root, groupIndex);
                        outlineGroups.Add(new OutlineGroup(root));
                    }

                    outlineGroups[groupIndex].Add(targetRenderer);
                }

                outlineGroups.Sort((a, b) =>
                {
                    float aDepth = Vector3.Dot(camera.transform.forward, a.Bounds.center - camera.transform.position);
                    float bDepth = Vector3.Dot(camera.transform.forward, b.Bounds.center - camera.transform.position);
                    return bDepth.CompareTo(aDepth);
                });
            }

            private bool ShouldDrawRenderer(Renderer targetRenderer, Plane[] frustumPlanes)
            {
                return targetRenderer != null &&
                       targetRenderer.enabled &&
                       targetRenderer.gameObject.activeInHierarchy &&
                       IsLayerIncluded(targetRenderer.gameObject.layer) &&
                       GeometryUtility.TestPlanesAABB(frustumPlanes, targetRenderer.bounds);
            }

            private Transform FindOutlineRoot(Transform target)
            {
                Transform root = target;
                while (root.parent != null && IsLayerIncluded(root.parent.gameObject.layer))
                    root = root.parent;

                return root;
            }

            private bool IsLayerIncluded(int layer)
            {
                return (layerMask.value & (1 << layer)) != 0;
            }

            private static void DrawGroup(CommandBuffer cmd, OutlineGroup group, Material material)
            {
                foreach (Renderer targetRenderer in group.Renderers)
                {
                    int subMeshCount = GetSubMeshCount(targetRenderer);
                    for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                        cmd.DrawRenderer(targetRenderer, material, subMeshIndex, 0);
                }
            }

            private static int GetSubMeshCount(Renderer targetRenderer)
            {
                Mesh mesh = null;

                if (targetRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    mesh = skinnedMeshRenderer.sharedMesh;
                }
                else
                {
                    MeshFilter meshFilter = targetRenderer.GetComponent<MeshFilter>();
                    if (meshFilter != null)
                        mesh = meshFilter.sharedMesh;
                }

                if (mesh != null)
                    return Mathf.Max(1, mesh.subMeshCount);

                return Mathf.Max(1, targetRenderer.sharedMaterials.Length);
            }

            private static Material GetMaterialForStencilRef(Dictionary<int, Material> cache, Material sourceMaterial, int stencilRef)
            {
                if (!cache.TryGetValue(stencilRef, out Material material) || material == null)
                {
                    material = new Material(sourceMaterial)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        name = $"{sourceMaterial.name}_StencilRef{stencilRef}"
                    };
                    cache[stencilRef] = material;
                }
                else
                {
                    material.CopyPropertiesFromMaterial(sourceMaterial);
                    material.renderQueue = sourceMaterial.renderQueue;
                }

                material.SetFloat("_StencilRef", stencilRef);
                return material;
            }

            private static void DestroyCachedMaterials(Dictionary<int, Material> cache)
            {
                foreach (Material material in cache.Values)
                {
                    if (material == null)
                        continue;

                    if (Application.isPlaying)
                        Destroy(material);
                    else
                        DestroyImmediate(material);
                }

                cache.Clear();
            }

            private class OutlineGroup
            {
                public readonly List<Renderer> Renderers = new List<Renderer>();
                public Bounds Bounds { get; private set; }
                private bool hasBounds;

                public OutlineGroup(Transform root)
                {
                    Bounds = new Bounds(root.position, Vector3.zero);
                }

                public void Add(Renderer targetRenderer)
                {
                    Renderers.Add(targetRenderer);

                    if (hasBounds)
                    {
                        Bounds.Encapsulate(targetRenderer.bounds);
                    }
                    else
                    {
                        Bounds = targetRenderer.bounds;
                        hasBounds = true;
                    }
                }
            }
        }
    }
}
