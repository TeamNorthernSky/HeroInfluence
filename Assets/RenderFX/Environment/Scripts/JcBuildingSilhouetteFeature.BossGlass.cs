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
            ConfigureTarget(copy);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            var cmd = CommandBufferPool.Get("JC Boss glass full resolution color");
            try
            {
                Blitter.BlitCameraTexture(cmd, data.cameraData.renderer.cameraColorTargetHandle, copy);
                cmd.SetGlobalTexture("_JcBossGlassColor", copy.nameID);
                cmd.SetGlobalFloat("_JcBossGlassColorReady", 1);
                context.ExecuteCommandBuffer(cmd);
            }
            finally { CommandBufferPool.Release(cmd); }
        }
        public void Dispose() { copy?.Release(); copy = null; }
    }
}
