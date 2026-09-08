using System.Collections.Generic;
using UnityEngine;

/// <summary>일반 건물만 대상으로 하는 Collider 없는 선분/삼각형 검사. 원본 메시별로 한 번만 읽는다.</summary>
public sealed class JcBuildingMeshOcclusion
{
    // DHScene_3의 일반 건물 프리팹 키. 이름이 비슷한 장식까지 포함하지 않는다.
    private static readonly HashSet<string> BuildingKeys = new HashSet<string>
    {
        "BGHouse001", "BGHouse002", "BGOffice001", "BGOffice002",
        "BGRowHouse001", "BGRowHouse002", "BGFactory001", "BGFactory002",
        "BGWarehouse_001", "BGWarehouse_002", "BGHigh001", "BGHigh002", "BGHigh003", "BGHigh004",
        "BGStore001", "BGStore002", "BGStore003", "BGStore004", "BGStore005"
    };

    private sealed class MeshData
    {
        public Vector3[] vertices;
        public int[] triangles;
    }

    private readonly Dictionary<Mesh, MeshData> meshes = new Dictionary<Mesh, MeshData>();
    private readonly Dictionary<DecorativeObjectPlacement, MeshFilter[]> parts = new Dictionary<DecorativeObjectPlacement, MeshFilter[]>();
    public int CachedMeshCount => meshes.Count;
    public static bool IsBuilding(DecorativeObjectPlacement item) => item != null && BuildingKeys.Contains(item.PrefabKey);
    public void Clear() { meshes.Clear(); parts.Clear(); }
    public void Remove(DecorativeObjectPlacement item) => parts.Remove(item);

    public bool IsOccluded(DecorativeObjectPlacement item, Vector3 camera, Vector3 party, JcBuildingSilhouetteSettings settings)
    {
        Vector3 center = party + settings.occlusionBoxOffset;
        if (settings.occlusionCenterPriority && Blocked(item, camera, center)) return true;
        Vector3 half = settings.occlusionBoxSize * 0.5f;
        int hits = 0;
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = center + Vector3.Scale(half, new Vector3((i & 1) == 0 ? -1 : 1,
                (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            if (Blocked(item, camera, point) && ++hits >= settings.occlusionRequiredCorners) return true;
            if (hits + 7 - i < settings.occlusionRequiredCorners) return false;
        }
        return false;
    }

    public static bool IntersectsSegment(Bounds bounds, Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        return length > 0.00001f && (bounds.Contains(from) ||
            (bounds.IntersectRay(new Ray(from, delta / length), out float distance) && distance <= length));
    }

    private bool Blocked(DecorativeObjectPlacement item, Vector3 from, Vector3 to)
    {
        // 중심만으로 후보를 거르지 않고 각 판정점으로 향하는 선분을 먼저 검사한다.
        if (!item.TryGetRenderBounds(out Bounds bounds) || !IntersectsSegment(bounds, from, to)) return false;
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
                else Debug.LogWarning($"[JC 건물 가림] {mesh.name}: Read/Write가 꺼져 있어 해당 부품은 Bounds 판정으로 대체합니다.", item);
                meshes.Add(mesh, data);
            }
            // 새 모델의 설정 누락 시 파티가 완전히 가려지는 문제를 피하고 한 번만 경고한다.
            if (data == null) return true;
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
