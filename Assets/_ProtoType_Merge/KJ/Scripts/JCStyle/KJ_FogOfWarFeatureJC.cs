using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// JC 원본 FogOfWarFeatureJC를 KJ_Work로 복제한 버전.
/// </summary>
public class KJ_FogOfWarFeatureJC : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material fogMaterial;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public Settings settings = new Settings();
    private FogOfWarRenderPass renderPass;

    public override void Create()
    {
        renderPass = new FogOfWarRenderPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.fogMaterial == null) return;
        renderer.EnqueuePass(renderPass);
    }

    protected override void Dispose(bool disposing)
    {
        renderPass?.Dispose();
    }

    private class FogOfWarRenderPass : ScriptableRenderPass
    {
        private readonly Settings settings;
        private static readonly int TempTexId = Shader.PropertyToID("_KJFogOfWarTemp");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        public FogOfWarRenderPass(Settings settings)
        {
            this.settings = settings;
            renderPassEvent = settings.renderPassEvent;
            profilingSampler = new ProfilingSampler("KJ_FogOfWarJC");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (settings.fogMaterial == null) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            var cmd = CommandBufferPool.Get("KJ_FogOfWarJC");

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            cmd.GetTemporaryRT(TempTexId, desc);

            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
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
