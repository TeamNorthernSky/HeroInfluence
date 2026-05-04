using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// JC 원본 FogStencilPrepassFeature를 KJ_Work로 복제한 버전.
/// </summary>
public class KJ_FogStencilPrepassFeature : ScriptableRendererFeature
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
        private readonly Material material;
        private readonly ProfilingSampler profilingSampler = new ProfilingSampler("KJ_FogStencilPrepass");

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
