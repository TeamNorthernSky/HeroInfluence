using System.Collections.Generic;
using UnityEngine;

/// <summary>등록된 가림 대상의 실제 메시와 카메라→파티 단일 선분을 검사한다. 원본 메시별로 한 번만 읽는다.</summary>
public sealed class JcBuildingMeshOcclusion
{
    private sealed class MeshData
    {
        public Vector3[] vertices;
        public int[] triangles;
    }

    private readonly Dictionary<Mesh, MeshData> meshes = new Dictionary<Mesh, MeshData>();
    private readonly Dictionary<Component, MeshFilter[]> parts = new Dictionary<Component, MeshFilter[]>();
    public int CachedMeshCount => meshes.Count;
    public void Clear() { meshes.Clear(); parts.Clear(); }
    public void Remove(Component item) => parts.Remove(item);

    public static bool IntersectsSegment(Bounds bounds, Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        return length > 0.00001f && (bounds.Contains(from) ||
            (bounds.IntersectRay(new Ray(from, delta / length), out float distance) && distance <= length));
    }

    public bool IsOccluded(Component item, Vector3 from, Vector3 to)
    {
        if (item == null || !item.gameObject.activeInHierarchy) return false;
        if (!parts.TryGetValue(item, out var filters))
            parts.Add(item, filters = item.GetComponentsInChildren<MeshFilter>(true));
        foreach (var filter in filters)
        {
            if (filter == null || !filter.gameObject.activeInHierarchy) continue;
            var renderer = filter.GetComponent<MeshRenderer>();
            Mesh mesh = filter.sharedMesh;
            if (renderer == null || !renderer.enabled || mesh == null || !IntersectsSegment(renderer.bounds, from, to)) continue;
            if (!meshes.TryGetValue(mesh, out var data))
            {
                data = null;
                if (mesh.isReadable) data = new MeshData { vertices = mesh.vertices, triangles = mesh.triangles };
                else Debug.LogWarning($"[건물 가림] {mesh.name}: 실제 표면을 검사하려면 모델 Import Settings의 Read/Write를 켜야 합니다. 이 메시의 가림 판정은 생략합니다.", item);
                meshes.Add(mesh, data);
            }
            // 읽기 실패를 가림으로 간주하면 산의 빈 Bounds 영역도 투명해지는 문제가 재발한다.
            if (data == null) continue;
            Vector3 localFrom = filter.transform.InverseTransformPoint(from);
            Vector3 localDelta = filter.transform.InverseTransformPoint(to) - localFrom;
            for (int i = 0; i + 2 < data.triangles.Length; i += 3)
                if (IntersectsTriangle(localFrom, localDelta, data.vertices[data.triangles[i]],
                    data.vertices[data.triangles[i + 1]], data.vertices[data.triangles[i + 2]])) return true;
        }
        return false;
    }

    // 방향을 정규화하지 않아 비균일 스케일에서도 t=0..1이 원래 선분을 유지한다. 양면 검사.
    public static bool IntersectsTriangle(Vector3 origin, Vector3 delta, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 e1 = b - a, e2 = c - a;
        Vector3 p = Vector3.Cross(delta, e2);
        float determinant = Vector3.Dot(e1, p);
        if (Mathf.Abs(determinant) < 1e-8f) return false;
        float inverse = 1f / determinant;
        Vector3 offset = origin - a;
        float u = Vector3.Dot(offset, p) * inverse;
        if (u < -1e-6f || u > 1f + 1e-6f) return false;
        Vector3 q = Vector3.Cross(offset, e1);
        float v = Vector3.Dot(delta, q) * inverse;
        if (v < -1e-6f || u + v > 1f + 1e-6f) return false;
        float t = Vector3.Dot(e2, q) * inverse;
        return t > 0.00001f && t < 0.99999f;
    }
}
