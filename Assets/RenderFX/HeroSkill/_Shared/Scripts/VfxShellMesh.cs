using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 세로 분할된 개방 실린더 셸 메시(캡 없음, 반지름 0.5, y∈[-1,1] — 유니티 실린더와 동일 규격).
    /// ★유니티 실린더 프리미티브는 측면에 상·하 링 2개뿐이라 버텍스 플레어의 곡률(pow)이
    ///   중간 정점 부재로 항상 직선 콘이 되어버림 → 세로 16분할로 곡선 프로파일이 실제로 보이게 함.
    /// 버텍스 플레어로 반경이 커져도 컬링되지 않게 바운드를 여유 있게 잡음. TaoAuraFlare/TaoBaseSpray 공용.
    /// </summary>
    public static class VfxShellMesh
    {
        private static Mesh _mesh;

        public static Mesh Get()
        {
            if (_mesh != null) return _mesh;
            const int radial = 48, rings = 16;
            var m = new Mesh { name = "VfxShellMesh" };
            var verts = new Vector3[(radial + 1) * (rings + 1)];
            var norms = new Vector3[verts.Length];
            int vi = 0;
            for (int j = 0; j <= rings; j++)
            {
                float y = -1f + 2f * j / rings;
                for (int i = 0; i <= radial; i++)
                {
                    float a = (float)i / radial * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    verts[vi] = new Vector3(c * 0.5f, y, s * 0.5f);
                    norms[vi] = new Vector3(c, 0f, s);
                    vi++;
                }
            }
            var tris = new int[radial * rings * 6];
            int ti = 0;
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < radial; i++)
                {
                    int a = j * (radial + 1) + i;
                    int b = a + radial + 1;
                    tris[ti++] = a; tris[ti++] = b; tris[ti++] = a + 1;
                    tris[ti++] = a + 1; tris[ti++] = b; tris[ti++] = b + 1;
                }
            m.vertices = verts;
            m.normals = norms;
            m.triangles = tris;
            m.bounds = new Bounds(Vector3.zero, new Vector3(4f, 2.4f, 4f));   // 플레어 확장분 여유
            _mesh = m;
            return m;
        }
    }
}
