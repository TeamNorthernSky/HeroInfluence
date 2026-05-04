using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FogOfWarRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("FogOfWar.shader가 적용된 Material")]
        public Material fogMaterial;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public Settings settings = new Settings();
    private FogOfWarPass _fogPass;

    class FogOfWarPass : ScriptableRenderPass
    {
        private Material _material;
        private RTHandle _source;
        private RTHandle _tempHandle;

        public FogOfWarPass(Material material, RenderPassEvent passEvent)
        {
            _material = material;
            renderPassEvent = passEvent;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // 월드 좌표 복원을 위해 Depth 정보가 필요함을 URP에 알립니다.
            ConfigureInput(ScriptableRenderPassInput.Depth);

            _source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0; // 복사용 임시 RT에는 Depth를 담지 않음
            RenderingUtils.ReAllocateIfNeeded(ref _tempHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TempFogRT");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null || _source == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("FogOfWar Blit");

            // Unity 2022+ Blitter API 사용
            Blitter.BlitCameraTexture(cmd, _source, _tempHandle, _material, 0);
            Blitter.BlitCameraTexture(cmd, _tempHandle, _source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            _tempHandle?.Release();
        }
    }

    public override void Create()
    {
        if (settings.fogMaterial != null)
        {
            _fogPass = new FogOfWarPass(settings.fogMaterial, settings.renderPassEvent);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game || settings.fogMaterial == null) return;
        renderer.EnqueuePass(_fogPass);
    }

    protected override void Dispose(bool disposing)
    {
        _fogPass?.Dispose();
        base.Dispose(disposing);
    }
}
