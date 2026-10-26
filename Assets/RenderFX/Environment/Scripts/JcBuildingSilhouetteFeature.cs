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

    private static UnityEngine.Object tutorialOwner;
    private static Transform tutorialTarget, cachedTutorialTarget;
    private static Renderer[] tutorialRenderers;
    private static Color tutorialColor;
    public static void SetTutorialHighlight(UnityEngine.Object owner, Transform target, Color color)
    {
        if (owner == null || target == null) return;
        tutorialOwner = owner; tutorialTarget = target; tutorialColor = color;
        if (cachedTutorialTarget != target || tutorialRenderers == null)
        { cachedTutorialTarget = target; tutorialRenderers = target.GetComponentsInChildren<Renderer>(true); }
    }
    public static void ClearTutorialHighlight(UnityEngine.Object owner)
    { if (tutorialOwner == owner) { tutorialOwner = null; tutorialTarget = null; } }
    private static UnityEngine.Object hoverOwner;
    private static Transform hoverTarget, cachedHoverTarget;
    private static Renderer[] hoverRenderers;

    public static void SetHoverHighlight(UnityEngine.Object owner, Transform target)
    {
        if (owner == null || target == null) return;
        hoverOwner = owner; hoverTarget = target;
        if (cachedHoverTarget != target || hoverRenderers == null)
        { cachedHoverTarget = target; hoverRenderers = target.GetComponentsInChildren<Renderer>(true); }
    }

    public static void ClearHoverHighlight(UnityEngine.Object owner)
    { if (hoverOwner == owner) { hoverOwner = null; hoverTarget = null; } }

    private MaskPass tutorialMaskPass;
    private CompositePass tutorialCompositePass;
    private MaskPass maskPass;
    private CompositePass compositePass;
    private ShadowRemovalPass shadowRemovalPass;

    public override void Create()
    {
        compositePass?.Dispose();
        tutorialCompositePass?.Dispose();
        maskPass?.Dispose();
        tutorialMaskPass?.Dispose();
        shadowRemovalPass?.Dispose();
        tutorialMaskPass = new MaskPass("_JcTutorialBuildingMask");
        tutorialMaskPass.renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 2);
        tutorialCompositePass = new CompositePass(tutorialMaskPass);
        tutorialCompositePass.renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 1);
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
        // 튜토리얼 안내가 있으면 일반 호버보다 우선한다. 외곽선 합성은 같은 패스를 사용한다.
        bool tutorial = tutorialOwner != null && tutorialTarget != null && tutorialTarget.gameObject.scene == scene;
        bool hover = hoverOwner != null && hoverTarget != null && hoverTarget.gameObject.scene == scene;
        if (tutorial || hover)
        {
            JC.Indicators.JcBuildingHoverOutlineController.Resolve(scene, out var hoverColor, out var hoverTuning, out var hoverShape);
            var highlight = JcBuildingSilhouetteSettings.Default;
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2 / 1.6f);
            highlight.opacity = 0;
            highlight.outlineColor = tutorial ? Color.Lerp(tutorialColor, Color.white, pulse) : hoverColor;
            highlight.outlineOpacity = 1;
            highlight.outlineWidthMode = JcBuildingOutlineWidthMode.ScreenPixels;
            highlight.outlineWidth = tutorial ? Mathf.Lerp(9, 15, pulse) : 9f;
            tutorialMaskPass.Setup(true, highlight, renderingData.cameraData.camera);
            tutorialMaskPass.explicitRenderers = tutorial ? tutorialRenderers : hoverRenderers;
            if (!tutorial) tutorialMaskPass.SetHoverOrbit(renderingData.cameraData.camera, hoverRenderers, hoverTuning, hoverShape);
            JC.Indicators.JcBuildingHoverOutlineController.ResolveAntialiasing(scene, out float smoothing);
            tutorialMaskPass.SetAntialiasing(smoothing);
            tutorialMaskPass.overrideMaterial = silhouetteMaterial;
            tutorialCompositePass.Setup(silhouetteMaterial, compositeIndex);
            renderer.EnqueuePass(tutorialMaskPass);
            renderer.EnqueuePass(tutorialCompositePass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        compositePass?.Dispose();
        tutorialCompositePass?.Dispose();
        maskPass?.Dispose();
        tutorialMaskPass?.Dispose();
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
        private static readonly int OrbitBoundsId = Shader.PropertyToID("_JcBuildingOrbitBounds");
        private static readonly int OrbitId = Shader.PropertyToID("_JcBuildingOrbit");
        private static readonly int OrbitIntensityId = Shader.PropertyToID("_JcBuildingOrbitIntensity");
        private Vector4 orbitBounds, orbit, orbitIntensity;
        private Vector4 tuningVector;
        private Vector4 outlineSettings;
        private Color fill;
        private Color outline;
        private Vector4 pixelSize;
        public RTHandle Mask { get; private set; }
        public Renderer[] explicitRenderers;
        public Material overrideMaterial;
        private readonly string maskName;
        private static readonly int EdgeSmoothingId = Shader.PropertyToID("_JcBuildingEdgeSmoothing");
        private float edgeSmoothing;

        public float EdgeSmoothing => edgeSmoothing;

        public void SetAntialiasing(float smoothing)
        {
            edgeSmoothing = smoothing;
        }

        public void Setup(bool enabled, JcBuildingSilhouetteSettings tuning, Camera camera)
        {
            orbit = Vector4.zero;
            edgeSmoothing = 0;
            tuningVector = new Vector4(enabled ? 1f : 0f, tuning.opacity, tuning.depthBias, tuning.outlineWidth);
            fill = QualitySettings.activeColorSpace == ColorSpace.Linear ? tuning.fillColor.linear : tuning.fillColor;
            outline = QualitySettings.activeColorSpace == ColorSpace.Linear ? tuning.outlineColor.linear : tuning.outlineColor;
            outlineSettings = new Vector4(tuning.outlineOpacity, tuning.outlineWorldWidth, (float)tuning.outlineWidthMode, 0f);
            // 최종 화면 픽셀 기준: URP Render Scale이 달라도 두께가 유지된다.
            pixelSize = new Vector4(1f / Mathf.Max(1, camera.pixelWidth), 1f / Mathf.Max(1, camera.pixelHeight),
                Mathf.Abs(camera.projectionMatrix.m11) * camera.pixelHeight * 0.5f, camera.orthographic ? 1f : 0f);
        }

        public void SetHoverOrbit(Camera camera, Renderer[] renderers, Vector3 tuning, Vector3 shape)
        {
            if (renderers == null) return;
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                Bounds b = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 uv = camera.WorldToViewportPoint(corner);
                    if (uv.z <= camera.nearClipPlane) continue;
                    min = Vector2.Min(min, new Vector2(uv.x, uv.y));
                    max = Vector2.Max(max, new Vector2(uv.x, uv.y));
                }
            }
            if (float.IsInfinity(min.x)) return;
            Vector2 size = max - min;
            size.x = Mathf.Max(size.x, .0001f); size.y = Mathf.Max(size.y, .0001f);
            Vector2 center = (min + max) * .5f;
            orbitBounds = new Vector4(center.x, center.y, 2f / size.x, 2f / size.y);
            orbit = new Vector4(1f, Mathf.Repeat(Time.unscaledTime * tuning.y, 1f),
                size.x * camera.pixelWidth / (size.y * camera.pixelHeight), shape.z);
            orbitIntensity = new Vector4(tuning.x, tuning.z, shape.x, shape.y);
        }

        public void WriteTuning(CommandBuffer cmd)
        {
            cmd.SetGlobalFloat("_JcBuildingHighlight", overrideMaterial != null && explicitRenderers != null ? 1f : 0f);
            if (Mask != null) cmd.SetGlobalTexture("_JcBuildingShapeDepth", Mask.nameID);
            cmd.SetGlobalFloat(EdgeSmoothingId, edgeSmoothing);
            cmd.SetGlobalVector(OrbitBoundsId, orbitBounds);
            cmd.SetGlobalVector(OrbitId, orbit);
            cmd.SetGlobalVector(OrbitIntensityId, orbitIntensity);
            cmd.SetGlobalVector(TuningId, tuningVector);
            cmd.SetGlobalColor(FillId, fill);
            cmd.SetGlobalColor(OutlineId, outline);
            cmd.SetGlobalVector(PixelSizeId, pixelSize);
            cmd.SetGlobalVector(OutlineSettingsId, outlineSettings);
        }

        public void ClearTuning(CommandBuffer cmd)
        {
            cmd.SetGlobalFloat("_JcBuildingHighlight", 0);
            cmd.SetGlobalTexture("_JcBuildingShapeDepth", Texture2D.blackTexture);
            cmd.SetGlobalFloat(EdgeSmoothingId, 0);
            cmd.SetGlobalVector(TuningId, Vector4.zero);
            cmd.SetGlobalVector(OrbitId, Vector4.zero);
        }

        public MaskPass(string name = "_JcBuildingSilhouetteMask")
        {
            maskName = name;
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
            // 강조는 가려지지 않은 모양 R/최근접 역깊이 G를 보존한다. 일반 투과 마스크는 기존 RGB 유지.
            bool highlightMask = overrideMaterial != null && explicitRenderers != null;
            desc.graphicsFormat = !highlightMask ? GraphicsFormat.R16G16B16A16_SFloat
                : SystemInfo.IsFormatSupported(GraphicsFormat.R32G32_SFloat, FormatUsage.Render)
                    && SystemInfo.IsFormatSupported(GraphicsFormat.R32G32_SFloat, FormatUsage.Blend)
                    ? GraphicsFormat.R32G32_SFloat : GraphicsFormat.R32G32B32A32_SFloat;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            RTHandle target = Mask;
            RenderingUtils.ReAllocateIfNeeded(ref target, desc, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: maskName);
            Mask = target;
            // 카메라 깊이와 같은 단일 샘플로 검사한다. AA는 최종 선에만 적용한다.
            ConfigureTarget(Mask);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("JC Building Silhouette Tuning");
            try
            {
                WriteTuning(cmd);
                if (overrideMaterial != null && explicitRenderers != null)
                {
                    int pass = overrideMaterial.FindPass("SilhouetteMask");
                    if (pass >= 0) foreach (var target in explicitRenderers)
                    {
                        if (target == null || !target.enabled || !target.gameObject.activeInHierarchy) continue;
                        // 방문 표식(SpriteRenderer) 등은 건물 메시가 아니다.
                        // Unity의 누락된 Component에는 ?. 대신 명시적 존재 검사를 사용한다.
                        Mesh mesh = null;
                        if (target is SkinnedMeshRenderer skin) mesh = skin.sharedMesh;
                        else if (target is MeshRenderer && target.TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter != null)
                            mesh = meshFilter.sharedMesh;
                        if (mesh == null) continue;
                        for (int i = 0; i < mesh.subMeshCount; i++) cmd.DrawRenderer(target, overrideMaterial, i, pass);
                    }
                    context.ExecuteCommandBuffer(cmd);
                    return;
                }
                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                CommandBufferPool.Release(cmd);
            }
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
        private RTHandle outlineColor;
        private int rawPassIndex, smoothPassIndex;

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
            rawPassIndex = material.FindPass("OutlineRaw");
            smoothPassIndex = material.FindPass("OutlineAntialiasing");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (source.EdgeSmoothing > 0 && rawPassIndex >= 0 && smoothPassIndex >= 0)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0; desc.msaaSamples = 1; desc.bindMS = false;
                desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.useMipMap = false; desc.autoGenerateMips = false;
                RenderingUtils.ReAllocateIfNeeded(ref outlineColor, desc, FilterMode.Bilinear,
                    TextureWrapMode.Clamp, name: "_JcBuildingOutlineColor");
            }
            else { outlineColor?.Release(); outlineColor = null; }
            ConfigureTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (source.Mask == null || material == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("JC Building Silhouette Composite");
            try
            {
                source.WriteTuning(cmd);
                if (outlineColor != null)
                {
                    // 건물 화면을 복사하지 않고 선의 premultiplied 색/알파만 만든다.
                    Blitter.BlitCameraTexture(cmd, source.Mask, outlineColor,
                        RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, material, rawPassIndex);
                    Blitter.BlitCameraTexture(cmd, outlineColor, renderingData.cameraData.renderer.cameraColorTargetHandle,
                        RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, material, smoothPassIndex);
                }
                else
                    Blitter.BlitCameraTexture(cmd, source.Mask, renderingData.cameraData.renderer.cameraColorTargetHandle,
                        RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, material, passIndex);
                source.ClearTuning(cmd);
                context.ExecuteCommandBuffer(cmd);
            }
            finally { CommandBufferPool.Release(cmd); }
        }

        public void Dispose() { outlineColor?.Release(); outlineColor = null; }
    }
}
