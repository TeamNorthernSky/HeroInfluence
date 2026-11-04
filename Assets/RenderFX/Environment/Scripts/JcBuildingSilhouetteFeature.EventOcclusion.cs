using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed partial class JcBuildingSilhouetteFeature
{
    // JC | 2026-11-04 | 건물 뒤에 가려진 이벤트 외형의 깊이와 투명도를 별도로 처리해 전면의 원본 외형을 보존합니다.
    private EventOcclusionPass eventOcclusion;
    // JC | 2026-11-04 | 원본 색상·포즈를 기록하고 전용 깊이에서 자가 겹침을 제거합니다. 카메라 장면 깊이는 수정하지 않습니다.
    private sealed class EventOcclusionPass : ScriptableRenderPass
    {
        private JcEventOcclusionController controller;
        private Camera camera;
        private RTHandle color, depth, surface;
        private readonly Dictionary<(Renderer renderer, int slot), Material> depthMaterials = new Dictionary<(Renderer, int), Material>();
        private readonly List<(Renderer renderer, int slot)> stale = new List<(Renderer, int)>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Renderer> visible = new List<Renderer>();
        private readonly HashSet<Renderer> selectedRenderers = new HashSet<Renderer>();
        private readonly Plane[] frustum = new Plane[6];
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock slotBlock = new MaterialPropertyBlock();
        public EventOcclusionPass()
        {
            renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 3);
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }
        public void Setup(JcEventOcclusionController c, Camera cam)
        {
            controller = c; camera = cam;
            stale.Clear();
            foreach (var pair in depthMaterials) if (!pair.Key.renderer || !Contains(c.Targets, pair.Key.renderer)) stale.Add(pair.Key);
            foreach (var key in stale) { JcEventOcclusionController.DestroyOwned(depthMaterials[key]); depthMaterials.Remove(key); }
        }
        private static bool Contains(IReadOnlyList<Renderer> list, Renderer renderer)
        { for (int i = 0; i < list.Count; i++) if (list[i] == renderer) return true; return false; }
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            var desc = data.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1; desc.bindMS = false; desc.depthBufferBits = 0;
            desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            RenderingUtils.ReAllocateIfNeeded(ref color, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_JcEventGhostColor");
            desc.graphicsFormat = GraphicsFormat.None; desc.depthStencilFormat = GraphicsFormat.D32_SFloat;
            RenderingUtils.ReAllocateIfNeeded(ref depth, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_JcEventGhostDepth");
            desc.depthStencilFormat = GraphicsFormat.None; desc.depthBufferBits = 0; desc.graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;
            RenderingUtils.ReAllocateIfNeeded(ref surface, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_JcEventSurfaceDepth");
            ConfigureTarget(color, depth); ConfigureClear(ClearFlag.All, Color.clear);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            var cmd = CommandBufferPool.Get("JC NPC event occlusion capture");
            try
            {
                visible.Clear(); selectedRenderers.Clear(); GeometryUtility.CalculateFrustumPlanes(camera, frustum);
                foreach (var r in controller.Targets)
                    if (r && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy &&
                        (camera.cullingMask & (1 << r.gameObject.layer)) != 0 && GeometryUtility.TestPlanesAABB(frustum, r.bounds)) { visible.Add(r); selectedRenderers.Add(r); }
                // JC | 2026-11-04 | 컷아웃·스키닝과 원본 속성 블록의 알파를 반영한 깊이를 먼저 기록합니다.
                foreach (var r in visible)
                {
                    r.GetSharedMaterials(materials); block.Clear(); r.GetPropertyBlock(block);
                    for (int slot = 0; slot < materials.Count; slot++)
                    {
                        var original = materials[slot]; if (!original) continue;
                        var key = (r, slot);
                        if (!depthMaterials.TryGetValue(key, out var copy)) depthMaterials.Add(key, copy = new Material(controller.CompositeShader) { hideFlags = HideFlags.HideAndDontSave });
                        slotBlock.Clear(); r.GetPropertyBlock(slotBlock, slot);
                        string map = original.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                        string tint = original.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                        var texture = slotBlock.GetTexture(map) ?? block.GetTexture(map) ?? (original.HasProperty(map) ? original.GetTexture(map) : null);
                        copy.SetTexture("_GhostMap", texture ? texture : Texture2D.whiteTexture);
                        copy.SetTextureScale("_GhostMap", original.HasProperty(map) ? original.GetTextureScale(map) : Vector2.one);
                        copy.SetTextureOffset("_GhostMap", original.HasProperty(map) ? original.GetTextureOffset(map) : Vector2.zero);
                        Color colorTint = slotBlock.HasColor(tint) ? slotBlock.GetColor(tint) : block.HasColor(tint) ? block.GetColor(tint) : original.HasProperty(tint) ? original.GetColor(tint) : Color.white;
                        copy.SetColor("_GhostTint", colorTint);
                        copy.SetFloat("_GhostCull", original.HasProperty("_Cull") ? original.GetFloat("_Cull") : 2);
                        copy.SetFloat("_GhostCutoff", original.IsKeywordEnabled("_ALPHATEST_ON") && original.HasProperty("_Cutoff") ? original.GetFloat("_Cutoff") : .001f);
                        cmd.DrawRenderer(r, copy, slot, copy.FindPass("EventObjectDepth"));
                    }
                }
                context.ExecuteCommandBuffer(cmd); cmd.Clear();
                DrawOriginalColors(context, ref data);
                cmd.SetRenderTarget(surface.nameID);
                cmd.ClearRenderTarget(false, true, new Color(65504, 65504, 65504, 65504));
                var view = camera.worldToCameraMatrix;
                cmd.SetGlobalMatrix("_JcTriggerDepthVP", GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * view);
                cmd.SetGlobalMatrix("_JcTriggerDepthView", view);
                controller.surfaces.Draw(cmd, camera, controller.SurfaceMaterial, block);
                cmd.SetGlobalTexture("_JcEventGhostColor", color.nameID);
                cmd.SetGlobalTexture("_JcEventGhostDepth", depth.nameID);
                cmd.SetGlobalTexture("_JcEventSurfaceDepth", surface.nameID);
                cmd.SetGlobalFloat("_JcEventGhostOpacity", controller.Opacity);
                cmd.SetGlobalFloat("_JcEventGhostActive", visible.Count > 0 ? 1 : 0);
                context.ExecuteCommandBuffer(cmd);
            }
            finally { CommandBufferPool.Release(cmd); }
        }
        private readonly List<KeyValuePair<Renderer, uint>> layers = new List<KeyValuePair<Renderer, uint>>();
        private static readonly List<ShaderTagId> forwardTags = new List<ShaderTagId> { new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit") };
        private void DrawOriginalColors(ScriptableRenderContext context, ref RenderingData data)
        {
            // JC | 2026-11-04 | DrawRenderer는 개별 조명/라이트프로브 데이터를 준비하지 않습니다.
            // 별도 컬링과 URP의 원래 DrawingSettings를 사용해 원본 포워드 색·야간 조명을 그대로 기록합니다.
            // 선택용 비트는 이 동기 실행 안에서만 사용하고 기존 값(해당 비트를 쓰던 대상 포함)을 반드시 복원합니다.
            const uint captureLayer = 1u << 31;
            layers.Clear();
            try
            {
                foreach (var r in controller.SceneRenderers)
                {
                    if (!r) continue;
                    uint original = r.renderingLayerMask;
                    uint selected = selectedRenderers.Contains(r) ? original | captureLayer : original & ~captureLayer;
                    if (selected == original) continue;
                    layers.Add(new KeyValuePair<Renderer, uint>(r, original)); r.renderingLayerMask = selected;
                }
                if (!camera.TryGetCullingParameters(out var parameters)) return;
                parameters.cullingOptions &= ~CullingOptions.OcclusionCull;
                var culling = context.Cull(ref parameters);
                var drawing = CreateDrawingSettings(forwardTags, ref data, SortingCriteria.CommonOpaque);
                var filtering = new FilteringSettings(RenderQueueRange.all, camera.cullingMask, captureLayer);
                context.DrawRenderers(culling, ref drawing, ref filtering);
            }
            finally { foreach (var pair in layers) if (pair.Key) pair.Key.renderingLayerMask = pair.Value; layers.Clear(); }
        }
        public void Dispose()
        {
            color?.Release(); depth?.Release(); surface?.Release(); color = depth = surface = null;
            foreach (var m in depthMaterials.Values) JcEventOcclusionController.DestroyOwned(m);
            depthMaterials.Clear();
        }
    }
}
