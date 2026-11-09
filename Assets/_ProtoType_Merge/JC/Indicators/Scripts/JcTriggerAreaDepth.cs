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
        private sealed class Buffer { internal Camera camera; internal RenderTexture texture, occluders, owners; }
        // JC | 2026-11-05 | 깊이가 같은 윤곽선은 DH 우선순위, 소유자, 부위, 렌더러 순으로 한 번만 선택합니다.
        internal sealed class Border
        {
            internal readonly MeshRenderer renderer;
            internal readonly Material material;
            internal readonly int ownerId, part, rendererId, regionId;
            internal int priority;
            internal Border(MeshRenderer renderer, Material material, int ownerId, int part, int regionId)
            {
                this.renderer = renderer; this.material = material; this.ownerId = ownerId; this.part = part;
                rendererId = renderer ? renderer.GetInstanceID() : 0;
                // JC | 2026-11-06 | 같은 영역의 본체·보조선은 픽셀 소유권을 공유하고 실제 앞뒤 가림은 깊이로 판정합니다.
                this.regionId = regionId;
            }
        }
        private static readonly List<Border> borders = new List<Border>();
        private static readonly List<JcTriggerAreaIndicator> controllers = new List<JcTriggerAreaIndicator>();
        private static readonly Dictionary<int, Buffer> buffers = new Dictionary<int, Buffer>();
        private static readonly List<int> staleCameras = new List<int>();
        private static MaterialPropertyBlock properties;
        private static readonly int textureId = Shader.PropertyToID("_JcTriggerBorderDepth");
        private static readonly int activeId = Shader.PropertyToID("_JcTriggerDepthActive");
        private static readonly int ownersId = Shader.PropertyToID("_JcTriggerBorderOwners");
        private static readonly int orderId = Shader.PropertyToID("_TriggerBorderOrder");

        internal static void Register(JcTriggerAreaIndicator controller)
        {
            if (controllers.Contains(controller)) return;
            if (controllers.Count == 0) RenderPipelineManager.beginCameraRendering += PrepareBorders;
            controllers.Add(controller);
        }
        internal static void Unregister(JcTriggerAreaIndicator controller)
        {
            controllers.Remove(controller);
            controllers.RemoveAll(c => !c);
            if (controllers.Count != 0) return;
            RenderPipelineManager.beginCameraRendering -= PrepareBorders;
            Shader.SetGlobalFloat(activeId, 0);
            foreach (var buffer in buffers.Values) { Release(buffer.texture); Release(buffer.occluders); Release(buffer.owners); }
            buffers.Clear();
        }
        // JC | 2026-11-05 | URP가 렌더러 속성을 수집하기 전에 표시 순서만 전달합니다.
        // 이 단계는 카메라 위치/마스크를 읽지 않으며 진동 콜백의 등록 순서와 무관합니다.
        private static void PrepareBorders(ScriptableRenderContext context, Camera camera)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            borders.Clear();
            try
            {
                foreach (var controller in controllers) if (controller) controller.CollectBorders(borders, camera);
                borders.Sort(CompareBorders);
                // JC | 2026-11-06 | 부품마다 승자를 고르면 MSAA 경계에서 서로의 일부 샘플을 지워 그리드가 비칩니다.
                // 서로 다른 영역끼리만 고정 우선순위를 적용하며 같은 영역의 세 부품은 한 소유자로 묶습니다.
                int regionOrder = 0;
                for (int i = 0; i < borders.Count; i++)
                {
                    if (i == 0 || borders[i].regionId != borders[i - 1].regionId) regionOrder++;
                    var renderer = borders[i].renderer;
                    properties.Clear(); renderer.GetPropertyBlock(properties);
                    properties.SetFloat(orderId, regionOrder);
                    renderer.SetPropertyBlock(properties);
                }
            }
            finally { borders.Clear(); }
        }
        // JC | 2026-11-05 | 진동 콜백보다 먼저 실행되던 beginCameraRendering 대신 URP의 확정된 카메라 행렬을 받습니다.
        // 기존 가림/겹침 마스크 생성은 유지하고 렌더 시점과 카메라별 전역값 수명만 전용 패스가 관리합니다.
        internal static bool HasControllers => controllers.Exists(controller => controller && controller.isActiveAndEnabled);
        // JC | 2026-11-06 | 깊이 마스크를 준비한 같은 카메라에서 트리거 색상을 먼저 합성합니다.
        internal static void DrawColors(CommandBuffer command, Camera camera, Plane[] planes)
        {
            for (int part = 0; part < 4; part++)
                foreach (var controller in controllers) if (controller) controller.DrawColors(command, camera, planes, part);
        }
        internal static void RenderDepth(CommandBuffer command, Camera camera, Matrix4x4 view,
            Matrix4x4 projection, int width, int height)
        {
            controllers.RemoveAll(c => !c);
            if (controllers.Count == 0) { Unregister(null); return; }
            staleCameras.Clear();
            foreach (var pair in buffers) if (!pair.Value.camera) staleCameras.Add(pair.Key);
            foreach (int stale in staleCameras) { Release(buffers[stale].texture); Release(buffers[stale].occluders); Release(buffers[stale].owners); buffers.Remove(stale); }
            if (properties == null) properties = new MaterialPropertyBlock();
            width = Mathf.Max(1, width); height = Mathf.Max(1, height);
            int id = camera.GetInstanceID();
            if (!buffers.TryGetValue(id, out var buffer)) buffers.Add(id, buffer = new Buffer { camera = camera });
            if (!buffer.texture || buffer.texture.width != width || buffer.texture.height != height)
            {
                Release(buffer.texture);
                Release(buffer.occluders);
                Release(buffer.owners);
                var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGFloat) ? RenderTextureFormat.RGFloat : RenderTextureFormat.ARGBFloat;
                buffer.texture = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear) {
                    name = "JC Trigger Private Depth", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, antiAliasing = 1
                };
                buffer.texture.Create();
                // JC | 2026-11-05 | 동일 깊이에서 선택할 표시순서의 최솟값을 별도 버퍼에 저장합니다.
                buffer.owners = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear) {
                    name = "JC Trigger Border Owners", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, antiAliasing = 1
                };
                buffer.owners.Create();
                buffer.occluders = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear) {
                    name = "JC Trigger Surface Depth", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, antiAliasing = 1
                };
                buffer.occluders.Create();
            }
            try
            {
                borders.Clear();
                foreach (var controller in controllers) if (controller) controller.CollectBorders(borders, camera);
                command.SetGlobalFloat(activeId, 0);
                command.SetGlobalMatrix("_JcTriggerDepthVP", projection * view);
                command.SetGlobalMatrix("_JcTriggerDepthView", view);
                command.SetGlobalFloat("_JcTriggerDepthUvFlip", SystemInfo.graphicsUVStartsAtTop ? 1 : 0);
                command.SetRenderTarget(buffer.texture);
                command.SetViewport(new Rect(0, 0, width, height));
                command.ClearRenderTarget(false, true, new Color(65504, 65504, 65504, 65504));
                DrawBorders(command, "TriggerAreaDepth");
                command.SetGlobalTexture(textureId, buffer.texture);
                // JC | 2026-11-05 | 먼저 구한 최근접 깊이에 해당하는 선 중 고정 표시순서가 앞선 선을 픽셀별로 선택합니다.
                command.SetRenderTarget(buffer.owners);
                command.ClearRenderTarget(false, true, new Color(float.MaxValue, float.MaxValue, 0, 0));
                DrawBorders(command, "TriggerAreaOwner");
                command.SetGlobalTexture(ownersId, buffer.owners);
                command.SetRenderTarget(buffer.occluders);
                command.ClearRenderTarget(false, true, new Color(65504, 65504, 65504, 65504));
                foreach (var controller in controllers) if (controller) controller.DrawOccluders(command, camera, properties);
                command.SetGlobalTexture("_JcTriggerOccluderDepth", buffer.occluders);
                command.SetGlobalFloat(activeId, 1);
            }
            finally { borders.Clear(); }
        }
        private static int CompareBorders(Border a, Border b)
        {
            int result = b.priority.CompareTo(a.priority);
            if (result == 0) result = a.ownerId.CompareTo(b.ownerId);
            // JC | 2026-11-06 | 같은 주체가 여러 색상 영역을 가질 때도 영역별 겹침 우선순위는 구분합니다.
            if (result == 0) result = a.regionId.CompareTo(b.regionId);
            if (result == 0) result = a.part.CompareTo(b.part);
            if (result == 0) result = a.rendererId.CompareTo(b.rendererId);
            return result;
        }
        private static void DrawBorders(CommandBuffer command, string passName)
        {
            foreach (var border in borders)
            {
                int pass = border.material.FindPass(passName);
                var mesh = border.renderer.GetComponent<MeshFilter>().sharedMesh;
                if (pass < 0 || !mesh) continue;
                properties.Clear(); border.renderer.GetPropertyBlock(properties);
                command.DrawMesh(mesh, border.renderer.localToWorldMatrix, border.material, 0, pass, properties);
            }
        }
        private static void Release(RenderTexture texture)
        {
            if (!texture) return;
            texture.Release();
            if (Application.isPlaying) Object.Destroy(texture); else Object.DestroyImmediate(texture);
        }
    }
}
