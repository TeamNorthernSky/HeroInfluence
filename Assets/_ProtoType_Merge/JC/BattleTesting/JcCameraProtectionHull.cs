using System.Collections.Generic;
using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-10 | 26방향 지지 평면의 교집합으로 원래 모든 점을 포함하는 외피를 만듭니다.
    // 정점 일부를 버리는 샘플링과 달리 몸체 보호를 유지하면서 반복 투영 계산량을 줄입니다.
    public static class JcCameraProtectionHull
    {
        public static Vector3[] Enclose(IReadOnlyList<Vector3> points)
        {
            if (points.Count < 100) { var copy = new Vector3[points.Count]; for (int i = 0; i < copy.Length; i++) copy[i] = points[i]; return copy; }
            var axes = new List<Vector3>();
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
                if (x != 0 || y != 0 || z != 0) axes.Add(new Vector3(x, y, z).normalized);
            var distances = new float[axes.Count];
            for (int i = 0; i < axes.Count; i++)
            {
                distances[i] = float.NegativeInfinity;
                for (int p = 0; p < points.Count; p++) distances[i] = Mathf.Max(distances[i], Vector3.Dot(axes[i], points[p]));
                distances[i] += .0001f;
            }
            var result = new List<Vector3>();
            for (int i = 0; i < axes.Count; i++) for (int j = i + 1; j < axes.Count; j++) for (int k = j + 1; k < axes.Count; k++)
            {
                var cross = Vector3.Cross(axes[j], axes[k]);
                float determinant = Vector3.Dot(axes[i], cross);
                if (Mathf.Abs(determinant) < .00001f) continue;
                var vertex = (distances[i] * cross + distances[j] * Vector3.Cross(axes[k], axes[i]) + distances[k] * Vector3.Cross(axes[i], axes[j])) / determinant;
                bool inside = true;
                for (int n = 0; n < axes.Count; n++)
                    if (Vector3.Dot(axes[n], vertex) > distances[n] + .0001f) { inside = false; break; }
                if (!inside) continue;
                bool duplicate = false;
                for (int n = 0; n < result.Count; n++) if ((result[n] - vertex).sqrMagnitude < .000001f) { duplicate = true; break; }
                if (!duplicate) result.Add(vertex);
            }
            return result.ToArray();
        }
    }
}
