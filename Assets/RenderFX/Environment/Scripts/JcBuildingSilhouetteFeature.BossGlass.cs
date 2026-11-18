using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed partial class JcBuildingSilhouetteFeature
{
    // JC | 2026-11-05 | 공용 OpaqueTexture 축소 설정이 유리 뒤 윤곽을 흐리게 하지 않도록 보스 카메라만 원본 해상도로 복사합니다.
    BossGlassPass bossGlass;
    sealed class BossGlassPass : ScriptableRenderPass
    {
        RTHandle copy;
        // JC | 2026-11-17 | 수면은 유리 배경 캡처 전에 한 번만 그립니다. 캐릭터/VFX의 일반 투명 패스는 유지합니다.
        static readonly ShaderTagId WaterTag = new ShaderTagId("JcBossWater");
        public BossGlassPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        }
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            var descriptor = data.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0; descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref copy, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_JcBossGlassColor");
            ConfigureTarget(data.cameraData.renderer.cameraColorTargetHandle, data.cameraData.renderer.cameraDepthTargetHandle);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            var cmd = CommandBufferPool.Get("JC Boss glass full resolution color");
            try
            {
                cmd.SetGlobalFloat("_JcBossWaterCaptured", 1);
                context.ExecuteCommandBuffer(cmd); cmd.Clear();
                var drawing = CreateDrawingSettings(WaterTag, ref data, SortingCriteria.CommonTransparent);
                var filtering = new FilteringSettings(RenderQueueRange.transparent, data.cameraData.camera.cullingMask);
                context.DrawRenderers(data.cullResults, ref drawing, ref filtering);
                Blitter.BlitCameraTexture(cmd, data.cameraData.renderer.cameraColorTargetHandle, copy);
                cmd.SetGlobalTexture("_JcBossGlassColor", copy.nameID);
                cmd.SetGlobalFloat("_JcBossGlassColorReady", 1);
                CoreUtils.SetRenderTarget(cmd, data.cameraData.renderer.cameraColorTargetHandle, data.cameraData.renderer.cameraDepthTargetHandle);
                context.ExecuteCommandBuffer(cmd);
            }
            finally { CommandBufferPool.Release(cmd); }
        }
        public void Dispose() { copy?.Release(); copy = null; }
    }
}
