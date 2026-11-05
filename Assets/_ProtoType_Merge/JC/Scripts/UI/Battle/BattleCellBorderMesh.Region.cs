using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// JC | 2026-11-04 | 이웃한 셀은 하나의 영역으로 표시하고 단일 전투 셀은 기존 테두리 경로를 사용합니다.
public static partial class BattleCellBorderMesh
{
    /// <summary>셀 합집합의 외곽과 구멍입니다. 키는 기준 셀에 대한 상대 좌표, 값은 지면 높이입니다.</summary>
    public sealed class Region
    {
        public readonly Dictionary<Vector2Int, float> Cells = new Dictionary<Vector2Int, float>();
        public readonly List<List<Vector3>> Loops = new List<List<Vector3>>();
        public float CellSize;
        public int BoundaryEdges { get; private set; }

        private struct Edge { public Vector2Int a, b; public float y; }
        public void Trace()
        {
            Loops.Clear();
            var edges = new List<Edge>();
            var outgoing = new Dictionary<Vector2Int, List<int>>();
            foreach (var pair in Cells)
            {
                var p = pair.Key;
                Add(p, p + Vector2Int.right, p + Vector2Int.down, pair.Value);
                Add(p + Vector2Int.right, p + Vector2Int.one, p + Vector2Int.right, pair.Value);
                Add(p + Vector2Int.one, p + Vector2Int.up, p + Vector2Int.up, pair.Value);
                Add(p + Vector2Int.up, p, p + Vector2Int.left, pair.Value);
            }
            BoundaryEdges = edges.Count;
            var used = new bool[edges.Count];
            for (int start = 0; start < edges.Count; start++)
            {
                if (used[start]) continue;
                var chain = new List<Edge>();
                int current = start;
                while (true)
                {
                    if (used[current]) throw new InvalidOperationException("영역 외곽선이 닫히지 않았습니다.");
                    var edge = edges[current]; used[current] = true; chain.Add(edge);
                    if (edge.b == edges[start].a) break;
                    int best = -1, bestRank = -1;
                    var direction = edge.b - edge.a;
                    foreach (int candidate in outgoing[edge.b])
                    {
                        if (used[candidate]) continue;
                        var next = edges[candidate].b - edge.b;
                        int cross = direction.x * next.y - direction.y * next.x;
                        int rank = cross > 0 ? 3 : direction.x * next.x + direction.y * next.y > 0 ? 2 : cross < 0 ? 1 : 0;
                        // 좌회전을 우선해 대각선으로만 맞닿은 셀을 별도 폐곡선으로 유지합니다.
                        if (rank > bestRank) { bestRank = rank; best = candidate; }
                    }
                    if (best < 0) throw new InvalidOperationException("영역 외곽선에 열린 변이 있습니다.");
                    current = best;
                }
                var loop = new List<Vector3>();
                for (int i = 0; i < chain.Count; i++)
                {
                    var edge = chain[i];
                    float ground = Mathf.Max(edge.y, chain[(i + chain.Count - 1) % chain.Count].y);
                    loop.Add(new Vector3((edge.a.x - .5f) * CellSize, ground, (edge.a.y - .5f) * CellSize));
                }
                Loops.Add(loop);
            }
            void Add(Vector2Int a, Vector2Int b, Vector2Int neighbour, float y)
            {
                if (Cells.ContainsKey(neighbour)) return;
                if (!outgoing.TryGetValue(a, out var list)) outgoing[a] = list = new List<int>();
                list.Add(edges.Count); edges.Add(new Edge { a = a, b = b, y = y });
            }
        }
    }

    /// <summary>전투 셀과 같은 윗면·밑면·양쪽 벽을 임의의 셀 영역 경계에 생성합니다.</summary>
    public static void BuildRegion(Mesh mesh, Region region, float inset, float radius, float width, float depth, int segments, bool fill)
    {
        inset = Mathf.Max(0, inset); width = Mathf.Max(.001f, width); depth = Mathf.Max(.001f, depth);
        radius = Mathf.Clamp(radius, 0, region.CellSize * .4f);
        segments = Mathf.Clamp(segments, 2, 32);
        var data = new RegionMeshData(region.CellSize);
        var outside = OffsetLoops(region, inset, radius, segments);
        if (fill) FillRegion(data, region, outside);
        else
        {
            var inside = OffsetLoops(region, inset + width, radius, segments);
            var up = Vector3.up * depth;
            for (int k = 0; k < outside.Count; k++)
                for (int i = 0; i < outside[k].Count; i++)
                {
                    int j = (i + 1) % outside[k].Count;
                    var a = outside[k][i]; var b = outside[k][j];
                    var c = inside[k][i]; var d = inside[k][j];
                    var direction = b - a;
                    var left = new Vector3(-direction.z, 0, direction.x);
                    data.Quad(a + up, b + up, d + up, c + up, Vector3.up);
                    data.Quad(a, c, d, b, Vector3.down);
                    data.Quad(a, b, b + up, a + up, -left);
                    data.Quad(c + up, d + up, d, c, left);
                }
        }
        data.Apply(mesh);
    }

