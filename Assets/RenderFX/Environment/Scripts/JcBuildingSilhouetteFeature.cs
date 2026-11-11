using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 가려진 건물 메시를 독립 마스크에 합친 뒤 한 번만 단색 합성한다.
/// 기존 외곽선/FOW의 스텐실과 카메라 깊이는 수정하지 않는다.
/// DH의 transparentOverrideMaterial과 같은 머티리얼을 지정한다.
/// </summary>
public sealed partial class JcBuildingSilhouetteFeature : ScriptableRendererFeature
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

    private static JC.Indicators.JcInteractionHoverOutlineController interactionOwner;
    private static Transform interactionTarget;
    private static Renderer[] interactionRenderers;

    public static void SetInteractionHighlight(JC.Indicators.JcInteractionHoverOutlineController owner, Transform target, Renderer[] meshes)
    {
        interactionOwner = owner;
        interactionTarget = target;
        interactionRenderers = meshes;
    }

    public static void ClearInteractionHighlight(JC.Indicators.JcInteractionHoverOutlineController owner)
    {
        if (interactionOwner != owner) return;
        interactionOwner = null;
        interactionTarget = null;
        interactionRenderers = null;
    }

    private MaskPass tutorialMaskPass;
    private CompositePass tutorialCompositePass;
    private MaskPass maskPass;
    private CompositePass compositePass;
    private PartyShadowPass partyShadows;
    private ShadowRemovalPass shadowRemovalPass;

    public override void Create()
    {
        // JC | 2026-11-05 | 보스 유리의 전용 색 복사도 기존 환경 렌더 기능의 수명에 맞춰 관리합니다.
        bossGlass?.Dispose(); bossGlass = new BossGlassPass();
        // JC | 2026-11-04 | 기존 환경 렌더 기능 안에서 이벤트 원본 메시 기록 패스를 관리합니다.
        eventOcclusion?.Dispose();
        eventOcclusion = new EventOcclusionPass();
        compositePass?.Dispose();
        tutorialCompositePass?.Dispose();
        maskPass?.Dispose();
        tutorialMaskPass?.Dispose();
        shadowRemovalPass?.Dispose();
        partyShadows?.Dispose();
        tutorialMaskPass = new MaskPass("_JcTutorialBuildingMask");
        tutorialMaskPass.renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 2);
        tutorialCompositePass = new CompositePass(tutorialMaskPass);
        tutorialCompositePass.renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 1);
        maskPass = new MaskPass();
        compositePass = new CompositePass(maskPass);
        partyShadows = new PartyShadowPass();
        shadowRemovalPass = new ShadowRemovalPass(maskPass, partyShadows);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // JC | 2026-11-05 | 일반/오버레이 카메라에는 새 패스를 추가하지 않고 이전 카메라의 투과색도 사용하지 않습니다.
        Shader.SetGlobalFloat("_JcBossGlassColorReady", 0);
        if (renderingData.cameraData.renderType != CameraRenderType.Overlay &&
            JC.BattleTesting.JcBossBackgroundMaterials.UsesRefraction(renderingData.cameraData.camera)) renderer.EnqueuePass(bossGlass);
        Shader.SetGlobalFloat("_JcEventGhostActive", 0);
        CameraType type = renderingData.cameraData.cameraType;
        if ((type != CameraType.Game && type != CameraType.SceneView)
            || renderingData.cameraData.renderType == CameraRenderType.Overlay
            || silhouetteMaterial == null)
            return;

        int compositeIndex = silhouetteMaterial.FindPass("SilhouetteComposite");
        if (compositeIndex < 0) return;
        if (JcEventOcclusionController.TryResolve(renderingData.cameraData.camera, out var eventController))
        {
            eventOcclusion.Setup(eventController, renderingData.cameraData.camera);
            renderer.EnqueuePass(eventOcclusion);
        }
        bool hasTuning = JcBuildingSilhouetteController.TryGetSettings(renderingData.cameraData.camera, out var tuning);
        maskPass.Setup(hasTuning, tuning, renderingData.cameraData.camera);
        compositePass.Setup(silhouetteMaterial, compositeIndex);
        shadowRemovalPass.Setup(silhouetteMaterial);
        renderer.EnqueuePass(maskPass);
        var scene = type == CameraType.SceneView ? UnityEngine.SceneManagement.SceneManager.GetActiveScene()
            : renderingData.cameraData.camera.gameObject.scene;
        bool known = PartyOcclusionFadeController.TryHasFadedObjects(scene, out bool hasFaded);
        var party = hasFaded ? PartyOcclusionFadeController.FadedParty(scene) : null;
        if (partyShadows.Prepare(party, ref renderingData))
            renderer.EnqueuePass(partyShadows);
        if (!known || hasFaded)
            renderer.EnqueuePass(shadowRemovalPass);
        float reduction = JC.Environment.JcDayNightController.PartyWallReduction(scene);
        maskPass.party = reduction > 0 ? party : null;
        maskPass.partyMaterial = silhouetteMaterial;
        maskPass.partyReduction = reduction;
        renderer.EnqueuePass(compositePass);
        // 튜토리얼 안내가 있으면 일반 호버보다 우선한다. 외곽선 합성은 같은 패스를 사용한다.
        bool tutorial = tutorialOwner != null && tutorialTarget != null && tutorialTarget.gameObject.scene == scene;
        bool hover = hoverOwner != null && hoverTarget != null && hoverTarget.gameObject.scene == scene;
        bool interaction = !tutorial && !hover && interactionOwner != null && interactionTarget != null && interactionTarget.gameObject.scene == scene;
        if (tutorial || hover || interaction)
        {
            Color hoverColor;
            Vector3 hoverTuning, hoverShape;
            float width = 9f, smoothing;
            if (interaction)
            {
                var v = interactionOwner.Values;
                hoverColor = v.outlineColor;
                hoverTuning = new Vector3(v.outlineIntensity, v.highlightSpeed, v.highlightIntensity);
                hoverShape = new Vector3(v.highlightRange, v.highlightSmoothing, v.whiteOutlineWidth);
                width = v.outlineWidth;
                smoothing = v.edgeSmoothing;
            }
            else
            {
                JC.Indicators.JcBuildingHoverOutlineController.Resolve(scene, out hoverColor, out hoverTuning, out hoverShape);
                JC.Indicators.JcBuildingHoverOutlineController.ResolveAntialiasing(scene, out smoothing);
            }
            var highlight = JcBuildingSilhouetteSettings.Default;
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2 / 1.6f);
            highlight.opacity = 0;
            highlight.outlineColor = tutorial ? Color.Lerp(tutorialColor, Color.white, pulse) : hoverColor;
            highlight.outlineOpacity = 1;
            highlight.outlineWidthMode = JcBuildingOutlineWidthMode.ScreenPixels;
            highlight.outlineWidth = tutorial ? Mathf.Lerp(9, 15, pulse) : width;
            tutorialMaskPass.Setup(true, highlight, renderingData.cameraData.camera);
            tutorialMaskPass.explicitRenderers = tutorial ? tutorialRenderers : interaction ? interactionRenderers : hoverRenderers;
            if (!tutorial) tutorialMaskPass.SetHoverOrbit(renderingData.cameraData.camera, tutorialMaskPass.explicitRenderers, hoverTuning, hoverShape);
            tutorialMaskPass.throughWalls = interaction;
            // JC | 2026-11-04 | 건물 강조만 내부 틈을 제거한 외곽을 사용하며 상호작용 표식의 투과 정책은 유지합니다.
            tutorialMaskPass.exteriorOnly = !interaction;
            tutorialMaskPass.SetAntialiasing(smoothing);
            tutorialMaskPass.renderPassEvent = !interaction ? (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 2) : RenderPassEvent.AfterRenderingTransparents;
            tutorialCompositePass.renderPassEvent = (RenderPassEvent)((int)tutorialMaskPass.renderPassEvent + 1);
            tutorialMaskPass.overrideMaterial = silhouetteMaterial;
            tutorialCompositePass.Setup(silhouetteMaterial, compositeIndex);
            renderer.EnqueuePass(tutorialMaskPass);
            renderer.EnqueuePass(tutorialCompositePass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        bossGlass?.Dispose();
        eventOcclusion?.Dispose();
        compositePass?.Dispose();
        tutorialCompositePass?.Dispose();
        maskPass?.Dispose();
        tutorialMaskPass?.Dispose();
        shadowRemovalPass?.Dispose();
        partyShadows?.Dispose();
    }

    private sealed class MaskPass : ScriptableRenderPass
    {
        public PartyGridMover party;
        public Material partyMaterial;
        public float partyReduction;
        private readonly System.Collections.Generic.List<Renderer> partyMeshes = new System.Collections.Generic.List<Renderer>();
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
        // JC | 2026-11-04 | 원본 깊이는 보존하고 외곽용 모양만 별도로 합성합니다.
        private RTHandle rawMask, exteriorMask, envelopeA, envelopeB;
        // JC | 2026-11-10 | 렌더 시점에 지원 포맷을 한 번 확인하며, 미지원 장치도 같은 비트 표현을 RGBA8의 R에 저장합니다.
        private GraphicsFormat envelopeFormat;
        private bool exteriorReady;
        public RTHandle Mask => exteriorReady ? exteriorMask : rawMask;
        public Renderer[] explicitRenderers;
        public bool throughWalls, exteriorOnly;
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
            // JC | 2026-11-06 | 튜토리얼은 회전광 범위를 설정하지 않으므로 이전 호버 대상의 범위가 외곽선을 자르지 않게 초기화합니다.
            orbitBounds = Vector4.zero;
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
            cmd.SetGlobalFloat("_JcBuildingExteriorOnly", exteriorOnly ? 1 : 0);
            cmd.SetGlobalVector("_JcBuildingExteriorRadius", new Vector4(pixelSize.x, pixelSize.y, tuningVector.w + orbit.w + edgeSmoothing + 1, 0));
            cmd.SetGlobalFloat("_JcBuildingHighlight", overrideMaterial != null && explicitRenderers != null ? (throughWalls ? 2f : 1f) : 0f);
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
            cmd.SetGlobalFloat("_JcBuildingExteriorOnly", 0);
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
            exteriorReady = false;
            RTHandle target = rawMask;
            RenderingUtils.ReAllocateIfNeeded(ref target, desc, FilterMode.Bilinear,
                TextureWrapMode.Clamp, name: maskName);
            rawMask = target;
            if (exteriorOnly)
            {
                RenderingUtils.ReAllocateIfNeeded(ref exteriorMask, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: maskName + "Exterior");
                if (envelopeFormat == GraphicsFormat.None)
                    envelopeFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, FormatUsage.Render)
                        && SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, FormatUsage.Blend)
                        && SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, FormatUsage.Sample)
                        ? GraphicsFormat.R8_UNorm : GraphicsFormat.R8G8B8A8_UNorm;
                desc.graphicsFormat = envelopeFormat;
                RenderingUtils.ReAllocateIfNeeded(ref envelopeA, desc, FilterMode.Point, TextureWrapMode.Clamp, name: maskName + "ScanA");
                RenderingUtils.ReAllocateIfNeeded(ref envelopeB, desc, FilterMode.Point, TextureWrapMode.Clamp, name: maskName + "ScanB");
            }
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
                    if (exteriorOnly) BuildExterior(cmd);
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
            if (!party) return;

            // The building owns RGB. Only visible party surfaces write the reduction to A.
            int partyPass = partyMaterial.FindPass("VisiblePartyCoverage");
            if (partyPass < 0) return;
            var coverage = CommandBufferPool.Get("JC Visible Party Coverage");
            try
            {
                coverage.SetGlobalFloat("_JcPartyWallReduction", partyReduction);
                party.GetComponentsInChildren(false, partyMeshes);
                int layers = renderingData.cameraData.camera.cullingMask;
                foreach (var target in partyMeshes)
                {
                    if (!target.enabled || target.forceRenderingOff || (layers & (1 << target.gameObject.layer)) == 0) continue;
                    Mesh mesh = null;
                    if (target is SkinnedMeshRenderer skin) mesh = skin.sharedMesh;
                    else if (target is MeshRenderer && target.GetComponentInParent<UnitVisualMotionController>() &&
                             target.TryGetComponent<MeshFilter>(out var mf)) mesh = mf.sharedMesh;
                    if (!mesh) continue;
                    for (int i = 0; i < mesh.subMeshCount; ++i)
                        coverage.DrawRenderer(target, partyMaterial, i, partyPass);
                }
                context.ExecuteCommandBuffer(coverage);
            }
            finally { CommandBufferPool.Release(coverage); }
        }

        private void BuildExterior(CommandBuffer cmd)
        {
            // JC | 2026-11-04 | 각 방향의 누적 마스크로 내부의 빈 픽셀을 메웁니다.
            // 건물별 선택 마스크에만 실행하며 원본 메시·파티 깊이·일반 투과 실루엣은 변경하지 않습니다.
            int init = overrideMaterial.FindPass("ExteriorEnvelopeInit");
            int scan = overrideMaterial.FindPass("ExteriorEnvelopeScan");
            int combine = overrideMaterial.FindPass("ExteriorEnvelopeCombine");
            if (init < 0 || scan < 0 || combine < 0) return;
            cmd.SetGlobalVector("_JcBuildingEnvelopeTexel", new Vector4(1f / rawMask.rt.width, 1f / rawMask.rt.height, 0, 0));
            Blitter.BlitCameraTexture(cmd, rawMask, envelopeA, overrideMaterial, init);
            var a = envelopeA; var b = envelopeB;
            for (int step = 1; step < Mathf.Max(rawMask.rt.width, rawMask.rt.height); step *= 2)
            {
                cmd.SetGlobalFloat("_JcBuildingEnvelopeStep", step);
                Blitter.BlitCameraTexture(cmd, a, b, overrideMaterial, scan);
                var swap = a; a = b; b = swap;
            }
            cmd.SetGlobalTexture("_JcBuildingEnvelopeSource", rawMask.nameID);
            Blitter.BlitCameraTexture(cmd, a, exteriorMask, overrideMaterial, combine);
            exteriorReady = true;
        }
        public void Dispose()
        {
            rawMask?.Release(); exteriorMask?.Release(); envelopeA?.Release(); envelopeB?.Release();
            rawMask = exteriorMask = envelopeA = envelopeB = null; exteriorReady = false;
        }
    }

    // Use the camera's own cascade layout so the receiver uses the same shadow
    // strength, bias, distance fade and soft sampling on both sides of the wall.
    private sealed class PartyShadowPass : ScriptableRenderPass
    {
        private readonly ShadowSliceData[] slices = new ShadowSliceData[4];
        private readonly System.Collections.Generic.List<Renderer> renderers = new System.Collections.Generic.List<Renderer>();
        private readonly System.Collections.Generic.List<Material> materials = new System.Collections.Generic.List<Material>();
        private RTHandle atlas;
        private int width, height, cascades;
        public RTHandle ReadyAtlas { get; private set; }

        public PartyShadowPass()
        {
            renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingOpaques + 1);
        }

        public bool Prepare(PartyGridMover party, ref RenderingData data)
        {
            ReadyAtlas = null;
            if (!party || !data.shadowData.supportsMainLightShadows ||
                data.cameraData.maxShadowDistance <= 0 || data.lightData.mainLightIndex < 0) return false;
            var light = data.lightData.visibleLights[data.lightData.mainLightIndex].light;
            if (!light || light.type != LightType.Directional || light.shadows == LightShadows.None ||
                light.shadowStrength <= 0) return false;
            party.GetComponentsInChildren(false, renderers);
            cascades = data.shadowData.mainLightShadowCascadesCount;
            width = data.shadowData.mainLightShadowmapWidth;
            int fullHeight = data.shadowData.mainLightShadowmapHeight;
            height = cascades == 2 ? fullHeight / 2 : fullHeight;
            int tile = ShadowUtils.GetMaxTileResolutionInAtlas(width, fullHeight, cascades);
            for (int i = 0; i < cascades; i++)
                if (!ShadowUtils.ExtractDirectionalLightMatrix(ref data.cullResults, ref data.shadowData,
                    data.lightData.mainLightIndex, i, width, height, tile, light.shadowNearPlane, out _, out slices[i]))
                    return false;
            return renderers.Count > 0;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            ShadowUtils.ShadowRTReAllocateIfNeeded(ref atlas, width, height, 16, name: "_JcPartyShadowAtlas");
            ConfigureTarget(atlas);
            ConfigureClear(ClearFlag.Depth, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            var cmd = CommandBufferPool.Get("JC Party Directional Shadows");
            int index = data.lightData.mainLightIndex;
            var light = data.lightData.visibleLights[index];
            const string punctual = "_CASTING_PUNCTUAL_LIGHT_SHADOW";
            bool wasPunctual = Shader.IsKeywordEnabled(punctual);
            try
            {
                cmd.DisableShaderKeyword(punctual);
                cmd.SetGlobalDepthBias(1f, 2.5f);
                for (int i = 0; i < cascades; i++)
                {
                    var slice = slices[i];
                    var bias = ShadowUtils.GetShadowBias(ref light, index, ref data.shadowData,
                        slice.projectionMatrix, slice.resolution);
                    ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref light, bias);
                    cmd.SetViewport(new Rect(slice.offsetX, slice.offsetY, slice.resolution, slice.resolution));
                    cmd.SetViewProjectionMatrices(slice.viewMatrix, slice.projectionMatrix);
                    foreach (var caster in renderers)
                    {
                        if (!caster || !caster.enabled || caster.forceRenderingOff ||
                            caster.shadowCastingMode == ShadowCastingMode.Off ||
                            (light.light.cullingMask & (1 << caster.gameObject.layer)) == 0) continue;
                        if (!(caster is SkinnedMeshRenderer) &&
                            !(caster is MeshRenderer && caster.GetComponentInParent<UnitVisualMotionController>())) continue;
                        caster.GetSharedMaterials(materials);
                        for (int slot = 0; slot < materials.Count; slot++)
                        {
                            var mat = materials[slot];
                            if (!mat || !mat.GetShaderPassEnabled("ShadowCaster")) continue;
                            int pass = mat.FindPass("ShadowCaster");
                            if (pass >= 0) cmd.DrawRenderer(caster, mat, slot, pass);
                        }
                    }
                }
                cmd.SetGlobalDepthBias(0, 0);
                CoreUtils.SetKeyword(cmd, punctual, wasPunctual);
                cmd.SetViewProjectionMatrices(data.cameraData.GetViewMatrix(), data.cameraData.GetProjectionMatrix());
                context.ExecuteCommandBuffer(cmd);
                ReadyAtlas = atlas;
            }
            finally { CommandBufferPool.Release(cmd); }
        }

        public void Dispose() { atlas?.Release(); atlas = null; ReadyAtlas = null; }
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

        private readonly PartyShadowPass partyShadows;
        private static readonly int MainAtlas = Shader.PropertyToID("_MainLightShadowmapTexture");

        public ShadowRemovalPass(MaskPass source, PartyShadowPass partyShadows)
        {
            this.source = source;
            renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingOpaques + 2);
            this.partyShadows = partyShadows;
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
            var originalAtlas = Shader.GetGlobalTexture(MainAtlas);
            bool restoreParty = partyShadows.ReadyAtlas != null && originalAtlas != null;
            try
            {
                for (int i = 0; i < ShadowKeywords.Length; i++)
                {
                    previousKeywords[i] = Shader.IsKeywordEnabled(ShadowKeywords[i]);
                    cmd.DisableShaderKeyword(ShadowKeywords[i]);
                }
                if (restoreParty)
                {
                    cmd.SetGlobalTexture(MainAtlas, partyShadows.ReadyAtlas.nameID);
                    CoreUtils.SetKeyword(cmd, ShadowKeywords[renderingData.shadowData.mainLightShadowCascadesCount > 1 ? 1 : 0], true);
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
                if (restoreParty) cmd.SetGlobalTexture(MainAtlas, originalAtlas);
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
