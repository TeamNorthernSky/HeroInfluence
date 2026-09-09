using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 가려진 건물 메시를 독립 마스크에 합친 뒤 한 번만 단색 합성한다.
/// 기존 외곽선/FOW의 스텐실과 카메라 깊이는 수정하지 않는다.
/// DH의 transparentOverrideMaterial과 같은 머티리얼을 지정한다.
/// </summary>
public sealed class JcBuildingSilhouetteFeature : ScriptableRendererFeature
{
    [Tooltip("DH PartyOcclusionFadeController의 대체 머티리얼과 같은 자산을 지정하세요.")]
    public Material silhouetteMaterial;

    private MaskPass maskPass;
    private CompositePass compositePass;
    private ShadowRemovalPass shadowRemovalPass;

    public override void Create()
    {
        maskPass?.Dispose();
        shadowRemovalPass?.Dispose();
        maskPass = new MaskPass();
        compositePass = new CompositePass(maskPass);
        shadowRemovalPass = new ShadowRemovalPass(maskPass);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        CameraType type = renderingData.cameraData.cameraType;
        if ((type != CameraType.Game && type != CameraType.SceneView)
            || renderingData.cameraData.renderType == CameraRenderType.Overlay
            || silhouetteMaterial == null)
            return;

        int compositeIndex = silhouetteMaterial.FindPass("SilhouetteComposite");
        if (compositeIndex < 0) return;
        bool hasTuning = JcBuildingSilhouetteController.TryGetSettings(renderingData.cameraData.camera, out var tuning);
        maskPass.Setup(hasTuning, tuning, renderingData.cameraData.camera);
        compositePass.Setup(silhouetteMaterial, compositeIndex);
        shadowRemovalPass.Setup(silhouetteMaterial);
        renderer.EnqueuePass(maskPass);
        var scene = type == CameraType.SceneView ? UnityEngine.SceneManagement.SceneManager.GetActiveScene()
            : renderingData.cameraData.camera.gameObject.scene;
        if (!PartyOcclusionFadeController.TryHasFadedObjects(scene, out bool hasFaded) || hasFaded)
            renderer.EnqueuePass(shadowRemovalPass);
        renderer.EnqueuePass(compositePass);
    }

    protected override void Dispose(bool disposing)
    {
        maskPass?.Dispose();
        shadowRemovalPass?.Dispose();
    }

    private sealed class MaskPass : ScriptableRenderPass
    {
        private static readonly ShaderTagId MaskTag = new ShaderTagId("JcBuildingSilhouetteMask");
        private static readonly int TuningId = Shader.PropertyToID("_JcBuildingTuning");
        private static readonly int FillId = Shader.PropertyToID("_JcBuildingFill");
        private static readonly int OutlineId = Shader.PropertyToID("_JcBuildingOutline");
        private static readonly int PixelSizeId = Shader.PropertyToID("_JcBuildingPixelSize");
        private static readonly int OutlineSettingsId = Shader.PropertyToID("_JcBuildingOutlineSettings");
        private Vector4 tuningVector;
        private Vector4 outlineSettings;
        private Color fill;
        private Color outline;
        private Vector4 pixelSize;
        public RTHandle Mask { get; private set; }

        public void Setup(bool enabled, JcBuildingSilhouetteSettings tuning, Camera camera)
        {
            tuningVector = new Vector4(enabled ? 1f : 0f, tuning.opacity, tuning.depthBias, tuning.outlineWidth);
            fill = QualitySettings.activeColorSpace == ColorSpace.Linear ? tuning.fillColor.linear : tuning.fillColor;
            outline = QualitySettings.activeColorSpace == ColorSpace.Linear ? tuning.outlineColor.linear : tuning.outlineColor;
            outlineSettings = new Vector4(tuning.outlineOpacity, tuning.outlineWorldWidth, (float)tuning.outlineWidthMode, 0f);
            // 최종 화면 픽셀 기준: URP Render Scale이 달라도 두께가 유지된다.
            pixelSize = new Vector4(1f / Mathf.Max(1, camera.pixelWidth), 1f / Mathf.Max(1, camera.pixelHeight),
                Mathf.Abs(camera.projectionMatrix.m11) * camera.pixelHeight * 0.5f, camera.orthographic ? 1f : 0f);
        }

        public void WriteTuning(CommandBuffer cmd)
        {
            cmd.SetGlobalVector(TuningId, tuningVector);
            cmd.SetGlobalColor(FillId, fill);
            cmd.SetGlobalColor(OutlineId, outline);
            cmd.SetGlobalVector(PixelSizeId, pixelSize);
            cmd.SetGlobalVector(OutlineSettingsId, outlineSettings);
        }

        public void ClearTuning(CommandBuffer cmd) => cmd.SetGlobalVector(TuningId, Vector4.zero);

