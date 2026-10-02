using System.Collections.Generic;
using UnityEngine;

namespace JC.Tutorial
{
    // 앞뒤 뚜껑·측면·베벨을 가진 닫힌 프리즘. 면별 정점으로 모따기 경계를 유지한다.
    internal sealed class JcTutorialSolidMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        public static List<Vector2> Chamfer(Vector2[] polygon, float cut)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 p = polygon[i], prev = polygon[(i + polygon.Length - 1) % polygon.Length], next = polygon[(i + 1) % polygon.Length];
                float distance = Mathf.Min(cut, Mathf.Min((prev - p).magnitude, (next - p).magnitude) * .25f);
                result.Add(p + (prev - p).normalized * distance);
                result.Add(p + (next - p).normalized * distance);
            }
            float area = 0;
            for (int i = 0; i < result.Count; i++) area += Cross(result[i], result[(i + 1) % result.Count]);
            if (area < 0) result.Reverse();
            return result;
        }

        public void Prism(List<Vector2> outline, float depth, float bevel, Color color, bool floor = false)
        {
            float half = depth * .5f;
            float lip = Mathf.Clamp(bevel, .0001f, half * .8f);
            // 동심 윤곽을 사용하여 오목한 화살표에서도 교차하지 않는 베벨을 만든다.
            float inset = 1 - Mathf.Clamp(bevel * 2, .001f, .15f);
            Vector3 Map(Vector2 p, float z) => floor ? new Vector3(p.x, half - z, p.y) : new Vector3(p.x, p.y, z);
            Vector3 Normal(Vector3 n) => floor ? new Vector3(n.x, -n.z, n.y) : n;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                Vector3 side = new Vector3(b.y - a.y, a.x - b.x, 0).normalized;
                Quad(Map(a, -half + lip), Map(b, -half + lip), Map(b, half - lip), Map(a, half - lip), Normal(side), color);
                for (int sign = -1; sign <= 1; sign += 2)
                    Quad(Map(a * inset, sign * half), Map(b * inset, sign * half), Map(b, sign * (half - lip)), Map(a, sign * (half - lip)), Normal(side + Vector3.forward * sign), color);
            }
            var remaining = new List<int>();
            for (int i = 0; i < outline.Count; i++) remaining.Add(i);
            while (remaining.Count > 2)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int ia = remaining[(i + remaining.Count - 1) % remaining.Count], ib = remaining[i], ic = remaining[(i + 1) % remaining.Count];
                    Vector2 a = outline[ia], b = outline[ib], c = outline[ic];
                    if (Cross(b - a, c - b) <= .000001f) continue;
                    bool occupied = false;
                    foreach (int j in remaining)
                        if (j != ia && j != ib && j != ic && Inside(outline[j], a, b, c)) { occupied = true; break; }
                    if (occupied) continue;
                    for (int sign = -1; sign <= 1; sign += 2)
                        Triangle(Map(a * inset, sign * half), Map(b * inset, sign * half), Map(c * inset, sign * half), Normal(Vector3.forward * sign), color);
                    remaining.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) throw new System.InvalidOperationException("튜토리얼 표식 윤곽을 삼각형으로 나눌 수 없습니다.");
            }
        }

        public void Ring(List<Vector2> outline, float innerScale, float depth, float bevel, Color color, bool floor = true)
        {
            float lip = Mathf.Min(bevel, depth * .3f);
            float cut = Mathf.Min(bevel, (1 - innerScale) * .15f);
            var scales = new[] { 1 - cut, 1f, 1f, 1 - cut, innerScale + cut, innerScale, innerScale, innerScale + cut };
            var heights = new[] { 0f, lip, depth - lip, depth, depth, depth - lip, lip, 0f };
            Vector3 Map(Vector2 v, float h) => floor ? new Vector3(v.x,h,v.y) : new Vector3(v.x,v.y,h-depth*.5f);
            for(int k=0;k<8;k++) for(int i=0;i<outline.Count;i++)
            {
                int n=(i+1)%outline.Count, j=(k+1)%8;
                Vector2 middle=(outline[i]+outline[n])*.5f;
                Vector3 normal = k==3 ? (floor?Vector3.up:Vector3.forward) : k==7 ? (floor?Vector3.down:Vector3.back) : Map(middle,0)-Map(Vector2.zero,0);
                if(k>=4 && k<=6)normal=-normal;
                Quad(Map(outline[i]*scales[k],heights[k]),Map(outline[n]*scales[k],heights[k]),Map(outline[n]*scales[j],heights[j]),Map(outline[i]*scales[j],heights[j]),normal,color);
            }
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c) => Cross(b - a, p - a) >= -1e-7f && Cross(c - b, p - b) >= -1e-7f && Cross(a - c, p - c) >= -1e-7f;
        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color)
        { Triangle(a, b, c, normal, color); Triangle(a, c, d, normal, color); }
        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Color color)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0) { var swap = b; b = c; c = swap; }
            int start = vertices.Count; color.a = 1;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            for (int i = 0; i < 3; i++) { uv.Add(new Vector2(vertices[start + i].x, vertices[start + i].z)); colors.Add(color); triangles.Add(start + i); }
        }
        public void Apply(Mesh mesh)
        { mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); }
    }
}
