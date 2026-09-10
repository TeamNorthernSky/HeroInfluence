using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JC.Indicators
{
    // 모든 구간은 도착점 기준 누적 UV를 공유한다. 이동으로 시작 구간이 줄어도 위상이 유지된다.
    public sealed class JcIndicatorPathMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<float> distances = new List<float>();

        public void Build(Mesh mesh, IReadOnlyList<Vector3> points, int reachableSegments, float halfWidth)
        {
            vertices.Clear(); uv.Clear(); colors.Clear(); triangles.Clear(); distances.Clear();
            float length = 0;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) length += Vector2.Distance(XZ(points[i - 1]), XZ(points[i]));
                distances.Add(length);
            }
            for (int i = 0; i + 1 < points.Count; i++)
            {
                if (distances[i + 1] - distances[i] < .00001f) continue;
                Vector3 a = JoinOffset(points, i, halfWidth);
                Vector3 b = JoinOffset(points, i + 1, halfWidth);
                int first = vertices.Count;
                vertices.Add(points[i] - a); vertices.Add(points[i] + a);
                vertices.Add(points[i + 1] - b); vertices.Add(points[i + 1] + b);
                uv.Add(new Vector2(distances[i] - length, -halfWidth));
                uv.Add(new Vector2(distances[i] - length, halfWidth));
                uv.Add(new Vector2(distances[i + 1] - length, -halfWidth));
                uv.Add(new Vector2(distances[i + 1] - length, halfWidth));
                Color state = i < reachableSegments ? Color.white : Color.black;
                for (int c = 0; c < 4; c++) colors.Add(state);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
                triangles.Add(first + 1); triangles.Add(first + 2); triangles.Add(first + 3);
            }
            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }

        private static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);
        private static Vector3 Direction(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a; d.y = 0;
            return d.sqrMagnitude > .0000001f ? d.normalized : Vector3.zero;
        }
        private static Vector3 JoinOffset(IReadOnlyList<Vector3> p, int i, float width)
        {
            Vector3 incoming = i > 0 ? Direction(p[i - 1], p[i]) : Vector3.zero;
            Vector3 outgoing = i + 1 < p.Count ? Direction(p[i], p[i + 1]) : Vector3.zero;
            if (incoming == Vector3.zero) incoming = outgoing;
            if (outgoing == Vector3.zero) outgoing = incoming;
            Vector3 normal = Vector3.Cross(Vector3.up, outgoing);
            Vector3 miter = Vector3.Cross(Vector3.up, incoming + outgoing).normalized;
            if (miter.sqrMagnitude < .001f) return normal * width;
            return miter * (width / Mathf.Max(.5f, Mathf.Abs(Vector3.Dot(miter, normal))));
        }
    }
}
