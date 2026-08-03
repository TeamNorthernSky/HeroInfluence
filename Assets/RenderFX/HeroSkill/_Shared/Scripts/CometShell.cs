using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// Taosenaiyo 요소: 유성 구체를 감싸는 혜성 실루엣(셰이더 JC/VFX/TaoComet).
    /// ★리본 메시: 부모(투사체)의 위치 히스토리를 샘플링해 궤적을 따라 휘는 꼬리를 생성 —
    ///   포물선 접선 직선이 아니라 트레일처럼 자연스럽게 곡선. 폭 방향은 카메라를 향해 빌보드.
    /// uv.y = 1(머리)→0(꼬리 끝)으로 정규화해 셰이더의 머리 글로우/꼬리 감쇠가 리본 전장에 맵핑됨.
    /// 페이드/표시는 ProjectileVfx.fadeRenderers(_FadeMul MPB)에 편승. Show 순간이동은 자동 리셋.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class CometShell : MonoBehaviour
    {
        [Tooltip("혜성 꼬리 최대 길이(m, 궤적 호길이). 이보다 오래된 샘플은 버림")]
        [SerializeField] private float length = 1.4f;
        [Tooltip("혜성 폭(m)")]
        [SerializeField] private float width = 0.5f;
        [Tooltip("궤적 샘플 최소 간격(m). 작을수록 곡선이 매끈(정점 수 증가)")]
        [Range(0.01f, 0.3f)] [SerializeField] private float sampleSpacing = 0.035f;
        [Tooltip("한 프레임 이동이 이보다 크면 순간이동(Show 재배치)으로 보고 꼬리 리셋(m)")]
        [SerializeField] private float teleportThreshold = 1.5f;
        [Tooltip("리본이 이 길이 미만이면 그리지 않음(발사 직후 압축 글리치 방지, m)")]
        [SerializeField] private float minVisibleLength = 0.18f;

        private readonly List<Vector3> _trail = new List<Vector3>();   // [0]=최신 샘플(머리 뒤)
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<Color> _cols = new List<Color>();
        private readonly List<int> _tris = new List<int>();
        private Mesh _mesh;
        private Camera _cam;
        private Vector3 _prevParentPos;

        /// <summary>크기 런타임 주입(프리셋 라이브 프리뷰용).</summary>
        public void SetSize(float newLength, float newWidth)
        {
            length = Mathf.Max(0.1f, newLength);
            width = Mathf.Max(0.02f, newWidth);
        }

        private void Awake()
        {
            _mesh = new Mesh { name = "CometRibbon" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        private void OnEnable()
        {
            _trail.Clear();
            if (_mesh) _mesh.Clear();
            if (transform.parent) _prevParentPos = transform.parent.position;
        }

        private void LateUpdate()
        {
            var parent = transform.parent;
            if (parent == null) return;

            Vector3 pp = parent.position;
            Vector3 delta = pp - _prevParentPos;
            _prevParentPos = pp;
            if (delta.magnitude > teleportThreshold) _trail.Clear();   // Show 재배치 등 순간이동 → 꼬리 리셋

            // 궤적 샘플 축적(머리 제외, 간격 기준)
            if (_trail.Count == 0 || (pp - _trail[0]).magnitude >= sampleSpacing)
                _trail.Insert(0, pp);

            // 호길이 기준 트림
            float acc = (pp - (_trail.Count > 0 ? _trail[0] : pp)).magnitude;
            for (int i = 1; i < _trail.Count; i++)
            {
                acc += (_trail[i - 1] - _trail[i]).magnitude;
                if (acc > length) { _trail.RemoveRange(i, _trail.Count - i); break; }
            }

            BuildRibbon(pp);
        }

        /// <summary>머리(현재 위치)+궤적 샘플로 카메라 빌보드 리본 생성. 로컬 = 월드 - 부모 위치.</summary>
        private void BuildRibbon(Vector3 head)
        {
            transform.rotation = Quaternion.identity;   // 정점을 부모 기준 월드 오프셋으로 쓰기 위한 고정
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;

            if (_trail.Count < 1) { _mesh.Clear(); return; }

            // 포인트 목록: [머리, 샘플...] + 누적 호길이
            int n = _trail.Count + 1;
            if (n < 2) { _mesh.Clear(); return; }
            if (_cam == null) _cam = Camera.main;
            Vector3 camPos = _cam ? _cam.transform.position : head + Vector3.back * 5f;

            float total = 0f;
            var cum = new float[n];
            Vector3 Pt(int i) => i == 0 ? head : _trail[i - 1];
            for (int i = 1; i < n; i++)
            {
                total += (Pt(i - 1) - Pt(i)).magnitude;
                cum[i] = total;
            }
            // 짧은 리본은 그리지 않음: 발사 직후/정지 시 전체 그라데이션이 압축돼 흰 덩어리로 보이는 글리치 방지
            if (total < minVisibleLength) { _mesh.Clear(); return; }
            // 짧을 때 그라데이션 압축 완화: 최소 정규화 길이 이하로는 늘려서 머리 쪽 구간만 보이게
            float norm = Mathf.Max(total, length * 0.5f);

            _verts.Clear(); _uvs.Clear(); _cols.Clear(); _tris.Clear();
            float half = width * 0.5f;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = Pt(i);
                // 리본 실제 끝(잘린 단면)에서 부드럽게 소멸 — 머리 글로우/꼬리가 단면에서 하드컷되는 것 방지
                float endFade = Mathf.Clamp01((total - cum[i]) / Mathf.Max(total * 0.25f, 1e-4f));
                // 접선(중앙차분) + 카메라 빌보드 폭 방향
                Vector3 dir = (Pt(Mathf.Max(i - 1, 0)) - Pt(Mathf.Min(i + 1, n - 1)));
                if (dir.sqrMagnitude < 1e-8f) dir = Vector3.up;
                Vector3 right = Vector3.Cross(dir.normalized, (p - camPos).normalized);
                if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
                right = right.normalized * half;

                Vector3 local = p - head;               // 부모(=머리) 기준
                float v = 1f - cum[i] / norm;           // 1=머리 → 0=꼬리 끝
                var vc = new Color(1f, 1f, 1f, endFade);
                _verts.Add(local - right); _uvs.Add(new Vector2(0f, v)); _cols.Add(vc);
                _verts.Add(local + right); _uvs.Add(new Vector2(1f, v)); _cols.Add(vc);
                if (i > 0)
                {
                    int b = (i - 1) * 2;
                    _tris.Add(b); _tris.Add(b + 2); _tris.Add(b + 1);
                    _tris.Add(b + 1); _tris.Add(b + 2); _tris.Add(b + 3);
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_cols);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
        }
    }
}
