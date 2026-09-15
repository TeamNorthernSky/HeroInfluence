using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace JC.Env
{
    [Serializable]
    public struct JcDofSettings
    {
        [Tooltip("Off는 DOF를 끕니다. Gaussian은 원거리 블러, Bokeh는 초점 앞뒤 블러입니다. 설정값은 DOF 프로파일 저장 대상입니다.")]
        public DepthOfFieldMode mode;
        [Tooltip("켜면 카메라 중앙 시선과 바닥의 교차 거리로 초점을 추적합니다. 끄면 수동 거리를 사용합니다.")]
        public bool autoFocus;
        [Tooltip("자동 초점 기준 바닥의 월드 Y 좌표입니다. 0은 월드 원점 높이입니다.")]
        public float groundY;
        [Tooltip("Gaussian 자동 초점 거리에서 블러 시작점까지의 오프셋(월드 단위). 음수는 초점보다 앞에서 시작합니다.")]
        public float gaussianStartOffset;
        [Tooltip("Gaussian 자동 초점 거리에서 최대 블러 지점까지의 오프셋(월드 단위). 시작 오프셋보다 최소 0.01 크게 보정합니다.")]
        public float gaussianEndOffset;
        [Tooltip("수동 Gaussian 블러 시작 거리(카메라 기준 월드 단위). 최소 0. 자동 추적 실패 시에도 이 값을 사용합니다.")]
        [Min(0f)] public float gaussianStart;
        [Tooltip("수동 Gaussian 최대 블러 거리(카메라 기준 월드 단위). 시작 거리보다 최소 0.01 크게 보정합니다.")]
        [Min(0.01f)] public float gaussianEnd;
        [Tooltip("Gaussian 최대 블러 반경. URP 지원 범위 0.5~1.5이며 클수록 흐려집니다. 1 초과는 샘플링 자국이 생길 수 있습니다.")]
        [Range(0.5f, 1.5f)] public float gaussianMaxRadius;
        [Tooltip("Gaussian 샘플링 품질을 높여 블러의 깜빡임을 줄입니다. 렌더링 비용이 증가할 수 있습니다.")]
        public bool highQualitySampling;
        [Tooltip("수동 Bokeh 초점 거리(카메라 기준 월드 단위). 최소 0.1. 자동 추적 실패 시에도 이 값을 사용합니다.")]
        [Min(0.1f)] public float focusDistance;
        [Tooltip("Bokeh 조리개 값(f-number), 범위 1~32. 작을수록 선명한 깊이 범위가 좁아지고 블러가 강해집니다.")]
        [Range(1f, 32f)] public float aperture;
        [Tooltip("Bokeh 렌즈 초점거리(mm), 범위 1~300. 클수록 선명한 깊이 범위가 좁아집니다. 초점 대상까지의 거리와는 별개입니다.")]
        [Range(1f, 300f)] public float focalLength;
        [Tooltip("Bokeh 조리개 날개 개수, 범위 3~9. 빛 번짐의 다각형 모양을 바꿉니다.")]
        [Range(3, 9)] public int bladeCount;
        [Tooltip("Bokeh 조리개 날개 곡률, 범위 0~1. 0은 다각형 모양을 강조하고 1은 원형입니다.")]
        [Range(0f, 1f)] public float bladeCurvature;
        [Tooltip("Bokeh 조리개 모양 회전각(도), 범위 -180~180. 0은 회전 없음입니다.")]
        [Range(-180f, 180f)] public float bladeRotation;

        public static JcDofSettings Default => new JcDofSettings
        {
            mode = DepthOfFieldMode.Gaussian, autoFocus = true,
            gaussianStartOffset = 1.5f, gaussianEndOffset = 6f,
            gaussianStart = 12f, gaussianEnd = 19f, gaussianMaxRadius = 1.5f,
            highQualitySampling = true, focusDistance = 10f, aperture = 1.2f,
            focalLength = 150f, bladeCount = 5, bladeCurvature = 1f
        };

        public JcDofSettings Sanitized()
        {
            var s = this;
            if (s.mode < DepthOfFieldMode.Off || s.mode > DepthOfFieldMode.Bokeh) s.mode = DepthOfFieldMode.Off;
            s.groundY = Finite(s.groundY, 0f);
            s.gaussianStartOffset = Finite(s.gaussianStartOffset, 1.5f);
            s.gaussianEndOffset = Mathf.Max(s.gaussianStartOffset + 0.01f, Finite(s.gaussianEndOffset, 6f));
            s.gaussianStart = Mathf.Max(0f, Finite(s.gaussianStart, 12f));
            s.gaussianEnd = Mathf.Max(s.gaussianStart + 0.01f, Finite(s.gaussianEnd, 19f));
            s.gaussianMaxRadius = Mathf.Clamp(Finite(s.gaussianMaxRadius, 1.5f), 0.5f, 1.5f);
            s.focusDistance = Mathf.Max(0.1f, Finite(s.focusDistance, 10f));
            s.aperture = Mathf.Clamp(Finite(s.aperture, 1.2f), 1f, 32f);
            s.focalLength = Mathf.Clamp(Finite(s.focalLength, 150f), 1f, 300f);
            s.bladeCount = Mathf.Clamp(s.bladeCount, 3, 9);
            s.bladeCurvature = Mathf.Clamp01(Finite(s.bladeCurvature, 1f));
            s.bladeRotation = Mathf.Clamp(Finite(s.bladeRotation, 0f), -180f, 180f);
            return s;
        }

        private static float Finite(float value, float fallback)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }

    [CreateAssetMenu(menuName = "JC/Environment/DOF 프로파일", fileName = "DOF_Miniature")]
    public sealed class JcDofProfile : ScriptableObject
    {
        [Tooltip("DOF 효과와 초점 추적 설정입니다. 카메라·Volume 연결 및 매 프레임 계산한 거리는 저장하지 않습니다.")]
        public JcDofSettings settings = JcDofSettings.Default;

        private void OnValidate() => settings = settings.Sanitized();
    }
}
