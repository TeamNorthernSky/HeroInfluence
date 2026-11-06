using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>중복 제거된 그리드 선분을 하나의 메시로 묶습니다. 교차점과 선 몸통은 겹치지 않습니다.</summary>
public sealed class JcMoveGridMesh : System.IDisposable
{
    // JC | 2026-11-06 | 탐사 이동 범위를 건물 입장 외곽선보다 먼저 합성하기 위한 기존 메시의 등록 목록입니다.
    private static readonly List<JcMoveGridMesh> instances = new List<JcMoveGridMesh>();
    internal static bool HasInstances => instances.Exists(item => item.root && item.root.activeInHierarchy);
    internal static void DrawAll(CommandBuffer command, Camera camera, Plane[] planes)
    {
        foreach (var item in instances)
        {
            var r = item.renderer;
            if (!r || !item.material || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy ||
                (camera.scene.IsValid() && camera.scene != r.gameObject.scene) ||
                (camera.cullingMask & (1 << r.gameObject.layer)) == 0 || !GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
            command.DrawRenderer(r, item.material, 0, 0);
        }
    }
    private readonly GameObject root;
    private readonly Mesh mesh;
    private readonly Material material;
    private readonly MeshRenderer renderer;
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector3> normals = new List<Vector3>();
    private readonly List<Vector2> uv = new List<Vector2>();
    private readonly List<Vector4> edges = new List<Vector4>();
    private readonly List<Vector2> modes = new List<Vector2>();
    private readonly List<int> indices = new List<int>();
    private readonly Dictionary<Vector3, int> junctions = new Dictionary<Vector3, int>();
    private float halfWidth;
    private Bounds groundBounds;
    private float curtainExtent;
    private float projectionExtent;

    public JcMoveGridMesh(Transform parent)
    {
        // Resources 참조로 플레이어 빌드의 셰이더 스트리핑도 방지합니다.
        var shader = Resources.Load<Shader>("JcMoveRangeGrid");
        root = new GameObject("Movement Range Grid", typeof(MeshFilter), typeof(MeshRenderer));
        root.transform.SetParent(parent, false);
        mesh = new Mesh { name = "Movement Range Grid", indexFormat = IndexFormat.UInt32 };
        mesh.MarkDynamic();
        root.GetComponent<MeshFilter>().sharedMesh = mesh;
        renderer = root.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (shader != null)
        {
            // JC | 2026-11-06 | 트리거보다 위, 입장 외곽선보다 아래의 전용 패스에서만 그려 이중 합성을 막습니다.
            material = new Material(shader) { name = "Movement Range Grid (Runtime)", renderQueue = JC.Indicators.JcIndicatorRenderOrder.MoveRangeGrid };
            material.SetShaderPassEnabled("UniversalForward", false);
            renderer.sharedMaterial = material;
        }
        else Debug.LogError("이동 그리드 셰이더 JcMoveRangeGrid를 찾을 수 없습니다.");
        Hide();
        instances.Add(this);
    }

    public void Begin(float width)
    {
        halfWidth = width * .5f;
        vertices.Clear(); normals.Clear(); uv.Clear(); edges.Clear(); modes.Clear(); indices.Clear(); junctions.Clear();
    }

    public void AddSegment(Vector3 from, Vector3 to)
    {
        Vector3 direction = (to - from).normalized;
        bool horizontal = Mathf.Abs(direction.x) > Mathf.Abs(direction.z);
        int forward = horizontal ? (direction.x > 0 ? 2 : 1) : (direction.z > 0 ? 8 : 4);
        int backward = horizontal ? (direction.x > 0 ? 1 : 2) : (direction.z > 0 ? 4 : 8);
        AddJunction(from, forward);
        AddJunction(to, backward);
        Vector3 start = from + direction * halfWidth;
        Vector3 end = to - direction * halfWidth;
        if (Vector3.Dot(end - start, direction) <= 0) return;
        Vector3 center = (start + end) * .5f;
        float length = Vector3.Distance(start, end) * .5f;
        AddQuad(center, horizontal ? length : halfWidth, horizontal ? halfWidth : length,
            horizontal ? new Vector4(0, 0, 1, 1) : new Vector4(1, 1, 0, 0));
        // 균일한 장벽은 선분당 사각형 하나면 충분합니다. 높이만 GPU에서 적용합니다.
        int first = vertices.Count;
        AddVertex(from, new Vector2(0, 0), Vector4.zero, 1);
        AddVertex(from, new Vector2(0, 1), Vector4.zero, 1);
        AddVertex(to, new Vector2(1, 1), Vector4.zero, 1);
        AddVertex(to, new Vector2(1, 0), Vector4.zero, 1);
        AddTriangles(first);
    }

    private void AddJunction(Vector3 point, int direction)
    {
        junctions.TryGetValue(point, out int directions);
        junctions[point] = directions | direction;
    }

    public void End()
    {
        foreach (var pair in junctions)
        {
            int mask = pair.Value;
            AddQuad(pair.Key, halfWidth, halfWidth, new Vector4(
                (mask & 1) == 0 ? 1 : 0, (mask & 2) == 0 ? 1 : 0,
                (mask & 4) == 0 ? 1 : 0, (mask & 8) == 0 ? 1 : 0));
        }
        mesh.Clear();
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(1, edges); mesh.SetUVs(2, modes);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();
        groundBounds = mesh.bounds;
        UpdateBounds();
        root.SetActive(material != null && vertices.Count > 0);
    }

    private void AddQuad(Vector3 center, float x, float z, Vector4 exposedEdges)
    {
        int first = vertices.Count;
        AddVertex(center + new Vector3(-x, 0, -z), new Vector2(0, 0), exposedEdges);
        AddVertex(center + new Vector3(-x, 0, z), new Vector2(0, 1), exposedEdges);
        AddVertex(center + new Vector3(x, 0, z), new Vector2(1, 1), exposedEdges);
        AddVertex(center + new Vector3(x, 0, -z), new Vector2(1, 0), exposedEdges);
        AddTriangles(first);
    }

    private void AddTriangles(int first)
    {
        indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
        indices.Add(first); indices.Add(first + 2); indices.Add(first + 3);
    }

    private void AddVertex(Vector3 world, Vector2 coord, Vector4 exposedEdges, float mode = 0)
    {
        vertices.Add(root.transform.InverseTransformPoint(world));
        normals.Add(root.transform.InverseTransformDirection(Vector3.up));
        uv.Add(coord); edges.Add(exposedEdges); modes.Add(new Vector2(mode, 0));
    }

    public void SetAppearance(Color color, float opacity, float intensity, float edgeSoftness = 0.15f)
    {
        if (material == null) return;
        color.a = 1;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Opacity", Mathf.Clamp01(opacity));
        material.SetFloat("_Intensity", Mathf.Max(0, intensity));
        material.SetFloat("_EdgeSoftness", Mathf.Clamp(edgeSoftness, 0f, 0.5f));
    }

    public void SetViewCompensation(JcMoveRangeGridProfile profile)
    {
        if (material == null) return;
        material.SetFloat("_UniformCurtain", profile.uniformCurtainSize ? 1 : 0);
        material.SetFloat("_CurtainScreenHeight", profile.curtainScreenHeight);
        material.SetFloat("_CurtainMaxHeight", profile.curtainMaxHeight);
        material.SetFloat("_AdaptDayNight", profile.adaptToDayNight ? 1 : 0);
        material.SetFloat("_NightIntensityMultiplier", profile.nightIntensityMultiplier);
        projectionExtent = profile.uniformCurtainSize ? profile.curtainMaxHeight : 0;
    }

    public void SetCurtain(float height, float opacity, float intensity, float softness)
    {
        if (material == null) return;
        height = Mathf.Max(0, height);
        material.SetFloat("_CurtainHeight", height);
        material.SetFloat("_CurtainOpacity", Mathf.Clamp01(opacity));
        material.SetFloat("_CurtainIntensity", Mathf.Max(0, intensity));
        material.SetFloat("_CurtainSoftness", Mathf.Clamp(softness, 0.5f, 6f));
        float extent = height > 0 ? Mathf.Max(height, projectionExtent) : 0;
        if (curtainExtent != extent) { curtainExtent = extent; UpdateBounds(); }
    }

    private void UpdateBounds()
    {
        // GPU에서 위로 펼친 장막이 카메라 가장자리에서 잘리지 않도록 로컬 AABB를 확장합니다.
        Vector3 up = root.transform.InverseTransformVector(Vector3.up * curtainExtent);
        Bounds bounds = groundBounds;
        bounds.center += up * .5f;
        bounds.size += new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
        mesh.bounds = bounds;
    }

    public void Hide() { if (root != null) root.SetActive(false); }
    public void Dispose()
    {
        // JC | 2026-11-06 | 씬 종료·숨김 후 이전 이동 격자가 다음 카메라에 남지 않게 등록을 해제합니다.
        instances.Remove(this);
        Destroy(root); Destroy(mesh); Destroy(material);
    }
    private static void Destroy(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj);
    }
}