    private static List<List<Vector3>> OffsetLoops(Region region, float inset, float radius, int segments)
    {
        var result = new List<List<Vector3>>();
        foreach (var loop in region.Loops)
        {
            var points = new List<Vector3>();
            for (int i = 0; i < loop.Count; i++)
            {
                var p = loop[i]; var previous = loop[(i + loop.Count - 1) % loop.Count]; var next = loop[(i + 1) % loop.Count];
                var incoming = p - previous; var outgoing = next - p;
                incoming.y = outgoing.y = 0; incoming.Normalize(); outgoing.Normalize();
                var n1 = new Vector3(-incoming.z, 0, incoming.x);
                var n2 = new Vector3(-outgoing.z, 0, outgoing.x);
                var corner = p + (n1 + n2) * (inset / Mathf.Max(.01f, 1 + Vector3.Dot(n1, n2)));
                float turn = incoming.x * outgoing.z - incoming.z * outgoing.x;
                // JC | 2026-11-04 | 오목한 모서리도 같은 원래 반지름으로 둥글립니다. 오프셋 반경을 반대 방향으로 보정해 선 폭을 유지합니다.
                if (Mathf.Abs(turn) > .5f)
                {
                    float r = Mathf.Max(0, radius - turn * inset);
                    var center = corner - incoming * r + outgoing * r;
                    for (int n = 0; n <= segments; n++)
                    {
                        float angle = n * Mathf.PI * .5f / segments;
                        var point = center - outgoing * (r * Mathf.Cos(angle)) + incoming * (r * Mathf.Sin(angle));
                        // 높이 차이 연결부의 지면 관통을 막기 위해 높은 쪽을 기준으로 합니다.
                        point.y = p.y;
                        points.Add(point);
                    }
                }
                else points.Add(corner);
            }
            result.Add(points);
        }
        return result;
    }

    private struct Crossing { public Vector3 a, b; public float x; }
    // 짝수·홀수 교차 규칙으로 구멍과 분리 영역을 함께 처리하고 셀 지면 높이별로 나눕니다.
    private static void FillRegion(RegionMeshData data, Region region, List<List<Vector3>> loops)
    {
        var levels = new SortedSet<float>();
        foreach (var loop in loops) foreach (var p in loop) levels.Add(p.z);
        foreach (var pair in region.Cells)
        {
            levels.Add((pair.Key.y - .5f) * region.CellSize);
            levels.Add((pair.Key.y + .5f) * region.CellSize);
        }
        var zs = new List<float>(levels);
        var crossings = new List<Crossing>();
        for (int band = 0; band + 1 < zs.Count; band++)
        {
            float z0 = zs[band], z1 = zs[band + 1], middle = (z0 + z1) * .5f;
            if (z1 - z0 < .00001f) continue;
            crossings.Clear();
            foreach (var loop in loops)
                for (int i = 0; i < loop.Count; i++)
                {
                    var a = loop[i]; var b = loop[(i + 1) % loop.Count];
                    if (middle <= Mathf.Min(a.z, b.z) || middle >= Mathf.Max(a.z, b.z)) continue;
                    crossings.Add(new Crossing { a = a, b = b, x = At(a, b, middle) });
                }
            crossings.Sort((a, b) => a.x.CompareTo(b.x));
            if (crossings.Count % 2 != 0) throw new InvalidOperationException("채움 외곽선 교점이 짝수가 아닙니다.");
            foreach (var cell in region.Cells)
            {
                float cellZ = cell.Key.y * region.CellSize;
                if (middle <= cellZ - region.CellSize * .5f || middle >= cellZ + region.CellSize * .5f) continue;
                float min = (cell.Key.x - .5f) * region.CellSize, max = min + region.CellSize;
                for (int i = 0; i + 1 < crossings.Count; i += 2)
                {
                    var l = crossings[i]; var r = crossings[i + 1];
                    if (r.x <= min || l.x >= max) continue;
                    float l0 = Mathf.Clamp(At(l.a, l.b, z0), min, max), l1 = Mathf.Clamp(At(l.a, l.b, z1), min, max);
                    float r0 = Mathf.Clamp(At(r.a, r.b, z0), min, max), r1 = Mathf.Clamp(At(r.a, r.b, z1), min, max);
                    data.Quad(new Vector3(l0, cell.Value, z0), new Vector3(r0, cell.Value, z0),
                        new Vector3(r1, cell.Value, z1), new Vector3(l1, cell.Value, z1), Vector3.up);
                }
            }
        }
        float At(Vector3 a, Vector3 b, float z) => Mathf.LerpUnclamped(a.x, b.x, (z - a.z) / (b.z - a.z));
    }

    private sealed class RegionMeshData
    {
        private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();
        private readonly float size;
        internal RegionMeshData(float cellSize) { size = Mathf.Max(.001f, cellSize); }
        internal void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        { Triangle(a, b, c, normal); Triangle(a, c, d, normal); }
        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            var cross = Vector3.Cross(b - a, c - a);
            if (cross.sqrMagnitude < 1e-14f) return;
            if (Vector3.Dot(cross, normal) < 0) { var swap = b; b = c; c = swap; cross = -cross; }
            normal = cross.normalized;
            foreach (var p in new[] { a, b, c })
            { triangles.Add(vertices.Count); vertices.Add(p); normals.Add(normal); uv.Add(new Vector2(p.x / size + .5f, p.z / size + .5f)); }
        }
        internal void Apply(Mesh mesh)
        {
            mesh.Clear(); mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }
    }
}
