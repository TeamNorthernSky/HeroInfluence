using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JC.Environment
{
    /// <summary>월드 깊이 기반 색상 합성과 투명 수면의 공유 구름 패턴. 색 복사 RT는 사용하지 않습니다.</summary>
    public sealed class JcCloudShadowFeature : ScriptableRendererFeature
    {
        [SerializeField, Tooltip("구름 그림자 전용 합성 셰이더입니다. 빌드에서 누락되지 않도록 명시적으로 참조합니다.")]
        private Shader shadowShader;
        private Material material;
        private CloudPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material); material = null; pass = null;
            if (shadowShader == null) return;
            material = CoreUtils.CreateEngineMaterial(shadowShader);
            pass = new CloudPass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Shader.SetGlobalVector("_JcCloudSettings", Vector4.zero);
            if (pass == null || renderingData.cameraData.renderType == CameraRenderType.Overlay
                || !JcCloudShadowController.TryGet(renderingData.cameraData.camera, out var controller)) return;
            pass.Setup(controller);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material); material = null; pass = null;
        }

        private sealed class CloudPass : ScriptableRenderPass
        {
            private readonly Material material;
            private readonly ProfilingSampler sampler = new ProfilingSampler("JC Cloud Shadows");
            private JcCloudShadowController controller;
            public CloudPass(Material material)
            {
                this.material = material;
                // 불투명 월드와 하늘 이후, 기존 건물 실루엣·투명 수면·월드 안내·UI 이전입니다.
                renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 1);
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public void Setup(JcCloudShadowController value) => controller = value;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ConfigureTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
                ConfigureClear(ClearFlag.None, Color.clear);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                // 수면 렌더 이후 초기화하여 다른 카메라/씬에 설정이 남지 않게 합니다.
                cmd.SetGlobalVector("_JcCloudSettings", Vector4.zero);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (material == null || controller == null || !controller.isActiveAndEnabled) return;
                var cmd = CommandBufferPool.Get();
                cmd.SetGlobalVector("_JcCloudSettings", new Vector4(Mathf.Clamp01(controller.strength) * JcDayNightController.CloudFactor, Mathf.Max(1, controller.size),
                    Mathf.Clamp(controller.softness, .05f, .5f), Mathf.Clamp(controller.coverage, .1f, .9f)));
                cmd.SetGlobalVector("_JcCloudMotion", new Vector4(controller.Offset.x, controller.Offset.y, controller.Travel, 0));
                Color tint = QualitySettings.activeColorSpace == ColorSpace.Linear ? controller.shadowColor.linear : controller.shadowColor;
                cmd.SetGlobalColor("_JcCloudColor", tint);
                using (new ProfilingScope(cmd, sampler))
                {
                    CoreUtils.SetRenderTarget(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle,
                        RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, ClearFlag.None, Color.clear);
                    cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
