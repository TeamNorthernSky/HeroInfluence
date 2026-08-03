using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Renderer Feature: 전장의 안개를 후처리 패스로 삽입.
/// </summary>
public class FogOfWarFeatureJC : ScriptableRendererFeature
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
        private static readonly int TempTexId = Shader.PropertyToID("_FogOfWarTemp");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        public FogOfWarRenderPass(Settings settings)
        {
            this.settings = settings;
            renderPassEvent = settings.renderPassEvent;
            profilingSampler = new ProfilingSampler("FogOfWar");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (settings.fogMaterial == null) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            var cmd = CommandBufferPool.Get("FogOfWar");

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            cmd.GetTemporaryRT(TempTexId, desc);

            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // 1) 원본 씬을 임시 RT에 복사 (안개 없이)
            cmd.Blit(source, TempTexId);

            // 2) 임시 RT를 _MainTex로 명시 설정
            cmd.SetGlobalTexture(MainTexId, TempTexId);

            // 3) 안개 셰이더를 적용하여 카메라 타겟에 직접 출력
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
