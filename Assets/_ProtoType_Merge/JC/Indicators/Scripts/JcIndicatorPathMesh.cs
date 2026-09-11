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
        private readonly Surface solid = new Surface();
        private readonly Surface glow = new Surface();
        private readonly List<float> cuts = new List<float>();
        private IReadOnlyList<Vector3> source;
        private float totalLength;

        // 점선마다 닫힌 입체 표면을 만들되 하나의 Mesh에 모은다. 그림자는 기존 리본을 사용한다.
        public void BuildSolid(Mesh mesh, Mesh glowMesh, IReadOnlyList<Vector3> points, int reachableSegments,
            JcMovementIndicatorSettings settings, float clock, JcIndicatorPulse pulse,
            Vector3 markerCenter, float markerSize, bool clipMarker)
        {
            source = points; solid.Clear(); glow.Clear(); distances.Clear(); totalLength = 0;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) totalLength += Vector2.Distance(XZ(points[i - 1]), XZ(points[i]));
                distances.Add(totalLength);
            }
            if (points.Count >= 2 && totalLength > .00001f)
            {
                float visibleLength = VisibleLength(markerCenter, markerSize, settings.cornerRadius, clipMarker);
                float period = settings.dashLength + settings.dashGap;
                float halfLength = settings.dashLength * .5f, halfWidth = settings.lineWidth * .5f;
                float phase = Mathf.Repeat(clock * settings.flowSpeed, period);
                float first = Mathf.Ceil((-totalLength - halfLength - phase) / period) * period + phase;
                float reachableDistance = distances[Mathf.Clamp(reachableSegments, 0, distances.Count - 1)];
                float glowWidth = settings.dashGlowStrength > 0 ? settings.dashGlowWidth : 0;
                int steps = Mathf.Clamp(settings.curveSegments, 4, 8);
                for (float u = first; u - halfLength < visibleLength - totalLength; u += period)
                {
                    float center = u + totalLength;
                    float start = Mathf.Max(0, center - halfLength), end = Mathf.Min(visibleLength, center + halfLength);
                    if (end - start < .00001f) continue;
                    // 끝에서 잘린 점선도 경로 안의 위상을 사용해 마지막 복귀가 주기 안에서 완료되게 한다.
                    float remaining = totalLength - Mathf.Clamp(center, 0, totalLength);
                    AddDash(solid, center, halfLength, halfWidth, start, end, 0, settings.commonThickness,
                        reachableDistance, remaining, steps, false);
                    if (glowWidth > 0)
                        AddDash(glow, center, halfLength + glowWidth, halfWidth + glowWidth,
                            Mathf.Max(0, center - halfLength - glowWidth), Mathf.Min(visibleLength, center + halfLength + glowWidth),
                            settings.commonThickness + .0005f, 0, reachableDistance, remaining, steps, true);
                }
            }
            solid.Write(mesh, pulse, settings.flickerLiftHeight); glow.Write(glowMesh, pulse, settings.flickerLiftHeight);
        }

        public void UpdateLift(Mesh mesh, Mesh glowMesh, JcIndicatorPulse pulse, float height)
        {
            solid.UpdateLift(mesh, pulse, height); glow.UpdateLift(glowMesh, pulse, height);
        }

        private void AddDash(Surface surface, float center, float halfLength, float halfWidth, float start, float end,
            float lift, float thickness, float reachableDistance, float remaining, int steps, bool flat)
        {
            if (end - start < .00001f) return;
            surface.ShapeCenter = center - totalLength;
            cuts.Clear(); cuts.Add(start); cuts.Add(end);
            float radius = Mathf.Min(halfLength, halfWidth);
            float left = center - halfLength + radius, right = center + halfLength - radius;
            for (int i = 0; i <= steps; i++)
            {
                float angle = i * Mathf.PI * .5f / steps;
                Cut(left - radius * Mathf.Cos(angle), start, end);
                Cut(right + radius * Mathf.Sin(angle), start, end);
            }
            for (int i = 1; i + 1 < distances.Count; i++) Cut(distances[i], start, end);
            Cut(reachableDistance, start, end);
            cuts.Sort();
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                float a = cuts[i], b = cuts[i + 1];
                if (b - a < .000001f) continue;
                float wa = WidthAt(a, left, right, radius, halfWidth), wb = WidthAt(b, left, right, radius, halfWidth);
                Color state = (a + b) * .5f < reachableDistance ? Color.white : Color.black;
                Vector3 al = Position(a, -wa) + Vector3.up * lift, ar = Position(a, wa) + Vector3.up * lift;
                Vector3 bl = Position(b, -wb) + Vector3.up * lift, br = Position(b, wb) + Vector3.up * lift;
                Vector3 up = Vector3.up * thickness;
                Vector2 ual = new Vector2(a - totalLength, -wa), uar = new Vector2(a - totalLength, wa);
                Vector2 ubl = new Vector2(b - totalLength, -wb), ubr = new Vector2(b - totalLength, wb);
                surface.Quad(al + up, bl + up, br + up, ar + up, ual, ubl, ubr, uar, state, -remaining);
                if (flat) continue;
                surface.Quad(al, ar, br, bl, ual, uar, ubr, ubl, state, -remaining);
                surface.Quad(al, bl, bl + up, al + up, ual, ubl, ubl, ual, state, -remaining);
                surface.Quad(ar, ar + up, br + up, br, uar, uar, ubr, ubr, state, -remaining);
            }
            if (!flat)
            {
                Cap(surface, start, WidthAt(start, left, right, radius, halfWidth), lift, thickness,
                    start < reachableDistance ? Color.white : Color.black, -remaining, false);
                Cap(surface, end, WidthAt(end, left, right, radius, halfWidth), lift, thickness,
                    end - .000001f < reachableDistance ? Color.white : Color.black, -remaining, true);
            }
        }

        private void Cut(float distance, float start, float end)
        {
            if (distance > start + .000001f && distance < end - .000001f) cuts.Add(distance);
        }

        private static float WidthAt(float d, float left, float right, float radius, float halfWidth)
        {
            float delta = Mathf.Max(Mathf.Max(left - d, d - right), 0);
            return halfWidth - radius + Mathf.Sqrt(Mathf.Max(0, radius * radius - delta * delta));
        }

        private void Cap(Surface surface, float d, float width, float lift, float thickness, Color color, float phase, bool end)
        {
            Vector3 l = Position(d, -width) + Vector3.up * lift, r = Position(d, width) + Vector3.up * lift;
            Vector3 up = Vector3.up * thickness;
            Vector2 ul = new Vector2(d - totalLength, -width), ur = new Vector2(d - totalLength, width);
            if (end) surface.Quad(l, r, r + up, l + up, ul, ur, ur, ul, color, phase);
            else surface.Quad(l, l + up, r + up, r, ul, ul, ur, ur, color, phase);
        }

        private Vector3 Position(float d, float width)
        {
            for (int i = 0; i + 1 < source.Count; i++)
            {
                float length = distances[i + 1] - distances[i];
                if (length < .00001f || d > distances[i + 1] + .000001f) continue;
                if (Mathf.Abs(d - distances[i]) < .000001f) return source[i] + JoinOffset(source, i, width);
                if (Mathf.Abs(d - distances[i + 1]) < .000001f) return source[i + 1] + JoinOffset(source, i + 1, width);
                return Vector3.Lerp(source[i], source[i + 1], (d - distances[i]) / length)
                    + Vector3.Cross(Vector3.up, Direction(source[i], source[i + 1])) * width;
            }
            return source[source.Count - 1] + JoinOffset(source, source.Count - 1, width);
        }

        private float VisibleLength(Vector3 center, float size, float corner, bool clip)
        {
            if (!clip || BoxDistance(source[source.Count - 1], center, size, corner) > 0) return totalLength;
            for (int i = source.Count - 2; i >= 0; i--)
            {
                if (BoxDistance(source[i], center, size, corner) <= 0) continue;
                float lo = 0, hi = 1;
                for (int n = 0; n < 24; n++)
                {
                    float mid = (lo + hi) * .5f;
                    if (BoxDistance(Vector3.Lerp(source[i], source[i + 1], mid), center, size, corner) > 0) lo = mid;
                    else hi = mid;
                }
                // 알파로만 자르면 단면이 열리므로 이 거리에서 실제 끝면을 만든다.
                return Mathf.Lerp(distances[i], distances[i + 1], lo);
            }
            return 0;
        }

        private static float BoxDistance(Vector3 point, Vector3 center, float size, float corner)
        {
            Vector2 p = XZ(point - center) / Mathf.Max(.001f, size);
            float radius = Mathf.Min(.5f, corner);
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - Vector2.one * (.5f - radius);
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
        }

        private sealed class Surface
        {
            public float ShapeCenter;
            private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            private readonly List<Vector3> baseVertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>(), centers = new List<Vector2>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> triangles = new List<int>();
            public void Clear() { vertices.Clear(); baseVertices.Clear(); normals.Clear(); uv.Clear(); centers.Clear(); colors.Clear(); triangles.Clear(); }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Color color, float center)
            {
                Triangle(a, b, c, ua, ub, uc, color, center); Triangle(a, c, d, ua, uc, ud, color, center);
            }
            private void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Color color, float center)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-16f) return;
                normal /= Mathf.Sqrt(normal.sqrMagnitude);
                Vertex(a, ua, normal, color, center); Vertex(b, ub, normal, color, center); Vertex(c, uc, normal, color, center);
            }
            private void Vertex(Vector3 v, Vector2 tex, Vector3 n, Color color, float center)
            {
                triangles.Add(vertices.Count); vertices.Add(v); baseVertices.Add(v); normals.Add(n); uv.Add(tex); colors.Add(color); centers.Add(new Vector2(ShapeCenter, center));
            }
            private void MoveVertices(JcIndicatorPulse pulse, float height)
            {
                float previous = float.NaN, lift = 0;
                for (int i = 0; i < vertices.Count; i++)
                {
                    float center = centers[i].y;
                    if (center != previous) { lift = height * pulse.Evaluate(-center); previous = center; }
                    vertices[i] = baseVertices[i] + Vector3.up * lift;
                }
            }
            public void UpdateLift(Mesh mesh, JcIndicatorPulse pulse, float height)
            {
                MoveVertices(pulse, height); mesh.SetVertices(vertices); mesh.RecalculateBounds();
            }
            public void Write(Mesh mesh, JcIndicatorPulse pulse, float height)
            {
                MoveVertices(pulse, height);
                mesh.Clear(); mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(1, centers);
                mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            }
        }

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
