using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 블랙불릿 AimShot — 스타버스트 빌보드 쿼드(탄두 머리 / 머즐 플래시 / 탄착 피격 공용).
    /// KStarBurst 셰이더를 입은 절차 쿼드 1장. 오케스트레이터가 위치·크기·페이드를 주입하는 수동 요소.
    ///
    /// ★소켓 자식이 되지 않는다. 위치는 월드 좌표로 대입받고, 스케일은 자기 localScale로만 관리한다.
    /// (소켓 lossyScale 1.6~200배가 크기에 곱해지는 것을 원천 차단)
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class KStarBurstQuad : MonoBehaviour
    {
        [Header("빌보드")]
        [Tooltip("비워두면 Camera.main. 매 프레임 카메라를 바라본다.")]
        [SerializeField] private Camera viewCamera;
        [Tooltip("축 정렬 모드: 지정하면 이 방향을 쿼드의 가로축으로 삼는다(머즐 플래시의 총구 방향 정렬용).")]
        [SerializeField] private bool alignToAxis;

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _mpb;
        private Vector3 _axis = Vector3.right;
        private float _baseSize = 1f;

        private static readonly int IdFadeMul = Shader.PropertyToID("_FadeMul");
        private static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
        private static readonly int IdGlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int IdCoreColor = Shader.PropertyToID("_CoreColor");

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
            EnsureQuad();
            _baseSize = transform.localScale.x;
            SetVisible(false);
        }

        private void EnsureQuad()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh != null) return;
            // 중심 원점, 한 변 1의 쿼드. uv 0..1 (셰이더가 0.5를 중심으로 씀)
            var m = new Mesh { name = "KStarBurstQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f), new Vector3(0.5f,  0.5f, 0f)
            };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            m.RecalculateBounds();
            mf.sharedMesh = m;
        }

        public void SetVisible(bool visible)
        {
            if (_renderer != null) _renderer.enabled = visible;
        }

        /// <summary>월드 위치 대입. 소켓을 부모로 삼지 않으므로 스케일 오염이 없다.</summary>
        public void SetWorldPosition(Vector3 worldPos) => transform.position = worldPos;

        /// <summary>축 정렬용 방향(월드). alignToAxis가 켜져 있을 때 쿼드의 가로축이 된다.</summary>
        public void SetAxis(Vector3 worldAxis)
        {
            if (worldAxis.sqrMagnitude > 1e-8f) _axis = worldAxis.normalized;
        }

        /// <summary>월드 크기(한 변 기준 미터).</summary>
        public void SetSize(float worldSize) => transform.localScale = Vector3.one * Mathf.Max(0f, worldSize);

        /// <summary>Awake 시점 localScale을 기준으로 한 배율 지정.</summary>
        public void SetSizeMul(float mul) => transform.localScale = Vector3.one * Mathf.Max(0f, _baseSize * mul);

        public void SetFade(float fadeMul)
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdFadeMul, Mathf.Clamp01(fadeMul));
            _renderer.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// 테두리(글로우)·내부(코어) 색을 함께 밀어 넣는다. 발광은 이미 적용된 HDR 색을 받는다
        /// (KColorEval.Boost로 탈색 방지 처리된 값). MPB라 공유 머티리얼을 건드리지 않는다.
        /// </summary>
        public void SetColors(Color edge, Color inner)
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(IdGlowColor, edge);
            _mpb.SetColor(IdCoreColor, inner);
            _renderer.SetPropertyBlock(_mpb);
        }

        public void SetIntensity(float intensity)
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdIntensity, Mathf.Max(0f, intensity));
            _renderer.SetPropertyBlock(_mpb);
        }

        private void LateUpdate()
        {
            if (_renderer == null || !_renderer.enabled) return;
            Camera cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null) return;

            Vector3 toCam = cam.transform.position - transform.position;
            if (toCam.sqrMagnitude < 1e-8f) return;
            Vector3 normal = -toCam.normalized;   // 쿼드가 카메라를 바라보도록

            if (alignToAxis)
            {
                // 가로축을 _axis에 맞추고, 법선은 시선으로 유지 → 총구 방향으로 누운 수평 스파이크
                Vector3 up = Vector3.Cross(normal, _axis);
                if (up.sqrMagnitude < 1e-8f) { transform.rotation = Quaternion.LookRotation(normal); return; }
                transform.rotation = Quaternion.LookRotation(normal, up.normalized);
            }
            else
            {
                transform.rotation = Quaternion.LookRotation(normal, cam.transform.up);
            }
        }
    }
}
