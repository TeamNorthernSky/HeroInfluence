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
        // JC | 2026-11-16 | 입력에서 유효하다고 확인한 부활 호버에만 죽은 아군의 모델 표시를 허용합니다.
        public bool allowDeadTarget;
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

    // JC | 2026-11-16 | 부활 표시 허용 여부만 전달받고 기존 단일 대상 마스크·합성 패스를 재사용합니다.
    public static void Publish(Object owner, Camera camera, BattleCharactor target, Renderer[] renderers, Color color, float width, float glowOffset = 0, float glowAlpha = 0, bool allowDeadTarget = false)
    {
        if (current == null) current = new HoverState();
        current.owner = owner;
        current.camera = camera;
        current.target = target;
        current.renderers = renderers;
        current.color = color;
        current.width = width;
        current.glowOffset = glowOffset; current.glowAlpha = glowAlpha;
        current.allowDeadTarget = allowDeadTarget;
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
        // JC | 2026-11-16 | 호버 거리 임시 텍스처는 기능 재생성·해제 시 함께 반환합니다.
        composite?.Dispose();
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
                // JC | 2026-11-17 | 캐릭터 하위 파티클/VFX는 MeshFilter가 없으므로 신체 메시 렌더러만 마스크에 전달합니다.
                foreach (var candidate in unit.GetComponentsInChildren<Renderer>(false))
                    if (candidate is MeshRenderer || candidate is SkinnedMeshRenderer) skillRenderers.Add(candidate);
            skillMask.SetRenderers(skillRenderers.ToArray());
            renderer.EnqueuePass(skillMask);
        }
        if (!Application.isPlaying || material == null || current == null || current.owner == null ||
            // JC | 2026-11-16 | 부활 대상으로 전달된 경우에만 사망 차단을 해제하고 비활성·오래된 프레임은 계속 제외합니다.
            current.target == null || (current.target.IsDead && !current.allowDeadTarget) || !current.target.gameObject.activeInHierarchy ||
            current.frame != Time.frameCount || current.width <= 0) return;
        if (renderingData.cameraData.camera != current.camera || renderingData.cameraData.cameraType != CameraType.Game ||
            renderingData.cameraData.renderType == CameraRenderType.Overlay) return;
        mask.SetRenderers(current.renderers);
        // JC | 2026-11-16 | 이미 캐시한 호버 렌더러로 화면 계산 범위를 정합니다. 씬 전체 검색은 추가하지 않습니다.
        composite.SetAppearance(current.color, current.width, current.camera, current.glowOffset, current.glowAlpha, current.renderers);
        renderer.EnqueuePass(mask);
        renderer.EnqueuePass(composite);
    }

    protected override void Dispose(bool disposing)
    {
        mask?.Dispose();
        skillMask?.Dispose();
        composite?.Dispose();
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
            if (renderers == null) return;
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                // JC | 2026-11-17 | 지원하는 메시 렌더러만 처리하고 Unity 객체의 유효성을 명시적으로 검사합니다.
                // ?.는 에디터의 누락 컴포넌트 참조를 걸러내지 못하므로 Smoke/VFX의 MeshFilter 접근 오류를 막지 못합니다.
                Mesh mesh;
                if (renderer is SkinnedMeshRenderer skin) mesh = skin.sharedMesh;
                else if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter) && filter)
                    mesh = filter.sharedMesh;
                else continue;
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
            // JC | 2026-11-17 | 수집을 끝낸 결과만 캐시로 확정하여 중간 예외 뒤 불완전한 마스크가 재사용되지 않게 합니다.
            cached = renderers;
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
        // JC | 2026-11-16 | 가로 거리만 R16에 저장하고 세로 거리와 최종 합성을 묶어 중간 패스는 하나만 추가합니다.
        private RTHandle horizontalDistance;
        private Camera camera;
        private Renderer[] renderers;
        private float width, glowOffset, glowAlpha;
        private static readonly int SourceMaskId = Shader.PropertyToID("_JcHoverSourceMask");
        public CompositePass(MaskPass mask, Material material)
        {
            this.mask = mask;
            this.material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            profilingSampler = new ProfilingSampler("JC Battle Hover Outline");
        }

        public void SetAppearance(Color color, float width, Camera camera, float glowOffset, float glowAlpha, Renderer[] renderers)
        {
            // JC | 2026-11-16 | 색·선명도는 보존하고 픽셀 크기는 실제 렌더 타깃이 준비된 뒤 계산합니다.
            this.width = width; this.camera = camera; this.glowOffset = glowOffset; this.glowAlpha = glowAlpha;
            this.renderers = renderers;
            material.SetColor("_OutlineColor", QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // JC | 2026-11-16 | 회복 기둥의 원본 마스크와 분리된 호버 전용 거리 버퍼입니다. 크기가 같으면 재사용합니다.
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0; descriptor.msaaSamples = 1; descriptor.bindMS = false;
            descriptor.graphicsFormat = GraphicsFormat.R16_SFloat;
            descriptor.useMipMap = false; descriptor.autoGenerateMips = false;
            RenderingUtils.ReAllocateIfNeeded(ref horizontalDistance, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_JcHoverHorizontalDistance");
            float scale = descriptor.height / (float)Mathf.Max(1, camera.pixelHeight);
            float lineWidth = width * scale;
            float waveOffset = glowAlpha > .001f ? glowOffset * scale : 0;
            float waveWidth = 2 * scale;
            int radius = Mathf.CeilToInt(lineWidth + (glowAlpha > .001f ? waveOffset + waveWidth : 0) + 1);
            material.SetVector("_HoverMetrics", new Vector4(lineWidth, waveOffset, glowAlpha, radius));
            // JC | 2026-11-16 | 렌더 배율이 달라도 수렴선·후광 폭은 기존 화면 2픽셀 기준을 유지합니다.
            material.SetVector("_HoverTexel", new Vector4(1f / descriptor.width, 1f / descriptor.height, waveWidth, 0));
            var matrix = renderingData.cameraData.GetGPUProjectionMatrix() * renderingData.cameraData.GetViewMatrix();
            material.SetVector("_HoverRect", CalculatePixelBounds(renderers, matrix, descriptor.width, descriptor.height, radius + 2));
            ConfigureTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (mask.Texture == null || horizontalDistance == null) return;
            var cmd = CommandBufferPool.Get("JC Battle Hover Outline");
            // JC | 2026-11-16 | 캐릭터는 마스크에 한 번만 그립니다. 거리 계산은 캐릭터 주변에서만 하고 원본을 다시 드로우하지 않습니다.
            cmd.SetGlobalTexture(SourceMaskId, mask.Texture.nameID);
            Blitter.BlitCameraTexture(cmd, mask.Texture, horizontalDistance, material, 1);
            Blitter.BlitCameraTexture(cmd, horizontalDistance, renderingData.cameraData.renderer.cameraColorTargetHandle,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, material, 2);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // JC | 2026-11-16 | 애니메이션 Bounds를 합쳐 8점만 투영합니다. GPU 투영과 SV_POSITION의 원점으로 영역을 맞춥니다.
        private static Vector4 CalculatePixelBounds(Renderer[] renderers, Matrix4x4 matrix, int width, int height, float padding)
        {
            bool found = false;
            Bounds bounds = default;
            foreach (var renderer in renderers ?? System.Array.Empty<Renderer>())
            {
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.forceRenderingOff) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            if (!found) return new Vector4(1, 1, 0, 0);
            var min = bounds.min; var max = bounds.max;
            float left = width, bottom = height, right = 0, top = 0;
            for (int i = 0; i < 8; i++)
            {
                var clip = matrix * new Vector4((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z, 1);
                // 시점에 걸친 Bounds는 투영 상한이 없으므로 화면 전체를 포함해야 윤곽이 잘리지 않습니다.
                if (clip.w <= .0001f) return new Vector4(0, 0, width, height);
                float x = (clip.x / clip.w * .5f + .5f) * width;
                float y = (clip.y / clip.w * (SystemInfo.graphicsUVStartsAtTop ? -.5f : .5f) + .5f) * height;
                left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
            }
            return new Vector4(Mathf.Max(0, left - padding), Mathf.Max(0, bottom - padding),
                Mathf.Min(width, right + padding), Mathf.Min(height, top + padding));
        }

        public void Dispose() => horizontalDistance?.Release();
    }
}
