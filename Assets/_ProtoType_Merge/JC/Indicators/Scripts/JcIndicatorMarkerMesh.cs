using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JC.Indicators
{
    // 중심을 공유하는 문양의 반경 구간을 합친다. 겹치는 링/테두리도 내부 면 없이 생성한다.
    public sealed class JcIndicatorMarkerMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<float> angles = new List<float>();
        private readonly List<float> junctions = new List<float>();
        private readonly int[] order = { 0, 1, 2, 3, 4, 5 };
        private JcMovementIndicatorSettings style;

        public void Build(Mesh mesh, JcMovementIndicatorSettings settings)
        {
            style = settings.Sanitized();
            vertices.Clear(); normals.Clear(); uv.Clear(); colors.Clear(); triangles.Clear();
            angles.Clear(); junctions.Clear();
            int count = style.curveSegments * 4;
            float step = Mathf.PI * 2 / count;
            for (int i = 0; i <= count; i++) angles.Add(i * step);
            // 원과 둥근 사각형의 교점을 추가해 각 구간 내 경계 순서를 일정하게 유지한다.
            for (int box = 4; box <= 5; box++)
                for (int circle = 1; circle <= 3; circle++)
                    for (int i = 0; i < count; i++)
                    {
                        float a = i * step, b = (i + 1) * step;
                        float fa = Radius(box, a) - Radius(circle, a);
                        float fb = Radius(box, b) - Radius(circle, b);
                        if (Mathf.Abs(fa) < .000001f) junctions.Add(a);
                        if (fa * fb >= 0) continue;
                        for (int n = 0; n < 22; n++)
                        {
                            float m = (a + b) * .5f;
                            if ((Radius(box, m) - Radius(circle, m)) * fa > 0) a = m;
                            else b = m;
                        }
                        float angle = (a + b) * .5f;
                        angles.Add(angle); junctions.Add(angle);
                    }
            angles.Sort();
            for (int i = 0; i + 1 < angles.Count; i++)
            {
                float a = angles[i], b = angles[i + 1];
                if (b - a < .000001f) continue;
                float mid = (a + b) * .5f;
                Array.Sort(order, (x, y) => Radius(x, mid).CompareTo(Radius(y, mid)));
                int lower = -1;
                for (int j = 0; j < order.Length - 1; j++)
                {
                    float lo = Radius(order[j], mid), hi = Radius(order[j + 1], mid);
                    bool filled = hi - lo > .000001f && Filled((hi + lo) * .5f, mid);
                    if (filled && lower < 0) lower = order[j];
                    bool nextFilled = j + 2 < order.Length && Filled((hi + Radius(order[j + 2], mid)) * .5f, mid);
                    if (lower >= 0 && (!nextFilled || j == order.Length - 2))
                    {
                        Band(a, b, lower, order[j + 1]); lower = -1;
                    }
                }
            }
            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }

        private float Radius(int id, float angle)
        {
            switch (id)
            {
                case 0: return 0;
                case 1: return style.dotRadius;
                case 2: return Mathf.Max(0, style.ringRadius - style.ringWidth);
                case 3: return style.ringRadius;
                default:
                    float half = id == 4 ? .5f - style.borderWidth : .5f;
                    float corner = Mathf.Min(half, style.cornerRadius * (id == 4 ? .85f : 1));
                    float x = Mathf.Abs(Mathf.Cos(angle)), z = Mathf.Abs(Mathf.Sin(angle));
                    float straight = half / Mathf.Max(x, z);
                    if (Mathf.Min(x, z) * straight <= half - corner) return straight;
                    float center = half - corner;
                    float projection = center * (x + z);
                    return projection + Mathf.Sqrt(Mathf.Max(0, projection * projection - 2 * center * center + corner * corner));
            }
        }
        private bool Filled(float r, float angle) => r <= style.dotRadius
            || (r >= Radius(2, angle) && r <= style.ringRadius)
            || (r >= Radius(4, angle) && r <= Radius(5, angle));

        private float Bevel(float angle, float inner, float outer)
        {
            float width = Mathf.Min(style.bevelWidth, style.markerThickness * .5f, (outer - inner) * .24f);
            // 결합점에서는 베벨을 좁혀 서로 다른 문양의 접합부가 벌어지지 않게 한다.
            foreach (float junction in junctions)
                width *= Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, junction * Mathf.Rad2Deg)) / (90f / style.curveSegments));
            return width;
        }
        private static Vector3 Point(float a, float radius, float y) => new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
        private void Band(float a, float b, int inner, int outer)
        {
            float ia = Radius(inner, a), ib = Radius(inner, b), oa = Radius(outer, a), ob = Radius(outer, b);
            float ba = Bevel(a, ia, oa), bb = Bevel(b, ib, ob), h = style.markerThickness;
            bool hole = ia > .000001f || ib > .000001f;
            Vector3 ai = Point(a, ia, 0), bi = Point(b, ib, 0), ao = Point(a, oa, 0), bo = Point(b, ob, 0);
            Vector3 ait = Point(a, ia + (hole ? ba : 0), h), bit = Point(b, ib + (hole ? bb : 0), h);
            Vector3 aot = Point(a, oa - ba, h), bot = Point(b, ob - bb, h);
            Quad(ait, bit, bot, aot); // 윗면
            Quad(ai, ao, bo, bi); // 밑면
            Vector3 aos = Point(a, oa, h - ba), bos = Point(b, ob, h - bb);
            Quad(ao, aos, bos, bo); Quad(aos, aot, bot, bos);
            if (hole)
            {
                Vector3 ais = Point(a, ia, h - ba), bis = Point(b, ib, h - bb);
                Quad(ai, bi, bis, ais); Quad(ais, bis, bit, ait);
            }
        }
        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Triangle(a, b, c); Triangle(a, c, d);
        }
        private void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-16f) return;
            n.Normalize();
            Vertex(a, n); Vertex(b, n); Vertex(c, n);
        }
        private void Vertex(Vector3 v, Vector3 n)
        {
            triangles.Add(vertices.Count); vertices.Add(v); normals.Add(n);
            uv.Add(new Vector2(v.x, v.z)); colors.Add(Color.white);
        }
    }
}
