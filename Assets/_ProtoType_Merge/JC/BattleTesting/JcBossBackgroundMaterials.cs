using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace JC.BattleTesting
{
    public sealed partial class JcBossBackgroundMaterials : MonoBehaviour
    {
        public Material GlassProfile { get; private set; }
        public Material GlassDraft { get; private set; }
        readonly List<Renderer> panes = new List<Renderer>();
        // JC | 2026-11-05 | 유리 메시/카메라 설정은 현재 보스전에만 적용하고 종료 시 원래 참조로 복원합니다.
        readonly Dictionary<MeshFilter, Mesh> originalMeshes = new Dictionary<MeshFilter, Mesh>();
        readonly Dictionary<MeshFilter, Mesh> volumeMeshes = new Dictionary<MeshFilter, Mesh>();
        UniversalAdditionalCameraData cameraData;
        CameraOverrideOption originalColor, originalDepth;
        float appliedThickness = -1;
        Transform stageRoot;
        // JC | 2026-11-05 | 고해상도 투과색 복사는 현재 유리를 사용하는 카메라에만 요청합니다.
        static readonly HashSet<JcBossBackgroundMaterials> instances = new HashSet<JcBossBackgroundMaterials>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInstances()
        {
            instances.Clear(); UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= ResetCapture;
            Shader.SetGlobalFloat("_JcBossGlassColorReady", 0);
        }
        static void ResetCapture(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera) => Shader.SetGlobalFloat("_JcBossGlassColorReady", 0);
        public static bool UsesRefraction(Camera camera)
        {
            foreach (var item in instances)
                if (item && item.isActiveAndEnabled && item.cameraData && item.cameraData.gameObject == camera.gameObject) return true;
            return false;
        }

        public void Initialize(GameObject stage, Material profile, Camera camera = null, JcBossSurfaceProfile surfaces = null)
        {
            stageRoot = stage.transform;
            GlassProfile = profile;
            GlassDraft = new Material(profile) { hideFlags = HideFlags.DontSave, name = "보스전 유리 · 조정 중" };
            foreach (var pane in stage.GetComponentsInChildren<Renderer>(true))
            {
                if (pane.sharedMaterial != profile) continue;
                panes.Add(pane);
                pane.sharedMaterial = GlassDraft;
                var filter = pane.GetComponent<MeshFilter>();
                if (filter && filter.sharedMesh) originalMeshes.Add(filter, filter.sharedMesh);
            }
            // JC | 2026-11-05 | 굴절에 필요한 색/깊이 복사는 보스 카메라에만 요청하며 공용 URP 에셋은 수정하지 않습니다.
            if (camera && camera.TryGetComponent(out cameraData))
            {
                originalColor = cameraData.requiresColorOption; originalDepth = cameraData.requiresDepthOption;
                cameraData.requiresColorOption = CameraOverrideOption.On; cameraData.requiresDepthOption = CameraOverrideOption.On;
            }
            InitializeSurfaces(surfaces);
            ApplyGlass();
            if (instances.Count == 0) UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += ResetCapture;
            instances.Add(this);
        }

        public void ApplyGlass()
        {
            if (!GlassDraft) return;
            float thickness = Mathf.Clamp(GlassDraft.GetFloat("_Thickness"), .005f, .3f);
            GlassDraft.SetFloat("_Thickness", thickness);
            if (Mathf.Approximately(thickness, appliedThickness)) return;
            appliedThickness = thickness;
            foreach (var entry in originalMeshes)
            {
                if (!entry.Key) continue;
                var mesh = BuildGlassVolume(entry.Value.bounds, entry.Key.transform, thickness);
                mesh.hideFlags = HideFlags.DontSave;
                if (volumeMeshes.TryGetValue(entry.Key, out var old)) Release(old);
                volumeMeshes[entry.Key] = mesh; entry.Key.sharedMesh = mesh;
            }
        }

        // JC | 2026-11-05 | 원본 창의 중심·폭·높이는 보존하고 가장 얇은 축에만 두께를 부여합니다. 각 면은 바깥쪽 법선을 가집니다.
        public static Mesh BuildGlassVolume(Bounds source, Transform transform, float thickness)
        {
            var extent = source.extents;
            int axis = extent.x < extent.y && extent.x < extent.z ? 0 : extent.y < extent.z ? 1 : 2;
            var direction = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            extent[axis] = thickness / (2 * Mathf.Max(.0001f, transform.TransformVector(direction).magnitude));
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            foreach (var normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                var u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up;
                var v = Vector3.Cross(normal, u);
                int start = vertices.Count;
                foreach (var corner in new[] { -u-v, u-v, u+v, -u+v })
                {
                    vertices.Add(source.center + Vector3.Scale(normal + corner, extent)); normals.Add(normal);
                }
                triangles.AddRange(new[] { start, start+1, start+2, start, start+2, start+3 });
            }
            var result = new Mesh { name = "보스 유리 · 얇은 입체" };
            result.SetVertices(vertices); result.SetNormals(normals); result.SetTriangles(triangles, 0); result.RecalculateBounds();
            return result;
        }

        void OnDestroy()
        {
            instances.Remove(this);
            if (instances.Count == 0)
            {
                UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= ResetCapture;
                Shader.SetGlobalFloat("_JcBossGlassColorReady", 0);
            }
            if (Environment) Environment.SuspendMaterialOverrides();
            foreach (var pane in panes)
                if (pane && pane.sharedMaterial == GlassDraft) pane.sharedMaterial = GlassProfile;
            foreach (var entry in originalMeshes) if (entry.Key) entry.Key.sharedMesh = entry.Value;
            foreach (var mesh in volumeMeshes.Values) Release(mesh);
            if (cameraData) { cameraData.requiresColorOption = originalColor; cameraData.requiresDepthOption = originalDepth; }
            RestoreSurfaces(); Release(SurfaceDraft); Release(GlassDraft);
        }
        static void Release(Object value) { if (!value) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
