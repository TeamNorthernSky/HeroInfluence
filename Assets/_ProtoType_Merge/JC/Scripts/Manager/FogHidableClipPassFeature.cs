using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// FogHidable Layer의 Renderer들을, FogStencilPrepass가 기록한 stencil을 기준으로
/// 완전 Fogged 셀에서 clip하여 렌더.
/// 2차 culling으로 FogHidable Layer를 포함시켜 (main culling에서 제외되어도) 수집 후 draw.
/// 기존 유닛 머티리얼·셰이더는 수정 불필요 — RenderStateBlock으로 stencil 상태만 덮어씀.
/// </summary>
public class FogHidableClipPassFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public Settings settings = new Settings();

    private class Pass : ScriptableRenderPass
    {
        private static readonly List<ShaderTagId> ShaderTags = new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("LightweightForward")
        };

        private readonly ProfilingSampler profilingSampler = new ProfilingSampler("FogHidableClipPass");

        public Pass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CameraType type = renderingData.cameraData.cameraType;
            if (type != CameraType.Game && type != CameraType.SceneView) return;

            Camera camera = renderingData.cameraData.camera;
            if (camera == null || camera.pixelWidth <= 0 || camera.pixelHeight <= 0) return;

            int fogLayer = FogRenderLayer.HidableLayerIndex;
            if (fogLayer < 0) return; // Layer 미등록 시 스킵

            if (!camera.TryGetCullingParameters(out ScriptableCullingParameters cullParams)) return;

            // FogHidable Layer만 포함한 2차 culling (main cullingMask에서 제외돼도 수집)
            cullParams.cullingMask = (uint)(1 << fogLayer);
            CullingResults fogCullResults = context.Cull(ref cullParams);

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                DrawingSettings drawingSettings = CreateDrawingSettings(
                    ShaderTags, ref renderingData, SortingCriteria.CommonOpaque);
                drawingSettings.perObjectData = PerObjectData.LightProbe
                                              | PerObjectData.ReflectionProbes
                                              | PerObjectData.Lightmaps
                                              | PerObjectData.ShadowMask;

                FilteringSettings filteringSettings = new FilteringSettings(
                    RenderQueueRange.opaque, 1 << fogLayer);

                // stencil != 1 인 픽셀만 통과 (완전 Fogged 영역 유닛 clip)
                StencilState stencilState = new StencilState(
                    enabled: true,
                    readMask: 0xFF,
                    writeMask: 0x00,
                    compareFunction: CompareFunction.NotEqual,
                    passOperation: StencilOp.Keep,
                    failOperation: StencilOp.Keep,
                    zFailOperation: StencilOp.Keep);

                RenderStateBlock stateBlock = new RenderStateBlock(RenderStateMask.Stencil)
                {
                    stencilState = stencilState,
                    stencilReference = 1
                };

                context.DrawRenderers(
                    fogCullResults,
                    ref drawingSettings,
                    ref filteringSettings,
                    ref stateBlock);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    private Pass pass;

    public override void Create()
    {
        pass = new Pass(settings.passEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pass);
    }
}
