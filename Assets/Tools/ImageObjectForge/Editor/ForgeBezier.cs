using System.Collections.Generic;
using UnityEngine;

namespace Orora.ImageObjectForge
{
    internal static class ForgeBezier
    {
        // 3차 베지어: p0 → p1(control) → p2(control) → p3, t ∈ [0,1]
        public static Vector2 Evaluate(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        // 베지어 세그먼트를 직선 근사 점 목록으로 평탄화. 적응적 세분화.
        public static void FlattenSegment(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, List<Vector2> output, float tolerance = 0.5f)
        {
            FlattenRecursive(p0, p1, p2, p3, output, tolerance, 0);
        }

        static void FlattenRecursive(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, List<Vector2> output, float tol, int depth)
        {
            if (depth > 10)
            {
                output.Add(p3);
                return;
            }

            // 직선과의 최대 편차 확인
            Vector2 d = p3 - p0;
            float lenSq = d.sqrMagnitude;
            float d1, d2;
            if (lenSq < 0.0001f)
            {
                d1 = (p1 - p0).magnitude;
                d2 = (p2 - p0).magnitude;
            }
            else
            {
                float inv = 1f / lenSq;
                float t1 = Mathf.Clamp01(Vector2.Dot(p1 - p0, d) * inv);
                d1 = (p1 - (p0 + d * t1)).magnitude;
                float t2 = Mathf.Clamp01(Vector2.Dot(p2 - p0, d) * inv);
                d2 = (p2 - (p0 + d * t2)).magnitude;
            }

            if (d1 + d2 <= tol)
            {
                output.Add(p3);
                return;
            }

            // De Casteljau 분할 (t=0.5)
            Vector2 p01 = (p0 + p1) * 0.5f;
            Vector2 p12 = (p1 + p2) * 0.5f;
            Vector2 p23 = (p2 + p3) * 0.5f;
            Vector2 p012 = (p01 + p12) * 0.5f;
            Vector2 p123 = (p12 + p23) * 0.5f;
            Vector2 mid = (p012 + p123) * 0.5f;

            FlattenRecursive(p0, p01, p012, mid, output, tol, depth + 1);
            FlattenRecursive(mid, p123, p23, p3, output, tol, depth + 1);
        }

        // PenVertex 목록(닫힌 경로)을 폴리곤 점 목록으로 변환
        public static List<Vector2> FlattenPath(IList<PenVertex> verts, float tolerance = 0.5f)
        {
            if (verts == null || verts.Count < 2) return new List<Vector2>();

            var pts = new List<Vector2>(verts.Count * 8);
            pts.Add(verts[0].Anchor);

            int n = verts.Count;
            for (int i = 0; i < n; i++)
            {
                var curr = verts[i];
                var next = verts[(i + 1) % n];

                Vector2 cp1 = curr.HandleOut;
                Vector2 cp2 = next.HandleIn;

                // 양쪽 모두 핸들 없으면 직선
                bool straight = curr.IsCorner && next.IsCorner;
                if (!straight)
                {
                    // 핸들 없는 쪽은 앵커 그대로 (직선 제어점)
                    if (curr.IsCorner) cp1 = curr.Anchor;
                    if (next.IsCorner) cp2 = next.Anchor;

                    // 두 제어점 모두 앵커와 거의 같으면 직선
                    straight = Vector2.Distance(cp1, curr.Anchor) < 0.1f
                            && Vector2.Distance(cp2, next.Anchor) < 0.1f;
                }

                if (straight)
                {
                    pts.Add(next.Anchor);
                }
                else
                {
                    FlattenSegment(curr.Anchor, cp1, cp2, next.Anchor, pts, tolerance);
                }
            }

            return pts;
        }

        // 열린 경로 (닫히지 않은 상태에서 프리뷰용)
        public static List<Vector2> FlattenOpenPath(IList<PenVertex> verts, float tolerance = 0.5f)
        {
            if (verts == null || verts.Count < 2) return new List<Vector2>();

            var pts = new List<Vector2>(verts.Count * 8);
            pts.Add(verts[0].Anchor);

            for (int i = 0; i < verts.Count - 1; i++)
            {
                var curr = verts[i];
                var next = verts[i + 1];

                Vector2 cp1 = curr.IsCorner ? curr.Anchor : curr.HandleOut;
                Vector2 cp2 = next.IsCorner ? next.Anchor : next.HandleIn;

                bool straight = Vector2.Distance(cp1, curr.Anchor) < 0.1f
                             && Vector2.Distance(cp2, next.Anchor) < 0.1f;

                if (straight)
                    pts.Add(next.Anchor);
                else
                    FlattenSegment(curr.Anchor, cp1, cp2, next.Anchor, pts, tolerance);
            }

            return pts;
        }
    }
}
