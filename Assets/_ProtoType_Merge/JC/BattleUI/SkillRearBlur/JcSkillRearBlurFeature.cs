using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JC.BattleUI
{
    // JC | 2026-11-11 | 개체 보호 마스크를 제거하고 장면 깊이만 사용합니다. 투명 VFX/UI보다 앞에서 월드 후방 보케를 합성합니다.
    public sealed class JcSkillRearBlurFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader blurShader;
        Material material;
        BlurPass blur;
        public override void Create()
        {
            DisposeResources();
            if (!blurShader) return;
            material = CoreUtils.CreateEngineMaterial(blurShader);
            blur = new BlurPass(material);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (blur == null || data.cameraData.cameraType != CameraType.Game || data.cameraData.renderType == CameraRenderType.Overlay) return;
            var owner = JcSkillRearBlurController.ForCamera(data.cameraData.camera);
            if (!owner) return;
            blur.owner = owner;
            renderer.EnqueuePass(blur);
        }
        void DisposeResources() { blur?.Dispose(); CoreUtils.Destroy(material); blur = null; material = null; }
        protected override void Dispose(bool disposing) => DisposeResources();

        sealed class BlurPass : ScriptableRenderPass
        {
            readonly Material material;
            RTHandle original, source;
            public JcSkillRearBlurController owner;
            public BlurPass(Material material)
            {
                this.material = material;
                renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents - 1);
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
                profilingSampler = new ProfilingSampler("JC World Rear Bokeh");
            }
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
            {
                var d = data.cameraData.cameraTargetDescriptor;
                d.depthBufferBits = 0; d.msaaSamples = 1; d.bindMS = false; d.useMipMap = false; d.autoGenerateMips = false;
                RenderingUtils.ReAllocateIfNeeded(ref original, d, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_JcRearOriginal");
                // JC | 2026-11-11 | 축소 없이 같은 픽셀의 RGB와 착란원 반경을 함께 저장합니다. 개체 보호 마스크가 아닌 일반 DOF 입력입니다.
                d.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
                RenderingUtils.ReAllocateIfNeeded(ref source, d, FilterMode.Point, TextureWrapMode.Clamp, name: "_JcRearSource");
                ConfigureTarget(data.cameraData.renderer.cameraColorTargetHandle);
                ConfigureClear(ClearFlag.None, Color.clear);
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                if (!owner || !owner.Draft) return;
                var settings = owner.Draft;
                var d = data.cameraData.cameraTargetDescriptor;
                var cmd = CommandBufferPool.Get("JC World Rear Bokeh");
                try
                {
                    material.SetVector("_RearPlane", owner.RearPlane);
                    material.SetVector("_DepthBands", owner.CurrentDistances);
                    material.SetVector("_SourceTexelSize", new Vector4(1f / d.width, 1f / d.height, d.width, d.height));
                    // JC | 2026-11-11 | 진입/복귀도 반경에 곱해 고정 블러 이미지를 덮는 경계 대신 연속적으로 초점이 변합니다.
                    material.SetVector("_Radii", new Vector4(Mathf.Clamp(settings.nearRadius, 0, 12) * Mathf.Clamp01(settings.nearStrength),
                        Mathf.Clamp(settings.farRadius, 0, 64), settings.farRadius > 0 ? Mathf.Clamp01(settings.farStrength) : 0,
                        Mathf.Clamp01(owner.Weight) * d.height / 1080f));
                    CoreUtils.SetKeyword(material, "_HIGH_QUALITY", settings.highQuality);
                    var camera = data.cameraData.renderer.cameraColorTargetHandle;
                    Blitter.BlitCameraTexture(cmd, camera, original);
                    Blitter.BlitCameraTexture(cmd, original, source, material, 0);
                    cmd.SetGlobalTexture("_JcRearSource", source.nameID);
                    Blitter.BlitCameraTexture(cmd, original, camera, material, 1);
                    context.ExecuteCommandBuffer(cmd);
                }
                finally { CommandBufferPool.Release(cmd); }
            }
            public void Dispose() { original?.Release(); source?.Release(); }
        }
    }
}
