using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JC.Indicators
{
    // JC | 2026-11-05 | 아놀드 진동이 적용된 최종 카메라로 트리거 마스크와 실제 화면을 일치시킵니다.
    // 공용 렌더러의 다른 효과는 그대로 두고 트리거가 있는 동안에만 전용 패스를 실행합니다.
    public sealed class JcTriggerAreaFeature : ScriptableRendererFeature
    {
        private DepthPass pass;
        public override void Create() => pass = new DepthPass();

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (JcTriggerAreaDepth.HasControllers) renderer.EnqueuePass(pass);
        }

        private sealed class DepthPass : ScriptableRenderPass
        {
            public DepthPass()
            {
                renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 2);
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var renderer = renderingData.cameraData.renderer;
                ConfigureTarget(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
                ConfigureClear(ClearFlag.None, Color.clear);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var data = renderingData.cameraData;
                var command = CommandBufferPool.Get("JC trigger private depth after camera shake");
                try
                {
                    command.SetGlobalFloat("_JcTriggerDepthActive", 0);
                    // JC | 2026-11-05 | URP가 렌더에 쓰는 뷰/투영을 사용하고 전용 RT의 Y 방향만 변환합니다.
                    var projection = GL.GetGPUProjectionMatrix(data.GetProjectionMatrix(), true);
                    JcTriggerAreaDepth.RenderDepth(command, data.camera, data.GetViewMatrix(), projection,
                        data.cameraTargetDescriptor.width, data.cameraTargetDescriptor.height);
                    // 전용 RT 작성 후 색상/깊이 타깃을 복구해 이후 구름·투명 표시를 같은 카메라에 계속 그립니다.
                    CoreUtils.SetRenderTarget(command, data.renderer.cameraColorTargetHandle,
                        data.renderer.cameraDepthTargetHandle, ClearFlag.None, Color.clear);
                    if (data.renderer.cameraColorTargetHandle.rt == null) command.SetViewport(data.camera.pixelRect);
                    context.ExecuteCommandBuffer(command);
                }
                finally { CommandBufferPool.Release(command); }
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                // JC | 2026-11-05 | 다음 카메라나 트리거 없는 씬에 이전 마스크가 사용되지 않게 합니다.
                cmd.SetGlobalFloat("_JcTriggerDepthActive", 0);
            }
        }
    }
}
