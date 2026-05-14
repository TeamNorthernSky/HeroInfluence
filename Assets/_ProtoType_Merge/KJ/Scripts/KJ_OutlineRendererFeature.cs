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
            [Header("=== Stencil Pre-pass ===")]
            [Tooltip("캐릭터 실루엣을 스텐실 버퍼에 기록할 머티리얼 (KJ_StencilWrite 셰이더)")]
            public Material stencilWriteMaterial;

            [Header("=== Outline Pass ===")]
            [Tooltip("외곽선을 그릴 머티리얼 (KJ_Outline 셰이더)")]
            public Material outlineMaterial;

            [Header("=== 공통 설정 ===")]
            [Tooltip("외곽선을 적용할 레이어를 선택합니다.")]
            public LayerMask layerMask = -1;
        }

        public OutlineSettings settings = new OutlineSettings();

        // ──────────────────────────────────────────────
        // 단일 Pass 안에서 Stencil Write와 Outline Draw를 순차적으로 실행
        // ──────────────────────────────────────────────
        class OutlineRenderPass : ScriptableRenderPass
        {
            private Material stencilMaterial;
            private Material outlineMaterial;
            private FilteringSettings filteringSettings;
            private ProfilingSampler profilingSampler;

            // URP에서 Lit/Toon 머티리얼을 포함하는 모든 패스 태그
            private static readonly ShaderTagId[] shaderTagIds =
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("LightweightForward"),
                new ShaderTagId("SRPDefaultUnlit")
            };

            public OutlineRenderPass(Material stencilMat, Material outlineMat, LayerMask layerMask)
            {
                // 불투명 오브젝트 렌더링이 끝난 직후 (Skybox 렌더링 이후)
                this.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
                this.stencilMaterial = stencilMat;
                this.outlineMaterial = outlineMat;
                this.filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
                this.profilingSampler = new ProfilingSampler("KJ Stencil & Outline Pass");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (stencilMaterial == null || outlineMaterial == null) return;

                // 커맨드 버퍼 할당
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                    var drawSettings = CreateDrawingSettings(shaderTagIds[0], ref renderingData, sortFlags);
                    for (int i = 1; i < shaderTagIds.Length; i++)
                        drawSettings.SetShaderPassName(i, shaderTagIds[i]);

                    // 1번 패스 (Stencil Write)
                    // ColorMask 0 설정으로 화면에는 보이지 않지만 스텐실 버퍼에 Ref 1을 기록
                    drawSettings.overrideMaterial = stencilMaterial;
                    drawSettings.overrideMaterialPassIndex = 0;
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);

                    // 2번 패스 (Outline Draw)
                    // 스텐실 1이 없는 영역(캐릭터 바깥)에만 외곽선을 기록
                    drawSettings.overrideMaterial = outlineMaterial;
                    drawSettings.overrideMaterialPassIndex = 0;
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);
                }

                // 버퍼 실행 및 해제
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        OutlineRenderPass m_OutlinePass;

        public override void Create()
        {
            m_OutlinePass = new OutlineRenderPass(settings.stencilWriteMaterial, settings.outlineMaterial, settings.layerMask);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.stencilWriteMaterial == null || settings.outlineMaterial == null)
                return;

            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            renderer.EnqueuePass(m_OutlinePass);
        }
    }
}
