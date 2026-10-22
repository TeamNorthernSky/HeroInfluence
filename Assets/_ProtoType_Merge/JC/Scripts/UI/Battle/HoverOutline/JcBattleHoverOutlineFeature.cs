using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>JC 전투 호버 전용 마스크/합성 패스입니다. KJ 코드·머티리얼·스텐실에 의존하지 않습니다.</summary>
public sealed class JcBattleHoverOutlineFeature : ScriptableRendererFeature
{
    [Tooltip("JC 전투 호버 전용 셰이더입니다. 직렬화 참조로 빌드 시 셰이더 누락을 방지합니다.")]
    [SerializeField] private Shader outlineShader;

    private sealed class HoverState
    {
        public Object owner;
        public Camera camera;
        public BattleCharactor target;
        public Renderer[] renderers;
        public Color color;
        public float width, glowOffset, glowAlpha;
        public int frame;
    }

    private static HoverState current;
    private MaskPass mask;
    private CompositePass composite;
    private Material material;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => current = null;

    public static void Publish(Object owner, Camera camera, BattleCharactor target, Renderer[] renderers, Color color, float width, float glowOffset = 0, float glowAlpha = 0)
    {
        if (current == null) current = new HoverState();
        current.owner = owner;
        current.camera = camera;
        current.target = target;
        current.renderers = renderers;
        current.color = color;
        current.width = width;
        current.glowOffset = glowOffset; current.glowAlpha = glowAlpha;
        current.frame = Time.frameCount;
    }

    public static void Clear(Object owner)
    {
        if (current != null && current.owner == owner)
        {
            current.owner = null;
            current.target = null;
            current.renderers = null;
            current.frame = -1;
        }
    }

    public override void Create()
    {
        mask?.Dispose();
        CoreUtils.Destroy(material);
        if (outlineShader == null) return;
        material = CoreUtils.CreateEngineMaterial(outlineShader);
        mask = new MaskPass(outlineShader);
        composite = new CompositePass(mask, material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!Application.isPlaying || material == null || current == null || current.owner == null ||
            current.target == null || current.target.IsDead || !current.target.gameObject.activeInHierarchy ||
            current.frame != Time.frameCount || current.width <= 0) return;
        if (renderingData.cameraData.camera != current.camera || renderingData.cameraData.cameraType != CameraType.Game ||
            renderingData.cameraData.renderType == CameraRenderType.Overlay) return;
        mask.SetRenderers(current.renderers);
        composite.SetAppearance(current.color, current.width, current.camera, current.glowOffset, current.glowAlpha);
        renderer.EnqueuePass(mask);
        renderer.EnqueuePass(composite);
    }

    protected override void Dispose(bool disposing)
    {
        mask?.Dispose();
        CoreUtils.Destroy(material);
        material = null;
    }

    private sealed class MaskPass : ScriptableRenderPass
    {
        private sealed class Draw
        {
            public Renderer renderer;
            public int submesh;
            public Material source;
            public Material maskMaterial;
        }

        private readonly Shader shader;
        private readonly List<Draw> draws = new List<Draw>();
        private Renderer[] cached;
        private RTHandle texture;
        public RTHandle Texture => texture;

        public MaskPass(Shader shader)
        {
            this.shader = shader;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth);
            profilingSampler = new ProfilingSampler("JC Battle Hover Mask");
        }

        public void SetRenderers(Renderer[] renderers)
        {
            if (ReferenceEquals(cached, renderers)) return;
            ClearMaterials();
            cached = renderers;
            if (renderers == null) return;
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                var sources = renderer.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount && sub < sources.Length; sub++)
                {
                    var source = sources[sub];
                    // 반투명 이펙트/보호막은 모델의 단색 윤곽에 포함하지 않습니다.
                    if (source == null || source.renderQueue >= (int)RenderQueue.Transparent) continue;
                    draws.Add(new Draw { renderer = renderer, submesh = sub, source = source,
                        maskMaterial = CoreUtils.CreateEngineMaterial(shader) });
                }
            }
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            RenderingUtils.ReAllocateIfNeeded(ref texture, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_JcBattleHoverMask");
            ConfigureTarget(texture);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("JC Battle Hover Mask");
            foreach (var draw in draws)
            {
                var renderer = draw.renderer;
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy ||
                    (renderingData.cameraData.camera.cullingMask & (1 << renderer.gameObject.layer)) == 0 || draw.source == null) continue;
                var source = draw.source;
                var target = draw.maskMaterial;
                string map = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                target.SetTexture("_BaseMap", source.HasProperty(map) ? source.GetTexture(map) : Texture2D.whiteTexture);
                var scale = source.HasProperty(map) ? source.GetTextureScale(map) : Vector2.one;
                var offset = source.HasProperty(map) ? source.GetTextureOffset(map) : Vector2.zero;
                target.SetVector("_BaseMap_ST", new Vector4(scale.x, scale.y, offset.x, offset.y));
                bool clip = source.IsKeywordEnabled("_ALPHATEST_ON") || (source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > 0.5f);
                target.SetFloat("_Cutoff", clip && source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0f);
                target.SetFloat("_AlphaClip", clip ? 1f : 0f);
                float alpha = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor").a : source.HasProperty("_Color") ? source.GetColor("_Color").a : 1f;
                target.SetFloat("_SourceAlpha", alpha);
                cmd.DrawRenderer(renderer, target, draw.submesh, 0);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void ClearMaterials()
        {
            foreach (var draw in draws) CoreUtils.Destroy(draw.maskMaterial);
            draws.Clear();
            cached = null;
        }

        public void Dispose() { ClearMaterials(); texture?.Release(); texture = null; }
    }

    private sealed class CompositePass : ScriptableRenderPass
    {
        private readonly MaskPass mask;
        private readonly Material material;
        public CompositePass(MaskPass mask, Material material)
        {
            this.mask = mask;
            this.material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            profilingSampler = new ProfilingSampler("JC Battle Hover Outline");
        }

        public void SetAppearance(Color color, float width, Camera camera, float glowOffset, float glowAlpha)
        {
            material.SetVector("_Convergence", new Vector4(glowOffset / Mathf.Max(.1f,width), 2f / Mathf.Max(.1f,width), glowAlpha, 0));
            material.SetColor("_OutlineColor", QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color);
            material.SetVector("_OutlineStep", new Vector4(width / Mathf.Max(1, camera.pixelWidth), width / Mathf.Max(1, camera.pixelHeight), 0, 0));
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (mask.Texture == null) return;
            var cmd = CommandBufferPool.Get("JC Battle Hover Outline");
            Blitter.BlitCameraTexture(cmd, mask.Texture, renderingData.cameraData.renderer.cameraColorTargetHandle,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, material, 1);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
