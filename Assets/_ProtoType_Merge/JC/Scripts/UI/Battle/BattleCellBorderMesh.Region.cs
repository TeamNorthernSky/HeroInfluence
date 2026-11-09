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
        // JC | 2026-11-09 | 명시적 예외 영역만 높이 경계에 수직 연결을 만들며 기본 평면 메시 결과는 보존합니다.
        public bool StepHeights;
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
                    float previousHeight = chain[(i + chain.Count - 1) % chain.Count].y;
                    if (StepHeights && !Mathf.Approximately(edge.y, previousHeight))
                        loop.Add(new Vector3((edge.a.x - .5f) * CellSize, previousHeight, (edge.a.y - .5f) * CellSize));
                    float ground = StepHeights ? edge.y : Mathf.Max(edge.y, previousHeight);
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
        if (fill)
        {
            FillRegion(data, region, outside);
            if (region.StepHeights) FillRisers(data, region, outside, segments);
        }
        else
        {
            var inside = OffsetLoops(region, inset + width, radius, segments);
            // JC | 2026-11-09 | 수직 단차의 위·아래 모서리만 둥글립니다. 두께는 곡면 법선 방향으로
            // 만들므로 수직 구간에서도 앞·뒷면이 겹치지 않습니다. 일반 평면 메시 생성은 그대로 유지합니다.
            var stepNormals = region.StepHeights ? RoundStepBorders(outside, inside, region.CellSize, segments) : null;
            var up = Vector3.up * depth;
            for (int k = 0; k < outside.Count; k++)
                for (int i = 0; i < outside[k].Count; i++)
                {
                    int j = (i + 1) % outside[k].Count;
                    var a = outside[k][i]; var b = outside[k][j];
                    var c = inside[k][i]; var d = inside[k][j];
                    if (stepNormals != null)
                    {
                        var n0 = stepNormals[k][i]; var n1 = stepNormals[k][j];
                        var center = up * .5f;
                        var a0 = a + center - n0 * depth * .5f; var a1 = a + center + n0 * depth * .5f;
                        var b0 = b + center - n1 * depth * .5f; var b1 = b + center + n1 * depth * .5f;
                        var c0 = c + center - n0 * depth * .5f; var c1 = c + center + n0 * depth * .5f;
                        var d0 = d + center - n1 * depth * .5f; var d1 = d + center + n1 * depth * .5f;
                        var normal = (n0 + n1).normalized;
                        var inward = ((c - a) + (d - b)).normalized;
                        data.Quad(a1, b1, d1, c1, normal);
                        data.Quad(a0, c0, d0, b0, -normal);
                        data.Quad(a0, b0, b1, a1, -inward);
                        data.Quad(c1, d1, d0, c0, inward);
                        continue;
                    }
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
                // JC | 2026-11-09 | 단차의 같은 XZ 정점 두 개는 동일한 수평 접선을 사용해 폭을 유지한 수직 띠로 연결합니다.
                if (region.StepHeights)
                {
                    for (int n = 1; n < loop.Count && FlatDistance(p, previous) < .000001f; n++) previous = loop[(i + loop.Count - n - 1) % loop.Count];
                    for (int n = 1; n < loop.Count && FlatDistance(p, next) < .000001f; n++) next = loop[(i + n + 1) % loop.Count];
                }
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

    private static float FlatDistance(Vector3 a, Vector3 b) => (a.x-b.x)*(a.x-b.x)+(a.z-b.z)*(a.z-b.z);

    private struct StepSample { internal Vector3 position, normal; }

    // JC | 2026-11-09 | 높이의 20%만 모서리 반경으로 사용해 가운데 60% 이상은 수직으로 남깁니다.
    // 윤곽과 채움이 같은 반경·단면을 사용해야 접합부에 틈이나 반투명 중복면이 생기지 않습니다.
    private static float StepRadius(float rise, float cellSize) => Mathf.Min(Mathf.Abs(rise) * .2f, cellSize * .035f);

    private static List<StepSample> StepProfile(Vector3 boundary, Vector3 towardHigh, float low, float high, float cellSize, int segments)
    {
        float r = StepRadius(high - low, cellSize);
        var result = new List<StepSample>();
        boundary.y = 0;
        for (int n = 0; n <= segments; n++)
        {
            float angle = n * Mathf.PI * .5f / segments;
            Add(-2 * r + r * Mathf.Sin(angle), low + r * (1 - Mathf.Cos(angle)),
                -towardHigh * Mathf.Sin(angle) + Vector3.up * Mathf.Cos(angle));
        }
        // 앞 호의 끝과 다음 호의 시작을 잇는 변이 실제 90도 수직면입니다.
        for (int n = 0; n <= segments; n++)
        {
            float angle = n * Mathf.PI * .5f / segments;
            Add(-r * Mathf.Cos(angle), high - r + r * Mathf.Sin(angle),
                -towardHigh * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle));
        }
        return result;
        void Add(float distance, float y, Vector3 normal)
        { result.Add(new StepSample { position = boundary + towardHigh * distance + Vector3.up * y, normal = normal }); }
    }

    // JC | 2026-11-09 | 경사로 대신 두 개의 1/4원과 수직선을 연결합니다. 윗모서리가 인도 속으로
    // 들어가지 않도록 수직면을 반경만큼 도로 쪽으로 빼고, 양 끝의 높이와 접선은 평면에 맞춥니다.
    private static List<List<Vector3>> RoundStepBorders(List<List<Vector3>> outside, List<List<Vector3>> inside, float cellSize, int segments)
    {
        var normals = new List<List<Vector3>>();
        for (int k = 0; k < outside.Count; k++)
        {
            var outer = outside[k]; var inner = inside[k];
            var roundedOuter = new List<Vector3>(); var roundedInner = new List<Vector3>();
            var loopNormals = new List<Vector3>();
            for (int i = 0; i < outer.Count; i++)
            {
                int next = (i + 1) % outer.Count;
                float rise = outer[next].y - outer[i].y;
                if (FlatDistance(outer[i], outer[next]) > .000001f || Mathf.Abs(rise) < .00001f)
                {
                    roundedOuter.Add(outer[i]); roundedInner.Add(inner[i]); loopNormals.Add(Vector3.up);
                    continue;
                }
                int neighbour = rise > 0 ? (i + outer.Count - 1) % outer.Count : (next + 1) % outer.Count;
                int low = rise > 0 ? i : next;
                var direction = outer[low] - outer[neighbour]; direction.y = 0; direction.Normalize();
                var profile = StepProfile(outer[low], direction, Mathf.Min(outer[i].y, outer[next].y), Mathf.Max(outer[i].y, outer[next].y), cellSize, segments);
                var across = inner[low] - outer[low];
                if (rise < 0) profile.Reverse();
                foreach (var sample in profile)
                {
                    roundedOuter.Add(sample.position); roundedInner.Add(sample.position + across); loopNormals.Add(sample.normal);
                }
                i++;
            }
            outside[k] = roundedOuter; inside[k] = roundedInner; normals.Add(loopNormals);
        }
        return normals;
    }

    // JC | 2026-11-09 | 채움의 도로/인도 사이 빈 틈도 연결하되, 둥근 외곽과 안쪽 여백 바깥에는 면을 만들지 않습니다.
    private static void FillRisers(RegionMeshData data, Region region, List<List<Vector3>> loops, int segments)
    {
        foreach (var cell in region.Cells)
        {
            Add(Vector2Int.right); Add(Vector2Int.up);
            void Add(Vector2Int offset)
            {
                if (!region.Cells.TryGetValue(cell.Key + offset, out float y) || Mathf.Approximately(y, cell.Value)) return;
                var center = new Vector3((cell.Key.x + offset.x * .5f) * region.CellSize, 0, (cell.Key.y + offset.y * .5f) * region.CellSize);
                var along = new Vector3(offset.y, 0, offset.x) * region.CellSize;
                var a = center - along * .5f; var b = center + along * .5f;
                var cuts = new List<float> { 0, 1 };
                foreach (var loop in loops) for (int i = 0; i < loop.Count; i++)
                {
                    var p = loop[i]; var q = loop[(i + 1) % loop.Count]; var edge = q - p;
                    float den = along.x * edge.z - along.z * edge.x;
                    if (Mathf.Abs(den) < .000001f) continue;
                    var delta = p - a;
                    float t = (delta.x * edge.z - delta.z * edge.x) / den;
                    float u = (delta.x * along.z - delta.z * along.x) / den;
                    if (t > 0 && t < 1 && u >= 0 && u <= 1) cuts.Add(t);
                }
                cuts.Sort();
                for (int i = 0; i + 1 < cuts.Count; i++)
                {
                    var p = Vector3.Lerp(a, b, cuts[i]); var q = Vector3.Lerp(a, b, cuts[i + 1]);
                    if (!Inside((p + q) * .5f)) continue;
                    // JC | 2026-11-09 | 수직면과 양쪽 둥근 모서리 전체를 동일한 채움으로 연결합니다.
                    var towardHigh = new Vector3(offset.x, 0, offset.y) * (y > cell.Value ? 1 : -1);
                    var profile = StepProfile(p, towardHigh, Mathf.Min(cell.Value, y), Mathf.Max(cell.Value, y), region.CellSize, segments);
                    for (int n = 0; n + 1 < profile.Count; n++)
                    {
                        var first = profile[n]; var last = profile[n + 1];
                        data.Quad(first.position, first.position + q - p, last.position + q - p, last.position,
                            (first.normal + last.normal).normalized);
                    }
                }
            }
        }
        bool Inside(Vector3 p)
        {
            bool inside = false;
            foreach (var loop in loops) for (int i = 0; i < loop.Count; i++)
            {
                var a = loop[i]; var b = loop[(i + 1) % loop.Count];
                if ((a.z > p.z) != (b.z > p.z) && p.x < (b.x-a.x)*(p.z-a.z)/(b.z-a.z)+a.x) inside = !inside;
            }
            return inside;
        }
    }

    private struct Crossing { public Vector3 a, b; public float x; }
    // JC | 2026-11-09 | 둥근 연결면이 차지하는 낮은 쪽 바닥 띠를 잘라 반투명 채움이 두 겹으로
    // 합성되지 않게 합니다. 높은 쪽 평면과 일반 영역의 경계는 변경하지 않습니다.
    private static Vector4 FillCellBounds(Region region, Vector2Int cell, float height)
    {
        float half = region.CellSize * .5f;
        float minX = (cell.x - .5f) * region.CellSize;
        var bounds = new Vector4(minX, cell.y * region.CellSize - half,
            minX + region.CellSize, cell.y * region.CellSize + half);
        if (!region.StepHeights) return bounds;
        bounds.x += Trim(Vector2Int.left); bounds.y += Trim(Vector2Int.down);
        bounds.z -= Trim(Vector2Int.right); bounds.w -= Trim(Vector2Int.up);
        return bounds;
        float Trim(Vector2Int offset) => region.Cells.TryGetValue(cell + offset, out float other) && other > height
            ? 2 * StepRadius(other - height, region.CellSize) : 0;
    }

    // 짝수·홀수 교차 규칙으로 구멍과 분리 영역을 함께 처리하고 셀 지면 높이별로 나눕니다.
    private static void FillRegion(RegionMeshData data, Region region, List<List<Vector3>> loops)
    {
        var levels = new SortedSet<float>();
        foreach (var loop in loops) foreach (var p in loop) levels.Add(p.z);
        foreach (var pair in region.Cells)
        {
            levels.Add((pair.Key.y - .5f) * region.CellSize);
            levels.Add((pair.Key.y + .5f) * region.CellSize);
            if (region.StepHeights)
            {
                var bounds = FillCellBounds(region, pair.Key, pair.Value);
                levels.Add(bounds.y); levels.Add(bounds.w);
            }
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
                var bounds = FillCellBounds(region, cell.Key, cell.Value);
                if (middle <= bounds.y || middle >= bounds.w) continue;
                float min = bounds.x, max = bounds.z;
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
