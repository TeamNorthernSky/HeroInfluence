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
    // JC | 2026-11-06 | 기존 알파·장면 깊이 판정 마스크를 회복 기둥에도 재사용합니다. 호버 대상과 별도 RT로 분리합니다.
    private MaskPass skillMask;
    private readonly List<Renderer> skillRenderers = new List<Renderer>();

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
        skillMask?.Dispose();
        CoreUtils.Destroy(material);
        if (outlineShader == null) return;
        material = CoreUtils.CreateEngineMaterial(outlineShader);
        mask = new MaskPass(outlineShader);
        // JC | 2026-11-06 | 투명 스킬을 그리기 전에 화면에 실제로 보이는 캐릭터 픽셀만 표시합니다.
        skillMask = new MaskPass(outlineShader, true);
        composite = new CompositePass(mask, material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // JC | 2026-11-06 | 회복 기둥이 있는 카메라에서만 추가 패스를 실행합니다. 건물 뒤 캐릭터는 기존 깊이 검사로 제외됩니다.
        if (skillMask != null && renderingData.cameraData.renderType != CameraRenderType.Overlay &&
            JC.VFX.HealAuraGlow.HasVisibleAura(renderingData.cameraData.camera))
        {
            skillRenderers.Clear();
            foreach (var unit in Object.FindObjectsByType<BattleCharactor>(FindObjectsSortMode.None))
                skillRenderers.AddRange(unit.GetComponentsInChildren<Renderer>(false));
            skillMask.SetRenderers(skillRenderers.ToArray());
            renderer.EnqueuePass(skillMask);
        }
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
        skillMask?.Dispose();
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
        // JC | 2026-11-06 | 같은 마스크 구현을 호버 합성과 스킬의 캐릭터 가림 분류에 각각 독립적으로 사용합니다.
        private readonly bool forSkill;
        private static readonly int SkillMaskId = Shader.PropertyToID("_JcSkillCharacterMask");
        private static readonly int SkillMaskActiveId = Shader.PropertyToID("_JcSkillCharacterMaskActive");
        public RTHandle Texture => texture;

        public MaskPass(Shader shader, bool forSkill = false)
        {
            this.shader = shader;
            this.forSkill = forSkill;
            renderPassEvent = forSkill ? RenderPassEvent.BeforeRenderingTransparents : RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth);
            profilingSampler = new ProfilingSampler("JC Battle Hover Mask");
        }

        public void SetRenderers(Renderer[] renderers)
        {
            if (ReferenceEquals(cached, renderers)) return;
            // JC | 2026-11-06 | 프레임별 캐릭터 검색 결과가 같으면 원본 재질별 마스크 재질을 계속 재사용합니다.
            if (forSkill && cached != null && renderers != null && cached.Length == renderers.Length)
            {
                bool same = true;
                for (int i = 0; i < cached.Length; i++) if (cached[i] != renderers[i]) { same = false; break; }
                if (same) return;
            }
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
                    if (source == null || source.renderQueue >= (forSkill ? 2501 : (int)RenderQueue.Transparent)) continue;
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
            // JC | 2026-11-06 | 스킬 마스크는 깊이 픽셀과 1:1로 대조하므로 점 샘플링을 사용합니다.
            RenderingUtils.ReAllocateIfNeeded(ref texture, descriptor, forSkill ? FilterMode.Point : FilterMode.Bilinear, TextureWrapMode.Clamp,
                name: forSkill ? "_JcSkillCharacterMask" : "_JcBattleHoverMask");
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
#if UNITY_EDITOR
                // JC | 2026-11-06 | 임시 검증/프리팹 카메라에서 제외된 다른 씬의 캐릭터를 수동 마스크에 그리지 않습니다.
                ulong sceneMask = renderingData.cameraData.camera.overrideSceneCullingMask;
                if (forSkill && sceneMask != 0 && (UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(renderer.gameObject.scene) & sceneMask) == 0) continue;
#endif
                var source = draw.source;
                var target = draw.maskMaterial;
                // JC | 2026-11-06 | 스킬 예외는 실제 장면 깊이와 일치하는 캐릭터 표면에만 허용합니다.
                target.SetFloat("_RequireDepthMatch", forSkill ? 1 : 0);
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
            // JC | 2026-11-06 | 현재 카메라에서만 유효한 마스크를 전달하고 카메라 정리 시 사용 플래그를 해제합니다.
            if (forSkill) { cmd.SetGlobalTexture(SkillMaskId, texture.nameID); cmd.SetGlobalFloat(SkillMaskActiveId, 1); }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // JC | 2026-11-06 | 다음 카메라·스킬 없는 프레임에 이전 캐릭터 마스크가 남지 않게 합니다.
            if (forSkill) cmd.SetGlobalFloat(SkillMaskActiveId, 0);
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
