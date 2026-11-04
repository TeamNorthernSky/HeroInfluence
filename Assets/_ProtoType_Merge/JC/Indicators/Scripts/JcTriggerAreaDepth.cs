using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JC.Indicators
{
    /// <summary>트리거 내부의 가림 깊이만 기록하며 카메라의 장면 깊이는 변경하지 않습니다.</summary>
    // JC | 2026-11-04 | 트리거와 도로·건물의 깊이를 비교해 가려진 부분의 불투명도를 따로 적용합니다.
    internal static class JcTriggerAreaDepth
    {
        // JC | 2026-11-04 | 테두리 자체 깊이와 지형 종류별 깊이를 분리해 장면 깊이를 오염시키지 않습니다.
        private sealed class Buffer { internal Camera camera; internal RenderTexture texture, occluders; }
        private static readonly List<JcTriggerAreaIndicator> controllers = new List<JcTriggerAreaIndicator>();
        private static readonly Dictionary<int, Buffer> buffers = new Dictionary<int, Buffer>();
        private static readonly List<int> staleCameras = new List<int>();
        private static MaterialPropertyBlock properties;
        private static readonly int textureId = Shader.PropertyToID("_JcTriggerBorderDepth");
        private static readonly int activeId = Shader.PropertyToID("_JcTriggerDepthActive");

        internal static void Register(JcTriggerAreaIndicator controller)
        {
            if (controllers.Contains(controller)) return;
            if (controllers.Count == 0) RenderPipelineManager.beginCameraRendering += RenderDepth;
            controllers.Add(controller);
        }
        internal static void Unregister(JcTriggerAreaIndicator controller)
        {
            controllers.Remove(controller);
            controllers.RemoveAll(c => !c);
            if (controllers.Count != 0) return;
            RenderPipelineManager.beginCameraRendering -= RenderDepth;
            Shader.SetGlobalFloat(activeId, 0);
            foreach (var buffer in buffers.Values) { Release(buffer.texture); Release(buffer.occluders); }
            buffers.Clear();
        }
        private static void RenderDepth(ScriptableRenderContext context, Camera camera)
        {
            controllers.RemoveAll(c => !c);
            if (controllers.Count == 0) { Unregister(null); return; }
            staleCameras.Clear();
            foreach (var pair in buffers) if (!pair.Value.camera) staleCameras.Add(pair.Key);
            foreach (int stale in staleCameras) { Release(buffers[stale].texture); Release(buffers[stale].occluders); buffers.Remove(stale); }
            if (properties == null) properties = new MaterialPropertyBlock();
            int width = Mathf.Max(1, camera.scaledPixelWidth), height = Mathf.Max(1, camera.scaledPixelHeight);
            int id = camera.GetInstanceID();
            if (!buffers.TryGetValue(id, out var buffer)) buffers.Add(id, buffer = new Buffer { camera = camera });
            if (!buffer.texture || buffer.texture.width != width || buffer.texture.height != height)
            {
                Release(buffer.texture);
                Release(buffer.occluders);
                var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGFloat) ? RenderTextureFormat.RGFloat : RenderTextureFormat.ARGBFloat;
                buffer.texture = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear) {
                    name = "JC Trigger Private Depth", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, antiAliasing = 1
                };
                buffer.texture.Create();
                buffer.occluders = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear) {
                    name = "JC Trigger Surface Depth", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, antiAliasing = 1
                };
                buffer.occluders.Create();
            }
            var command = CommandBufferPool.Get("JC trigger private depth");
            try
            {
                var view = camera.worldToCameraMatrix;
                var projection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
                command.SetGlobalFloat(activeId, 0);
                command.SetGlobalMatrix("_JcTriggerDepthVP", projection * view);
                command.SetGlobalMatrix("_JcTriggerDepthView", view);
                command.SetGlobalFloat("_JcTriggerDepthUvFlip", SystemInfo.graphicsUVStartsAtTop ? 1 : 0);
                command.SetRenderTarget(buffer.texture);
                command.SetViewport(new Rect(0, 0, width, height));
                command.ClearRenderTarget(false, true, new Color(65504, 65504, 65504, 65504));
                foreach (var controller in controllers) if (controller) controller.DrawPrivateDepth(command, camera, properties);
                command.SetGlobalTexture(textureId, buffer.texture);
                command.SetRenderTarget(buffer.occluders);
                command.ClearRenderTarget(false, true, new Color(65504, 65504, 65504, 65504));
                foreach (var controller in controllers) if (controller) controller.DrawOccluders(command, camera, properties);
                command.SetGlobalTexture("_JcTriggerOccluderDepth", buffer.occluders);
                command.SetGlobalFloat(activeId, 1);
                context.ExecuteCommandBuffer(command);
            }
            finally { CommandBufferPool.Release(command); }
        }
        private static void Release(RenderTexture texture)
        {
            if (!texture) return;
            texture.Release();
            if (Application.isPlaying) Object.Destroy(texture); else Object.DestroyImmediate(texture);
        }
    }
}
