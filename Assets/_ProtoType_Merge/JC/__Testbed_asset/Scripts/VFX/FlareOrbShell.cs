using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「플레어 봄」 차징 오브 — 통통 티어드롭 셸 메시 생성기.
    /// 정원(구체) 몸통 위에 끝만 살짝 솟은 티어드롭 회전체를 절차 생성해
    /// MeshFilter에 꽂는다(tipHeight 0이면 정확히 구체).
    /// uv.v에 정규화 높이(0 하단..1 팁)를 구워 FlareAuraTongue 셰이더가 침식 밴드에 쓴다.
    /// 생성 메시는 DontSave라 씬/프리팹을 오염시키지 않는다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FlareOrbShell : MonoBehaviour
    {
        [Header("형태")]
        [Tooltip("몸통(구) 반경(m). 코어 구체(0.4)를 살짝 감싸는 값.")]
        public float radius = 0.46f;
        [Tooltip("팁 시작 높이(정규화 0~1). 이 높이부터 위 캡이 솟기 시작한다. 0.6~0.75 권장.")]
        [Range(0f, 0.95f)] public float tipStart = 0.65f;
        [Tooltip("팁 솟음 높이(m). 0이면 정확히 구체.")]
        public float tipHeight = 0.22f;
        [Tooltip("팁 곡률 지수. 클수록 옆이 오목하게 파이며 뾰족해진다. 1.5~2.5 권장.")]
        [Range(1f, 4f)] public float tipPower = 1.9f;
        [Tooltip("셸 전체 상하 오프셋(m). 메시 정점에 굽는다(트랜스폼 불변).")]
        public float yOffset = 0f;
        [Tooltip("몸통 세로 비율(타원화). 1=정원, >1=길쭉, <1=납작. 양 극점 간 거리를 조절한다.")]
        [Range(0.4f, 2f)] public float bodyHeightRatio = 1f;

        [Header("분할")]
        [Tooltip("둘레 분할수.")]
        [Range(8, 96)] public int radialSegments = 48;
        [Tooltip("세로 분할수.")]
        [Range(8, 96)] public int heightSegments = 40;

        [Header("운동")]
        [Tooltip("메시 Y축 자전 속도(도/초). 상승 무늬와 합쳐져 스파이럴로 보인다. 부호로 방향 반전.")]
        public float spinSpeed = 25f;

        Mesh _mesh;
        int _builtHash;

        int ParamHash()
        {
            unchecked
            {
                int h = radialSegments * 397 ^ heightSegments;
                h = h * 31 + radius.GetHashCode();
                h = h * 31 + tipStart.GetHashCode();
                h = h * 31 + tipHeight.GetHashCode();
                h = h * 31 + tipPower.GetHashCode();
                h = h * 31 + yOffset.GetHashCode();
                h = h * 31 + bodyHeightRatio.GetHashCode();
                return h;
            }
        }

        void OnEnable() { Build(); }

        void OnDisable()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
                _mesh = null;
            }
        }

        void Update()
        {
            if (_builtHash != ParamHash()) Build();
            // Spiral Shear(+)와 시각적 회전 방향이 일치하도록 부호 반전
            if (Application.isPlaying)
                transform.localRotation *= Quaternion.AngleAxis(-spinSpeed * Time.deltaTime, Vector3.up);
        }

        void Build()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "FlareTeardropShell" };
                _mesh.hideFlags = HideFlags.DontSave;
            }
            _mesh.Clear();

            int cols = radialSegments + 1;   // u 이음매용 중복 열
            int rows = heightSegments + 1;
            var verts = new Vector3[cols * rows];
            var uvs = new Vector2[cols * rows];

            // 정규화 v 계산용 상하한: 타원 몸통 반높이 ± 팁 솟음 + 오프셋
            float halfH = radius * bodyHeightRatio;
            float totalBottom = -halfH + yOffset;
            float totalTop = halfH + tipHeight + yOffset;

            for (int iy = 0; iy < rows; iy++)
            {
                float tRow = (float)iy / heightSegments;          // 0 하단극 .. 1 상단극
                float phi = Mathf.PI * tRow;                      // 하단극 0 .. 상단극 π
                float r = Mathf.Sin(phi) * radius;
                float y = -Mathf.Cos(phi) * halfH;                // 세로 타원화

                // 팁: tipStart 위 캡을 지수 곡선으로 끌어올린다 (tipHeight 0 = 타원체)
                float tn = Mathf.Clamp01((tRow - tipStart) / Mathf.Max(0.0001f, 1f - tipStart));
                y += tipHeight * Mathf.Pow(tn, tipPower);
                y += yOffset;

                float vNorm = Mathf.InverseLerp(totalBottom, totalTop, y);
                for (int ix = 0; ix < cols; ix++)
                {
                    float ang = (Mathf.PI * 2f) * ix / radialSegments;
                    verts[iy * cols + ix] = new Vector3(Mathf.Sin(ang) * r, y, Mathf.Cos(ang) * r);
                    uvs[iy * cols + ix] = new Vector2((float)ix / radialSegments, vNorm);
                }
            }

            var tris = new int[radialSegments * heightSegments * 6];
            int ti = 0;
            for (int iy = 0; iy < heightSegments; iy++)
                for (int ix = 0; ix < radialSegments; ix++)
                {
                    int a = iy * cols + ix;
                    int b = a + 1;
                    int c = a + cols;
                    int d = c + 1;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }

            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _builtHash = ParamHash();
        }
    }
}
