using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JC.Indicators
{
    // 게임 경로는 그대로 두고 표시용 경로만 지형 위에 구성한다.
    public sealed class JcIndicatorTerrainPath
    {
        public const float Clearance = .02f;
        private readonly List<float> sourceDistances = new List<float>();
        private readonly List<float> edges = new List<float>(), heights = new List<float>();
        private readonly List<float> cuts = new List<float>(), knots = new List<float>();
        private readonly List<float> ramps = new List<float>();

        public float Build(IReadOnlyList<Vector3> input, int reachable, GridManager grid,
            float rampLength, float radius, List<Vector3> output, out int displayReachable)
        {
            output.Clear(); sourceDistances.Clear(); edges.Clear(); heights.Clear(); knots.Clear(); ramps.Clear();
            displayReachable = 0;
            if (input.Count == 0) return 0;
            float distance = 0;
            sourceDistances.Add(0);
            float cell = Mathf.Max(.001f, grid.CellSize);
            for (int i = 0; i + 1 < input.Count; i++)
            {
                Vector3 a = input[i], b = input[i + 1];
                float length = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                if (length > .00001f)
                {
                    cuts.Clear(); cuts.Add(0); cuts.Add(1);
                    AxisCuts(a.x, b.x, radius, cell); AxisCuts(a.z, b.z, radius, cell);
                    cuts.Sort();
                    for (int j = 0; j + 1 < cuts.Count; j++)
                    {
                        if (cuts[j + 1] - cuts[j] < .000001f) continue;
                        Vector3 sample = Vector3.Lerp(a, b, (cuts[j] + cuts[j + 1]) * .5f);
                        float h = SurfaceHeight(grid, sample, radius) + Clearance;
                        float end = distance + cuts[j + 1] * length;
                        if (heights.Count > 0 && Mathf.Abs(heights[heights.Count - 1] - h) < .00001f)
                            edges[edges.Count - 1] = end;
                        else
                        {
                            if (edges.Count == 0) edges.Add(distance + cuts[j] * length);
                            heights.Add(h); edges.Add(end);
                        }
                    }
                }
                distance += length; sourceDistances.Add(distance);
            }
            if (heights.Count == 0)
            {
                Vector3 p = input[0]; p.y = SurfaceHeight(grid, p, radius) + Clearance;
                output.Add(p); return 0;
            }
            for (int i = 0; i < edges.Count; i++) ramps.Add(0);
            knots.AddRange(sourceDistances);
            for (int i = 1; i < heights.Count; i++)
            {
                bool up = heights[i] > heights[i - 1];
                int low = up ? i - 1 : i;
                // 짧은 낮은 구간 양쪽에 경사가 있더라도 서로 뒤집히지 않게 제한한다.
                float available = edges[low + 1] - edges[low];
                bool both = low > 0 && low + 1 < heights.Count
                    && heights[low - 1] > heights[low] && heights[low + 1] > heights[low];
                float run = Mathf.Min(Mathf.Max(.01f, rampLength), available * (both ? .45f : .9f));
                ramps[i] = run;
                knots.Add(edges[i]); knots.Add(edges[i] + (up ? -run : run));
            }
            knots.Sort();
            float reachableDistance = sourceDistances[Mathf.Clamp(reachable, 0, input.Count - 1)];
            float length3D = 0, previous = -1;
            for (int i = 0; i < knots.Count; i++)
            {
                float d = knots[i];
                if (previous >= 0 && d - previous < .000001f) continue;
                Vector3 p = Sample(input, d); p.y = Height(d);
                if (output.Count > 0) length3D += Vector3.Distance(output[output.Count - 1], p);
                output.Add(p);
                if (d <= reachableDistance + .000001f) displayReachable = output.Count - 1;
                previous = d;
            }
            return length3D;
        }

        private void AxisCuts(float a, float b, float radius, float cell)
        {
            if (Mathf.Abs(b - a) < .000001f) return;
            int lo = Mathf.FloorToInt((Mathf.Min(a, b) - radius) / cell - .5f);
            int hi = Mathf.CeilToInt((Mathf.Max(a, b) + radius) / cell - .5f);
            for (int k = lo; k <= hi; k++)
            {
                float boundary = (k + .5f) * cell;
                AddCut((boundary - radius - a) / (b - a));
                AddCut((boundary + radius - a) / (b - a));
            }
        }
        private void AddCut(float t) { if (t > 0 && t < 1) cuts.Add(t); }

        public static float SurfaceHeight(GridManager grid, Vector3 p, float radius)
        {
            float cell = Mathf.Max(.001f, grid.CellSize), height = float.NegativeInfinity;
            int x0 = Mathf.FloorToInt((p.x - radius) / cell + .5f);
            int x1 = Mathf.FloorToInt((p.x + radius) / cell + .5f);
            int z0 = Mathf.FloorToInt((p.z - radius) / cell + .5f);
            int z1 = Mathf.FloorToInt((p.z + radius) / cell + .5f);
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                height = Mathf.Max(height, grid.GetCellSurfaceY(new Vector2Int(x, z)));
            return height;
        }

        private Vector3 Sample(IReadOnlyList<Vector3> input, float d)
        {
            for (int i = 0; i + 1 < input.Count; i++)
            {
                float length = sourceDistances[i + 1] - sourceDistances[i];
                if (length > .000001f && d <= sourceDistances[i + 1])
                    return Vector3.Lerp(input[i], input[i + 1], (d - sourceDistances[i]) / length);
            }
            return input[input.Count - 1];
        }
        private float Height(float d)
        {
            int index = 0;
            while (index + 1 < heights.Count && d > edges[index + 1]) index++;
            float h = heights[index];
            if (index > 0 && heights[index - 1] > h && ramps[index] > 0)
                h = Mathf.Max(h, Mathf.Lerp(heights[index - 1], heights[index], (d - edges[index]) / ramps[index]));
            if (index + 1 < heights.Count && heights[index + 1] > heights[index] && ramps[index + 1] > 0)
                h = Mathf.Max(h, Mathf.Lerp(heights[index], heights[index + 1],
                    (d - edges[index + 1] + ramps[index + 1]) / ramps[index + 1]));
            return h;
        }
    }

    // 그림자 삼각형을 셀 경계에서 잘라 평평한 각 타일 위에 투영한다. 턱을 가로지르는 사선 면을 만들지 않는다.
    public sealed class JcIndicatorGroundShadow
    {
        private struct Vertex { public Vector3 p; public Vector2 uv; public Color color; }
        private List<Vertex> polygon = new List<Vertex>(), buffer = new List<Vertex>();
        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<Vector2> texcoords = new List<Vector2>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private readonly Dictionary<Vector2Int, float> sampled = new Dictionary<Vector2Int, float>();
        public bool TerrainChanged(GridManager grid)
        {
            foreach (var pair in sampled) if (grid.GetCellSurfaceY(pair.Key) != pair.Value) return true;
            return false;
        }
        public void Project(Mesh mesh, Matrix4x4 localToWorld, GridManager grid)
        {
            var vertices = mesh.vertices; var uv = mesh.uv; var tint = mesh.colors; var indices = mesh.triangles;
            positions.Clear(); texcoords.Clear(); colors.Clear(); triangles.Clear(); sampled.Clear();
            float cell = Mathf.Max(.001f, grid.CellSize);
            Matrix4x4 inverse = localToWorld.inverse;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vertex a = Read(indices[i], vertices, uv, tint, localToWorld);
                Vertex b = Read(indices[i + 1], vertices, uv, tint, localToWorld);
                Vertex c = Read(indices[i + 2], vertices, uv, tint, localToWorld);
                int x0 = Mathf.FloorToInt(Mathf.Min(a.p.x, Mathf.Min(b.p.x, c.p.x)) / cell + .5f);
                int x1 = Mathf.FloorToInt(Mathf.Max(a.p.x, Mathf.Max(b.p.x, c.p.x)) / cell + .5f);
                int z0 = Mathf.FloorToInt(Mathf.Min(a.p.z, Mathf.Min(b.p.z, c.p.z)) / cell + .5f);
                int z1 = Mathf.FloorToInt(Mathf.Max(a.p.z, Mathf.Max(b.p.z, c.p.z)) / cell + .5f);
                for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                {
                    polygon.Clear(); polygon.Add(a); polygon.Add(b); polygon.Add(c);
                    Clip(0, (x - .5f) * cell, true); Clip(0, (x + .5f) * cell, false);
                    Clip(2, (z - .5f) * cell, true); Clip(2, (z + .5f) * cell, false);
                    if (polygon.Count < 3) continue;
                    var key = new Vector2Int(x, z); float h = grid.GetCellSurfaceY(key); sampled[key] = h;
                    int start = positions.Count;
                    foreach (Vertex v in polygon)
                    {
                        Vector3 p = v.p; p.y = h + .015f;
                        positions.Add(inverse.MultiplyPoint3x4(p)); texcoords.Add(v.uv); colors.Add(v.color);
                    }
                    for (int k = 1; k + 1 < polygon.Count; k++)
                    { triangles.Add(start); triangles.Add(start + k); triangles.Add(start + k + 1); }
                }
            }
            mesh.Clear(); mesh.indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(positions); mesh.SetUVs(0, texcoords); mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }
        private static Vertex Read(int i, Vector3[] p, Vector2[] uv, Color[] colors, Matrix4x4 matrix)
            => new Vertex { p = matrix.MultiplyPoint3x4(p[i]), uv = uv[i], color = colors.Length > i ? colors[i] : Color.white };
        private void Clip(int axis, float edge, bool greater)
        {
            buffer.Clear();
            if (polygon.Count == 0) return;
            Vertex a = polygon[polygon.Count - 1]; bool insideA = greater ? a.p[axis] >= edge : a.p[axis] <= edge;
            foreach (Vertex b in polygon)
            {
                bool insideB = greater ? b.p[axis] >= edge : b.p[axis] <= edge;
                if (insideA != insideB)
                {
                    float t = (edge - a.p[axis]) / (b.p[axis] - a.p[axis]);
                    buffer.Add(new Vertex { p = Vector3.Lerp(a.p, b.p, t), uv = Vector2.Lerp(a.uv, b.uv, t), color = Color.Lerp(a.color, b.color, t) });
                }
                if (insideB) buffer.Add(b);
                a = b; insideA = insideB;
            }
            var swap = polygon; polygon = buffer; buffer = swap;
        }
    }
}
