using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Visibility 텍스처를 읽어 Fogged 영역에 stencil=1을 기록하는 프리패스.
/// 이후 FogDynamicClipPassFeature가 이 stencil을 이용해 FogDynamic Renderer를 clip한다.
/// </summary>
public class FogStencilPrepassFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader stencilShader;
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public Settings settings = new Settings();

    private class Pass : ScriptableRenderPass
    {
        private Material material;
        private readonly ProfilingSampler profilingSampler = new ProfilingSampler("FogStencilPrepass");

        public Pass(Material mat, RenderPassEvent evt)
        {
            material = mat;
            renderPassEvent = evt;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null) return;

            CameraType type = renderingData.cameraData.cameraType;
            if (type != CameraType.Game && type != CameraType.SceneView) return;

            Camera cam = renderingData.cameraData.camera;
            if (cam == null || cam.pixelWidth <= 0 || cam.pixelHeight <= 0) return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                // 풀스크린 삼각형 — SV_VertexID 기반
                cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    private Material material;
    private Pass pass;

    public override void Create()
    {
        if (settings.stencilShader != null)
        {
            if (material != null) CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(settings.stencilShader);
        }
        pass = new Pass(material, settings.passEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null) return;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
    }
}