        public MaskPass()
        {
            // 그림자 제외 색을 기존 외곽선보다 먼저 적용한다. 깊이 입력은 URP가 준비한다.
            renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingOpaques + 1);
            ConfigureInput(ScriptableRenderPassInput.Depth);
            profilingSampler = new ProfilingSampler("JC Building Silhouette Mask");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.bindMS = false;
            // R: 모양, G: DH 폴백 알파, B: 가장 가까운 건물의 역깊이.
            // 알파가 0이어도 모양이 남으며, Max 블렌딩으로 중복 메시와 깊이를 함께 합친다.
            desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            RTHandle target = Mask;
            RenderingUtils.ReAllocateIfNeeded(ref target, desc, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_JcBuildingSilhouetteMask");
            Mask = target;
            ConfigureTarget(Mask);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("JC Building Silhouette Tuning");
            WriteTuning(cmd);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            DrawingSettings draw = CreateDrawingSettings(MaskTag, ref renderingData, SortingCriteria.None);
            draw.perObjectData = PerObjectData.None;
            FilteringSettings filter = new FilteringSettings(RenderQueueRange.transparent);
            context.DrawRenderers(renderingData.cullResults, ref draw, ref filter);
        }

        public void Dispose()
        {
            Mask?.Release();
            Mask = null;
        }
    }

    /// <summary>원본 재질의 조명/텍스처는 유지하고 실시간 그림자만 제외한 색을 별도 타깃에 그린다.</summary>
    private sealed class ShadowRemovalPass : ScriptableRenderPass
    {
        private static readonly string[] ShadowKeywords =
        {
            "_MAIN_LIGHT_SHADOWS", "_MAIN_LIGHT_SHADOWS_CASCADE",
            "_MAIN_LIGHT_SHADOWS_SCREEN", "_ADDITIONAL_LIGHT_SHADOWS"
        };
        private readonly bool[] previousKeywords = new bool[ShadowKeywords.Length];
        private readonly MaskPass source;
        private RTHandle color;
        private RTHandle depth;
        private Material material;

        public ShadowRemovalPass(MaskPass source)
        {
            this.source = source;
            renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingOpaques + 2);
            profilingSampler = new ProfilingSampler("JC Building Shadow Removal");
        }

        public void Setup(Material value) => material = value;

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.bindMS = false;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref color, desc, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: "_JcShadowlessColor");
            desc.graphicsFormat = GraphicsFormat.None;
            desc.depthStencilFormat = GraphicsFormat.D32_SFloat;
            RenderingUtils.ReAllocateIfNeeded(ref depth, desc, FilterMode.Point,
                TextureWrapMode.Clamp, name: "_JcShadowlessDepth");
            ConfigureTarget(color, depth);
            ConfigureClear(ClearFlag.All, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            int pass = material.FindPass("ShadowlessComposite");
            if (pass < 0 || source.Mask == null) return;
            var cmd = CommandBufferPool.Get("JC Building Shadow Removal");
            try
            {
                for (int i = 0; i < ShadowKeywords.Length; i++)
                {
                    previousKeywords[i] = Shader.IsKeywordEnabled(ShadowKeywords[i]);
                    cmd.DisableShaderKeyword(ShadowKeywords[i]);
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                var drawing = CreateDrawingSettings(new ShaderTagId("UniversalForward"), ref renderingData,
                    renderingData.cameraData.defaultOpaqueSortFlags);
                drawing.SetShaderPassName(1, new ShaderTagId("UniversalForwardOnly"));
                drawing.SetShaderPassName(2, new ShaderTagId("SRPDefaultUnlit"));
                var filter = new FilteringSettings(RenderQueueRange.opaque, renderingData.cameraData.camera.cullingMask);
                context.DrawRenderers(renderingData.cullResults, ref drawing, ref filter);
            }
            finally
            {
                for (int i = 0; i < ShadowKeywords.Length; i++)
                    CoreUtils.SetKeyword(cmd, ShadowKeywords[i], previousKeywords[i]);
                cmd.SetGlobalTexture("_JcShadowRemovalMask", source.Mask.nameID);
                Blitter.BlitCameraTexture(cmd, color, renderingData.cameraData.renderer.cameraColorTargetHandle,
                    RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, material, pass);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        public void Dispose() { color?.Release(); depth?.Release(); }
    }

    private sealed class CompositePass : ScriptableRenderPass
    {
        private readonly MaskPass source;
        private Material material;
        private int passIndex;

        public CompositePass(MaskPass source)
        {
            this.source = source;
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            profilingSampler = new ProfilingSampler("JC Building Silhouette Composite");
        }

        public void Setup(Material nextMaterial, int nextPass)
        {
            material = nextMaterial;
            passIndex = nextPass;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (source.Mask == null || material == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("JC Building Silhouette Composite");
            source.WriteTuning(cmd);
            // 원본 카메라 색은 Load로 유지하며 하드웨어 알파 합성한다. 색 복사 RT는 불필요.
            Blitter.BlitCameraTexture(cmd, source.Mask,
                renderingData.cameraData.renderer.cameraColorTargetHandle,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, material, passIndex);
            source.ClearTuning(cmd);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
