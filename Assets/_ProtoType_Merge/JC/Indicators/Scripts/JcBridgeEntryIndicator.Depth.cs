using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JC.Indicators
{
    public sealed partial class JcBridgeEntryIndicator
    {
        // JC | 2026-11-12 | 기둥 위 합성은 보존하고 차단기만 별도 깊이로 가립니다. 기존 트리거 패스 안에서
        // 화면에 보이는 활성 입구의 차단기만 제출하며 카메라 깊이/게이트 원본 재질은 변경하지 않습니다.
        private struct GateMesh { internal Renderer renderer; internal int slots; internal bool returning; }
        private struct GateDraw { internal GateMesh mesh; internal Material material; internal int pass; }
        private readonly List<GateMesh> gateMeshes = new List<GateMesh>();
        private readonly List<MeshRenderer> gateScan = new List<MeshRenderer>();
        private GateRuntimeController cachedDepthSource, cachedDepthDestination;
        private static readonly List<JcBridgeEntryIndicator> depthInstances = new List<JcBridgeEntryIndicator>();
        private static readonly List<GateDraw> gateDraws = new List<GateDraw>();
        private static readonly HashSet<Renderer> submittedGates = new HashSet<Renderer>();
        private static RenderTexture gateDepth;
        internal static bool HasDepthInstances => depthInstances.Count > 0;

        private void CacheGateDepth()
        {
            if (cachedDepthSource == sourceGate && cachedDepthDestination == destinationGate) return;
            cachedDepthSource = sourceGate; cachedDepthDestination = destinationGate; gateMeshes.Clear();
            Collect(sourceGate, false); Collect(destinationGate, true);
            void Collect(GateRuntimeController gate, bool returning)
            {
                if (!gate) return;
                gateScan.Clear(); gate.GetComponentsInChildren(true, gateScan);
                foreach (var r in gateScan)
                    if (r.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh)
                        gateMeshes.Add(new GateMesh { renderer = r, slots = f.sharedMesh.subMeshCount, returning = returning });
            }
        }

        internal static void DrawGateDepth(CommandBuffer cmd, Camera camera, Plane[] planes,
            Matrix4x4 view, Matrix4x4 projection, int width, int height)
        {
            cmd.SetGlobalFloat("_JcBridgeGateDepthActive", 0);
            if (depthInstances.Count == 0) return;
            gateDraws.Clear(); submittedGates.Clear();
            foreach (var instance in depthInstances)
            {
                if (!instance || !instance.overlayBorderMaterial ||
                    (camera.scene.IsValid() && camera.scene != instance.gameObject.scene)) continue;
                bool sourceVisible = false, destinationVisible = false;
                // JC | 2026-11-12 | 화면에 있는 실제 표시 셀만 검사합니다. 셀/게이트 목록과 공간은 재사용합니다.
                for (int i = 0; i < instance.views.Count; i++)
                {
                    var cell = instance.views[i];
                    if (!cell.root.activeInHierarchy || (camera.cullingMask & (1 << cell.root.layer)) == 0) continue;
                    var bounds = new Bounds(cell.root.transform.position, new Vector3(instance.configuredSize, .5f, instance.configuredSize));
                    if (!GeometryUtility.TestPlanesAABB(planes, bounds)) continue;
                    if (i < instance.cells.Count) sourceVisible = true; else destinationVisible = true;
                }
                if (!sourceVisible && !destinationVisible) continue;
                int pass = instance.overlayBorderMaterial.FindPass("BridgeGateDepth");
                if (pass < 0) continue;
                foreach (var item in instance.gateMeshes)
                {
                    if (item.returning ? !destinationVisible : !sourceVisible) continue;
                    var r = item.renderer;
                    if (!r || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy ||
                        (camera.cullingMask & (1 << r.gameObject.layer)) == 0 || !GeometryUtility.TestPlanesAABB(planes, r.bounds) ||
                        !submittedGates.Add(r)) continue;
                    gateDraws.Add(new GateDraw { mesh = item, material = instance.overlayBorderMaterial, pass = pass });
                }
            }
            // JC | 2026-11-12 | 대상이 없으면 RT 생성·초기화·드로우를 모두 생략합니다. 새 카메라에서는 직전 결과를 쓰지 않습니다.
            if (gateDraws.Count == 0) return;
            if (!gateDepth || gateDepth.width != width || gateDepth.height != height)
            {
                ReleaseDepthTexture();
                gateDepth = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear) {
                    name = "JC Visible Bridge Gate Depth", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, antiAliasing = 1 };
                gateDepth.Create();
            }
            cmd.SetGlobalMatrix("_JcBridgeDepthVP", projection * view);
            cmd.SetGlobalMatrix("_JcBridgeDepthView", view);
            cmd.SetGlobalFloat("_JcBridgeDepthUvFlip", SystemInfo.graphicsUVStartsAtTop ? 1 : 0);
            cmd.SetRenderTarget(gateDepth); cmd.SetViewport(new Rect(0, 0, width, height));
            cmd.ClearRenderTarget(false, true, new Color(65504, 0, 0, 0));
            foreach (var item in gateDraws)
                for (int slot = 0; slot < item.mesh.slots; slot++) cmd.DrawRenderer(item.mesh.renderer, item.material, slot, item.pass);
            cmd.SetGlobalTexture("_JcBridgeGateDepth", gateDepth);
            cmd.SetGlobalFloat("_JcBridgeGateDepthActive", 1);
            gateDraws.Clear(); submittedGates.Clear();
        }

        private void ReleaseGateDepth()
        {
            // JC | 2026-11-12 | 마지막 다리가 해제되면 공유 RT도 정리하여 씬 전환 뒤 메모리를 남기지 않습니다.
            depthInstances.Remove(this); gateMeshes.Clear(); gateScan.Clear();
            cachedDepthSource = cachedDepthDestination = null;
            if (depthInstances.Count != 0) return;
            ReleaseDepthTexture(); gateDraws.Clear(); submittedGates.Clear();
            Shader.SetGlobalFloat("_JcBridgeGateDepthActive", 0);
        }
        private static void ReleaseDepthTexture()
        {
            if (!gateDepth) return;
            gateDepth.Release();
            if (Application.isPlaying) Destroy(gateDepth); else DestroyImmediate(gateDepth);
            gateDepth = null;
        }
    }
}
