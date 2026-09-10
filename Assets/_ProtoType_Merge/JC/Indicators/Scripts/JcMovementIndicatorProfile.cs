using System;
using UnityEngine;

namespace JC.Indicators
{
    [Serializable]
    public struct JcMovementIndicatorSettings
    {
        [Tooltip("이동 가능한 경로와 도달 가능한 도착 표식의 색입니다. 색상의 알파에 공통 불투명도가 곱해집니다.")]
        public Color reachableColor;
        [Tooltip("이동력을 초과한 경로와 도달 불가능한 도착 표식의 색입니다. 기본은 적색이며 임의 색상을 사용할 수 있습니다.")]
        public Color unreachableColor;
        [Range(0, 1), Tooltip("선과 도착 문양의 불투명도입니다. 0은 완전 투명, 1은 색상 알파 그대로 표시합니다.")]
        public float opacity;
        [Range(.1f, 1), Tooltip("셀 한 변 대비 도착 문양 크기입니다. 클릭 판정 영역은 바꾸지 않습니다.")]
        public float markerSize;
        [Range(.01f, .3f), Tooltip("문양 한 변 대비 사각 테두리 두께입니다.")]
        public float borderWidth;
        [Range(0, .5f), Tooltip("문양 한 변 대비 바깥 모서리 반지름입니다. 0은 직각입니다.")]
        public float cornerRadius;
        [Range(.02f, .45f), Tooltip("문양 한 변 대비 중앙 링의 바깥 반지름입니다.")]
        public float ringRadius;
        [Range(.005f, .2f), Tooltip("문양 한 변 대비 중앙 링 두께입니다. 반지름보다 클 때는 원판이 됩니다.")]
        public float ringWidth;
        [Range(0, .3f), Tooltip("문양 한 변 대비 중앙 점 반지름입니다. 0은 점을 숨깁니다.")]
        public float dotRadius;
        [Min(0), Tooltip("타일 표면 위로 띄우는 높이입니다. 선과 도착 문양에 함께 적용합니다. 0에도 겹침 방지 여유가 있습니다.")]
        public float floatHeight;
        [Min(0), Tooltip("상하 흔들림 진폭입니다. 0은 정지한 부유 상태입니다. 지면 아래로 내려가지 않도록 높이 이내로 제한됩니다.")]
        public float bobAmplitude;
        [Min(0), Tooltip("상하 흔들림의 초당 반복 횟수입니다. 0은 흔들림을 멈춥니다.")]
        public float bobFrequency;
        [Tooltip("지면 그림자 색입니다. 알파와 그림자 농도가 곱해집니다. 광원 설정과 독립적입니다.")]
        public Color shadowColor;
        [Range(0, 1), Tooltip("그림자 농도입니다. 0이면 그림자를 숨깁니다. 본체 불투명도도 함께 반영합니다.")]
        public float shadowOpacity;
        [Min(0), Tooltip("본체 바로 아래 지점에서 그림자를 옮기는 수평 거리입니다. 월드 단위이며 0이면 바로 아래입니다.")]
        public float shadowDistance;
        [Range(0, 360), Tooltip("그림자 방향입니다. 월드 XZ 평면 기준 0도는 +X, 90도는 +Z입니다. 카메라/광원 회전과 독립적입니다.")]
        public float shadowAngle;
        [Range(0, .2f), Tooltip("그림자 가장자리의 번짐 폭입니다. 월드 단위이며 0은 선명한 그림자입니다.")]
        public float shadowSoftness;
        [Range(.01f, .4f), Tooltip("점선의 전체 굵기입니다. 카메라 확대 시 타일과 함께 확대됩니다.")]
        public float lineWidth;
        [Min(.01f), Tooltip("점선 한 조각의 길이입니다. 간격과 함께 밀집도를 정합니다.")]
        public float dashLength;
        [Min(.005f), Tooltip("점선 조각 사이 빈 공간 길이입니다. 작을수록 조밀해집니다.")]
        public float dashGap;
        [Min(0), Tooltip("도착지점을 향해 흐르는 초당 월드 거리입니다. 0은 점선을 정지합니다.")]
        public float flowSpeed;
        [ColorUsage(true, true), Tooltip("고정 문양 위로 지나가는 밝은 띠의 색입니다. HDR 값을 사용할 수 있고 알파는 강도에 곱해집니다.")]
        public Color highlightColor;
        [Range(0, 8), Tooltip("원형 파동 하이라이트의 밝기입니다. 0은 하이라이트를 끕니다. 투명 여백에는 표시되지 않습니다.")]
        public float highlightStrength;
        [Range(.005f, .4f), Tooltip("문양 한 변 대비 원형 하이라이트 띠 폭입니다.")]
        public float waveWidth;
        [Min(0), Tooltip("중심으로 수렴하는 초당 거리입니다. 문양 한 변을 1로 계산합니다. 0은 파동을 멈춥니다.")]
        public float waveSpeed;
        [Min(.05f), Tooltip("새 파동을 시작하는 간격(초)입니다. 중심에 먼저 도착하면 다음 주기까지 쉽니다. 너무 짧으면 도착 전에 다시 시작합니다.")]
        public float wavePeriod;

