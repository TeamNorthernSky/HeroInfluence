using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JC.Env
{
    /// <summary>
    /// 미니어처(틸트시프트풍) DOF 초점 추적기 (P4).
    ///
    /// 교수님 지시: 카메라 중심에서 바깥으로 갈수록 블러 — 사실적 피사계심도가 아니라 「미니어처 효과」가 목적.
    /// 쿼터뷰 고정 피치라 화면 위쪽 = 먼 곳이므로 거리 기반 DOF 로 근사가 잘 먹는다.
    /// 문제는 줌(카메라 높이 3~8)에 따라 초점 거리가 바뀐다는 것 — 그래서 매 프레임
    /// 「화면 중앙 시선이 바닥(groundY)을 만나는 거리」를 초점으로 잡고 Volume 의 DOF 값을 갱신한다.
    ///
    /// Volume 은 <c>volume.profile</c>(인스턴스 사본)에 쓴다 — 에셋(sharedProfile)은 정적 설정(모드·최대 반경)만 소유하고
    /// 프레임마다 바뀌는 초점 거리는 사본에만 남는다(에셋 dirty 방지, 저장 규격 준수).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Volume))]
    public class JcDofFocusTracker : MonoBehaviour
    {
        [Tooltip("추적할 카메라. 비우면 Camera.main.")]
        public Camera targetCamera;

        [Tooltip("초점을 잡을 바닥 높이(Y).")]
        public float groundY = 0f;

        [Header("Gaussian 모드 — 초점 거리 기준 오프셋")]
        [Tooltip("선명 구간이 끝나고 블러가 시작되는 거리 = 초점 + 이 값. 화면 중앙~위쪽 1/3 이 선명하게 남는 정도.")]
        public float gaussianStartOffset = 2.5f;
        [Tooltip("블러가 최대가 되는 거리 = 초점 + 이 값.")]
        public float gaussianEndOffset = 9f;

        [Header("Bokeh 모드")]
        [Tooltip("Bokeh 는 앞·뒤 양쪽이 흐려진다(레퍼런스의 하단 블러까지 재현). 초점 거리는 자동, 나머지는 프로파일 값.")]
        public bool driveBokehFocus = true;

        [Header("읽기 전용")]
        public float lastFocusDistance;

        private Volume _volume;
        private DepthOfField _dof;

        private void OnEnable()
        {
            _volume = GetComponent<Volume>();
            _dof = null;
        }

        private void LateUpdate()
        {
            // 새 설정 컨트롤러가 연결된 Volume은 그쪽이 초점과 효과를 함께 소유합니다.
            // 다른 씬의 기존 추적기 및 컨트롤러 비활성 시에는 기존 동작을 유지합니다.
            if (JcDofController.IsDriving(_volume)) return;
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null || _volume == null) return;
            // 컨트롤러 해제/프로파일 교체 후 이전 사본을 계속 쓰지 않습니다.
            if (!_volume.profile.TryGet(out _dof)) return;

            // 화면 중앙 시선 ∩ 바닥 평면
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            var plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            float d;
            if (!plane.Raycast(ray, out d) || d <= 0f) d = Vector3.Distance(cam.transform.position, new Vector3(cam.transform.position.x, groundY, cam.transform.position.z));
            lastFocusDistance = d;

            if (_dof.mode.value == DepthOfFieldMode.Gaussian)
            {
                _dof.gaussianStart.Override(d + gaussianStartOffset);
                _dof.gaussianEnd.Override(d + gaussianEndOffset);
            }
            else if (_dof.mode.value == DepthOfFieldMode.Bokeh && driveBokehFocus)
            {
                _dof.focusDistance.Override(d);
            }
        }
    }
}
