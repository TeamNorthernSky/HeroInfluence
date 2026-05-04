using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DHFogOfWarFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material fogMaterial;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public Settings settings = new Settings();
    private DHFogOfWarRenderPass renderPass;

    public override void Create()
    {
        renderPass = new DHFogOfWarRenderPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.fogMaterial == null)
            return;

        renderer.EnqueuePass(renderPass);
    }

    protected override void Dispose(bool disposing)
    {
        renderPass?.Dispose();
    }

    private class DHFogOfWarRenderPass : ScriptableRenderPass
    {
        private readonly Settings settings;
        private static readonly int TempTexId = Shader.PropertyToID("_DHFogOfWarTemp");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        public DHFogOfWarRenderPass(Settings settings)
        {
            this.settings = settings;
            renderPassEvent = settings.renderPassEvent;
            profilingSampler = new ProfilingSampler("DHFogOfWar");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (settings.fogMaterial == null)
                return;

            if (renderingData.cameraData.cameraType != CameraType.Game)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("DHFogOfWar");

            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            cmd.GetTemporaryRT(TempTexId, descriptor);

            RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            cmd.Blit(source, TempTexId);
            cmd.SetGlobalTexture(MainTexId, TempTexId);
            cmd.Blit(TempTexId, source, settings.fogMaterial, 0);
            cmd.ReleaseTemporaryRT(TempTexId);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
        }
    }
}