        public static JcMovementIndicatorSettings Default => new JcMovementIndicatorSettings
        {
            reachableColor = Color.green, unreachableColor = Color.red, opacity = .8f,
            markerSize = .78f, borderWidth = .145f, cornerRadius = .16f,
            ringRadius = .255f, ringWidth = .095f, dotRadius = .095f,
            floatHeight = .12f, bobAmplitude = .012f, bobFrequency = .65f,
            shadowColor = new Color(.025f, .045f, .045f, 1), shadowOpacity = .32f,
            shadowDistance = .09f, shadowAngle = 225, shadowSoftness = .035f,
            lineWidth = .075f, dashLength = .18f, dashGap = .105f, flowSpeed = .65f,
            highlightColor = new Color(.8f, 1, .86f, 1), highlightStrength = 1.2f,
            waveWidth = .085f, waveSpeed = .55f, wavePeriod = 1.8f
        };

        public JcMovementIndicatorSettings Sanitized()
        {
            var s = this;
            s.opacity = Mathf.Clamp01(s.opacity);
            s.markerSize = Mathf.Clamp(s.markerSize, .1f, 1);
            s.borderWidth = Mathf.Clamp(s.borderWidth, .01f, .3f);
            s.cornerRadius = Mathf.Clamp(s.cornerRadius, 0, .5f);
            s.ringRadius = Mathf.Clamp(s.ringRadius, .02f, .45f);
            s.ringWidth = Mathf.Clamp(s.ringWidth, .005f, .2f);
            s.dotRadius = Mathf.Clamp(s.dotRadius, 0, .3f);
            s.floatHeight = Mathf.Max(0, s.floatHeight);
            s.bobAmplitude = Mathf.Clamp(s.bobAmplitude, 0, s.floatHeight);
            s.bobFrequency = Mathf.Max(0, s.bobFrequency);
            s.shadowOpacity = Mathf.Clamp01(s.shadowOpacity);
            s.shadowDistance = Mathf.Max(0, s.shadowDistance);
            s.shadowSoftness = Mathf.Clamp(s.shadowSoftness, 0, .2f);
            s.shadowAngle = Mathf.Repeat(s.shadowAngle, 360);
            s.lineWidth = Mathf.Clamp(s.lineWidth, .01f, .4f);
            s.dashLength = Mathf.Max(.01f, s.dashLength);
            s.dashGap = Mathf.Max(.005f, s.dashGap);
            s.flowSpeed = Mathf.Max(0, s.flowSpeed);
            s.highlightStrength = Mathf.Clamp(s.highlightStrength, 0, 8);
            s.waveWidth = Mathf.Clamp(s.waveWidth, .005f, .4f);
            s.waveSpeed = Mathf.Max(0, s.waveSpeed);
            s.wavePeriod = Mathf.Max(.05f, s.wavePeriod);
            return s;
        }
    }

    [CreateAssetMenu(menuName = "JC/Indicators/이동 인디케이터 프로파일")]
    public sealed class JcMovementIndicatorProfile : ScriptableObject
    {
        [Tooltip("저장된 이동 인디케이터 기본값입니다. 씬의 MovementIndicator에서 초안을 조절한 뒤 캡처·저장합니다.")]
        public JcMovementIndicatorSettings settings = JcMovementIndicatorSettings.Default;
    }
}
