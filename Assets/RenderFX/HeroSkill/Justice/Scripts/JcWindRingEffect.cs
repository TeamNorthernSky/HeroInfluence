using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 바람무늬 방사광(안개 토러스) — ASB 연출 규약(ISkillEffectBehaviour) 구현체.
    ///
    /// 구성 요소 3개를 런타임에 생성한다(프리팹은 루트 하나로 가볍게 유지):
    ///   Floor      바닥 안개 도넛 쿼드 — 반경 방향 폭(접지감)
    ///   WallOuter  외벽 원통 셸 — 높이·소멸 뜯김
    ///   WallInner  내벽 원통 셸 — 외벽과 회전 배속을 달리해 시차로 두께감을 만든다
    /// 셋이 재질 하나(Testbed/Justice/WindRing)를 공유하고, 지오메트리 의존 파라미터
    /// (모드·밴드·회전 배속)는 렌더러별 MPB로 덮는다. 회전·표류는 셰이더 _Time이 전담하고
    /// 이 컴포넌트는 수명 알파 엔벨로프(등장→유지→소멸)만 매 프레임 민다.
    ///
    /// 위치는 재생 순간 고정(궤도 중심) — 호 획·포인트 획과 같은 소켓 비부모 규칙.
    /// </summary>
    [DisallowMultipleComponent]
    public class JcWindRingEffect : MonoBehaviour, ISkillEffectBehaviour
    {
        [Header("형상 (m) — 바인더가 프리셋에서 채운다")]
        [SerializeField, Min(0.05f)] private float floorInner = 0.9f;
        [SerializeField, Min(0.1f)] private float floorOuter = 1.8f;
        [SerializeField, Min(0.1f)] private float wallRadius = 1.7f;
        [SerializeField, Min(0.02f)] private float wallThickness = 0.25f;
        [SerializeField, Min(0.1f)] private float wallHeight = 1.2f;
        [SerializeField, Min(0f)] private float groundLift = 0.03f;

        [Header("노이즈·운동 (렌더러별 MPB 몫)")]
        [SerializeField, Range(0.02f, 0.6f)] private float bandSoft = 0.18f;
        [SerializeField, Range(0f, 1f)] private float tearAmount = 0.5f;
        [SerializeField, Range(-2f, 2f)] private float innerSpinMul = -0.6f;
        [SerializeField, Range(0f, 2f)] private float floorAlphaMul = 0.8f;

        [Header("수명 (초)")]
        [SerializeField, Min(0.02f)] private float fadeIn = 0.12f;
        [SerializeField, Min(0f)] private float hold = 0.45f;
        [SerializeField, Min(0.05f)] private float fadeOut = 0.5f;

        [Header("배치 · 재질")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -0.2f, 0f);
        [SerializeField] private Material windMaterial;

        [Header("카메라 보정 (매 프레임 추종)")]
        [SerializeField, Range(0f, 1f)] private float cameraTilt = 0.25f;
        [SerializeField] private bool autoCenter = true;
        [SerializeField, Range(-2f, 2f)] private float centerShift = 0f;

        [SerializeField] private bool logLifecycle;

        const int WallSegments = 64;
        /// <summary>바닥 쿼드 여유 배율 — 소프트 경계·뜯김이 잘리지 않게 바깥 반경보다 크게 잡는다.</summary>
        const float FloorMargin = 1.15f;

        private MeshRenderer floorR, wallOuterR, wallInnerR;
        private MaterialPropertyBlock mpb;
        private float elapsed;
        private bool playing;
        private Vector3 basePos;   // 보정 전 기준 위치(궤도 중심) — 카메라 보정은 여기서 출발한다

        // 프리셋 바인더가 밀어넣는 값
        public float FloorInner { get => floorInner; set => floorInner = Mathf.Max(0.05f, value); }
        public float FloorOuter { get => floorOuter; set => floorOuter = Mathf.Max(0.1f, value); }
        public float WallRadius { get => wallRadius; set => wallRadius = Mathf.Max(0.1f, value); }
        public float WallThickness { get => wallThickness; set => wallThickness = Mathf.Max(0.02f, value); }
        public float WallHeight { get => wallHeight; set => wallHeight = Mathf.Max(0.1f, value); }
        public float GroundLift { get => groundLift; set => groundLift = Mathf.Max(0f, value); }
        public float BandSoft { get => bandSoft; set => bandSoft = Mathf.Clamp(value, 0.02f, 0.6f); }
        public float TearAmount { get => tearAmount; set => tearAmount = Mathf.Clamp01(value); }
        public float InnerSpinMul { get => innerSpinMul; set => innerSpinMul = Mathf.Clamp(value, -2f, 2f); }
        public float FloorAlphaMul { get => floorAlphaMul; set => floorAlphaMul = Mathf.Clamp(value, 0f, 2f); }
        public float FadeIn { get => fadeIn; set => fadeIn = Mathf.Max(0.02f, value); }
        public float Hold { get => hold; set => hold = Mathf.Max(0f, value); }
        public float FadeOut { get => fadeOut; set => fadeOut = Mathf.Max(0.05f, value); }
        public Vector3 SpawnOffset { get => spawnOffset; set => spawnOffset = value; }
        public Material WindMaterial { get => windMaterial; set => windMaterial = value; }
        public float CameraTilt { get => cameraTilt; set => cameraTilt = Mathf.Clamp01(value); }
        public bool AutoCenter { get => autoCenter; set => autoCenter = value; }
        public float CenterShift { get => centerShift; set => centerShift = Mathf.Clamp(value, -2f, 2f); }

        public void Play(SkillEffectContext ctx)
        {
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;

            if (ctx != null)
            {
                Transform socket = ctx.SocketTransform;
                transform.position = (socket != null ? socket.position : ctx.SpawnPosition) + spawnOffset;
            }
            basePos = transform.position;
            transform.rotation = Quaternion.identity;   // 고리형이라 방위 무의미 — 카메라 보정이 매 프레임 잡는다

            Build();
            PushStaticParams();

            elapsed = 0f;
            playing = true;
            SetEnvelopeAlpha(0f);

            Destroy(gameObject, fadeIn + hold + fadeOut + 0.1f);

            if (logLifecycle)
                Debug.Log("[JcWindRing] Play  origin=" + transform.position.ToString("F2"), this);
        }

        private void Update()
        {
            if (!playing) return;
            elapsed += Time.deltaTime;

            float a =
                elapsed < fadeIn ? elapsed / fadeIn :
                elapsed < fadeIn + hold ? 1f :
                1f - Mathf.Clamp01((elapsed - fadeIn - hold) / fadeOut);
            SetEnvelopeAlpha(Mathf.Clamp01(a));
        }

        /// <summary>
        /// ★뷰 벡터 보정 — 매 프레임 현재 카메라 기준으로 재계산해 카메라 이동·회전을 자동 추종한다.
        /// ① 틸트: 링 평면 법선을 위쪽 ↔ 카메라 방향 사이로 기울여 화면상 타원을 원에 접근시킨다.
        /// ② 중심: 원근이 토러스의 시각 중심을 화면 위로 밀어내는 것을, 시선 수평 방향 이동으로 상쇄.
        ///    자동 모드는 가까운/먼 끝의 화면 투영 중점이 기하 중심 투영과 일치하도록 역산(반복 2회).
        /// 직하 카메라에서는 시선 수평 성분이 0이라 중심 보정이 자연 감쇠한다(퇴화 가드).
        /// </summary>
        private void LateUpdate()
        {
            if (!playing) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            // ① 틸트
            Vector3 toCam = -cam.transform.forward;
            Vector3 normal = Vector3.Slerp(Vector3.up, toCam, cameraTilt).normalized;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);

            // ② 중심 보정 — 시선 수평 방향
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            float horiz = fwd.magnitude;
            if (horiz < 1e-4f) { transform.position = basePos; return; }
            Vector3 dir = fwd / horiz;

            float shift = autoCenter ? SolveAutoShift(cam, dir, normal) : centerShift * horiz;
            transform.position = basePos + dir * shift;
        }

        /// <summary>화면상 시각 중심 정합 보정량 역산 — 링 평면 위 시선 방향 지름의 투영 중점을 기하 중심에 맞춘다.</summary>
        private float SolveAutoShift(Camera cam, Vector3 dir, Vector3 normal)
        {
            // 대표 반경: 실루엣을 지배하는 벽 반경
            float r = wallRadius;
            // 링 평면 위에서의 시선 방향 축(틸트 반영)
            Vector3 axis = (dir - normal * Vector3.Dot(dir, normal));
            if (axis.sqrMagnitude < 1e-6f) return 0f;
            axis.Normalize();

            Vector2 targetS = cam.WorldToScreenPoint(basePos);
            float shift = 0f;
            for (int it = 0; it < 2; it++)
            {
                Vector3 c = basePos + dir * shift;
                Vector2 sNear = cam.WorldToScreenPoint(c - axis * r);
                Vector2 sFar = cam.WorldToScreenPoint(c + axis * r);
                Vector2 err = targetS - (sNear + sFar) * 0.5f;

                // 월드 dir 1m가 만드는 화면 벡터로 픽셀 오차를 월드 보정량으로 환산
                Vector2 perMeter = (Vector2)cam.WorldToScreenPoint(c + dir) - (Vector2)cam.WorldToScreenPoint(c);
                float len2 = perMeter.sqrMagnitude;
                if (len2 < 1e-4f) break;
                shift += Vector2.Dot(err, perMeter) / len2;
            }
            return Mathf.Clamp(shift, -3f, 3f);
        }

        /// <summary>자식 3종(바닥·외벽·내벽)을 생성/갱신한다. 프리팹에는 없고 재생 시 만들어진다.</summary>
        private void Build()
        {
            float half = floorOuter * FloorMargin;
            floorR = EnsureChild("Floor", BuildFloorQuad(half), new Vector3(0f, groundLift, 0f));
            wallOuterR = EnsureChild("WallOuter", BuildCylinder(wallRadius, wallHeight), Vector3.zero);
            wallInnerR = EnsureChild("WallInner", BuildCylinder(Mathf.Max(wallRadius - wallThickness, 0.05f), wallHeight), Vector3.zero);
        }

        private MeshRenderer EnsureChild(string name, Mesh mesh, Vector3 localPos)
        {
            Transform t = transform.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                t = go.transform;
                t.SetParent(transform, false);
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
            }
            t.localPosition = localPos;
            var mf = t.GetComponent<MeshFilter>();
            if (mf.sharedMesh != null) Destroy(mf.sharedMesh);
            mf.sharedMesh = mesh;
            var mr = t.GetComponent<MeshRenderer>();
            mr.sharedMaterial = windMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        /// <summary>xz 평면 쿼드 — uv 0~1, 셰이더가 극좌표 도넛을 그린다.</summary>
        private static Mesh BuildFloorQuad(float half)
        {
            var m = new Mesh { name = "WindFloor" };
            m.vertices = new[]
            {
                new Vector3(-half, 0f, -half), new Vector3(half, 0f, -half),
                new Vector3(-half, 0f,  half), new Vector3(half, 0f,  half)
            };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>옆면만 있는 열린 원통 — u = 둘레(0~1, 셰이더가 단위원 사상으로 이음새 제거), v = 높이(0~1).</summary>
        private static Mesh BuildCylinder(float radius, float height)
        {
            var m = new Mesh { name = "WindWall" };
            int ring = WallSegments + 1;
            var verts = new Vector3[ring * 2];
            var uvs = new Vector2[ring * 2];
            for (int i = 0; i < ring; i++)
            {
                float u = (float)i / WallSegments;
                float a = u * Mathf.PI * 2f;
                float x = Mathf.Sin(a) * radius;
                float z = Mathf.Cos(a) * radius;
                verts[i] = new Vector3(x, 0f, z);
                verts[i + ring] = new Vector3(x, height, z);
                uvs[i] = new Vector2(u, 0f);
                uvs[i + ring] = new Vector2(u, 1f);
            }
            var tris = new int[WallSegments * 6];
            for (int i = 0; i < WallSegments; i++)
            {
                int b = i * 6;
                tris[b] = i; tris[b + 1] = i + ring; tris[b + 2] = i + 1;
                tris[b + 3] = i + 1; tris[b + 4] = i + ring; tris[b + 5] = i + ring + 1;
            }
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        /// <summary>렌더러별 고정 파라미터 — 모드·밴드(지오메트리 정규화)·회전 배속.</summary>
        private void PushStaticParams()
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            float half = floorOuter * FloorMargin;

            // 바닥 도넛: 밴드 = 반경 정규(0~1). 뜯김은 바깥 가장자리에.
            Push(floorR, mode: 0f, spinMul: 1f,
                 bandInner: floorInner / half, bandOuter: floorOuter / half,
                 soft: bandSoft, tear: tearAmount);

            // 벽: 밴드 = 높이(0~1). 하단은 접지 소프트, 상단(소멸)은 뜯김.
            Push(wallOuterR, mode: 1f, spinMul: 1f,
                 bandInner: 0.06f, bandOuter: 0.85f, soft: bandSoft, tear: tearAmount);
            Push(wallInnerR, mode: 1f, spinMul: innerSpinMul,
                 bandInner: 0.06f, bandOuter: 0.85f, soft: bandSoft, tear: tearAmount);
        }

        private void Push(MeshRenderer r, float mode, float spinMul, float bandInner, float bandOuter, float soft, float tear)
        {
            if (r == null) return;
            r.GetPropertyBlock(mpb);
            mpb.SetFloat("_Mode", mode);
            mpb.SetFloat("_SpinMul", spinMul);
            mpb.SetFloat("_BandInner", bandInner);
            mpb.SetFloat("_BandOuter", bandOuter);
            mpb.SetFloat("_BandSoft", soft);
            mpb.SetFloat("_TearAmount", tear);
            r.SetPropertyBlock(mpb);
        }

        private void SetEnvelopeAlpha(float a)
        {
            SetAlpha(floorR, a * floorAlphaMul);
            SetAlpha(wallOuterR, a);
            SetAlpha(wallInnerR, a);
        }

        private void SetAlpha(MeshRenderer r, float a)
        {
            if (r == null || mpb == null) return;
            r.GetPropertyBlock(mpb);
            mpb.SetFloat("_Alpha", a);
            r.SetPropertyBlock(mpb);
        }
    }
}
