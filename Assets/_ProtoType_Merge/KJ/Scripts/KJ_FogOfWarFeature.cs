using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP renderer feature for the KJ fog of war post-process pass.
/// </summary>
public class KJ_FogOfWarFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class FogSettings
    {
        public bool isEnabled = true;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        public Material fogMaterial;
    }

    public FogSettings settings = new FogSettings();

    private KJ_FogOfWarRenderPass fogPass;

    public override void Create()
    {
        fogPass = new KJ_FogOfWarRenderPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!settings.isEnabled || settings.fogMaterial == null)
            return;

        if (renderingData.cameraData.cameraType != CameraType.Game)
            return;

        renderer.EnqueuePass(fogPass);
    }

    protected override void Dispose(bool disposing)
    {
        fogPass?.Dispose();
    }

    private class KJ_FogOfWarRenderPass : ScriptableRenderPass
    {
        private readonly FogSettings settings;
        private RTHandle sourceHandle;
        private RTHandle tempHandle;

        public KJ_FogOfWarRenderPass(FogSettings settings)
        {
            this.settings = settings;
            renderPassEvent = settings.renderPassEvent;
            profilingSampler = new ProfilingSampler("KJ_FogOfWar");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureInput(ScriptableRenderPassInput.Depth);

            sourceHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            RenderingUtils.ReAllocateIfNeeded(
                ref tempHandle,
                desc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_KJFogOfWarTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (settings.fogMaterial == null || sourceHandle == null || tempHandle == null)
                return;

            var cmd = CommandBufferPool.Get("KJ_FogOfWar");

            Blitter.BlitCameraTexture(cmd, sourceHandle, tempHandle);
            Blitter.BlitCameraTexture(cmd, tempHandle, sourceHandle, settings.fogMaterial, 0);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            tempHandle?.Release();
        }
    }
}
