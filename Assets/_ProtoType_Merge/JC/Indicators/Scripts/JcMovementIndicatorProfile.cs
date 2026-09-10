using System;
using UnityEngine;

namespace JC.Indicators
{
    public enum JcDashFlickerDirection
    {
        [InspectorName("도착 방향")] TowardDestination,
        [InspectorName("출발 방향")] TowardStart
    }

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
        [Tooltip("공통 부유 높이에 더하는 도착 문양 밑면의 높이 오프셋(월드 단위)입니다. 0은 기존 높이, 음수는 낮춤이며 기준면 아래로 내려가지 않습니다. 그림자와 클릭 판정은 그대로 유지합니다.")]
        public float markerHeightOffset;
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
        [Min(0), Tooltip("중심 수렴 하이라이트가 현재 도착 가능/불가능 색과 하이라이트 색 사이를 선형 왕복하는 전체 주기(초)입니다. 2는 편도 1초이며, 0은 기존 고정색 가산 표시입니다. 파동 발생 주기·투명도와 독립적입니다.")]
        public float highlightColorCyclePeriod;
        [Range(0, 8), Tooltip("원형 파동 하이라이트의 밝기입니다. 0은 하이라이트를 끕니다. 투명 여백에는 표시되지 않습니다.")]
        public float highlightStrength;
        [Range(.005f, .4f), Tooltip("문양 한 변 대비 원형 하이라이트 띠 폭입니다.")]
        public float waveWidth;
        [Min(0), Tooltip("중심으로 수렴하는 초당 거리입니다. 문양 한 변을 1로 계산합니다. 0은 파동을 멈춥니다.")]
        public float waveSpeed;
        [Min(.05f), Tooltip("새 파동을 시작하는 간격(초)입니다. 중심에 먼저 도착하면 다음 주기까지 쉽니다. 너무 짧으면 도착 전에 다시 시작합니다.")]
        public float wavePeriod;

        [SerializeField, HideInInspector] private int extensionVersion;
        [Range(.001f, .3f), Tooltip("문양 한 변 대비 실제 입체 두께입니다. 밑면의 부유 높이를 유지하며 위로 두꺼워집니다.")]
        public float markerThickness;
        [Range(0, .08f), Tooltip("문양 한 변 대비 윗면 베벨의 최대 폭입니다. 0은 각진 모서리이며 두께와 문양 간격에 맞춰 자동 제한됩니다.")]
        public float bevelWidth;
        [Range(4, 32), Tooltip("원과 모서리의 사분면당 분할 수입니다. 높을수록 매끄럽고 메시 정점 수가 늘어납니다. 짝수로 조절됩니다.")]
        public int curveSegments;
        [Range(0, 1), Tooltip("윗면에 대한 옆면의 밝기 비율입니다. 0은 검정, 1은 윗면과 같은 밝기이며 씬 광원과 독립적입니다.")]
        public float sideBrightness;
        [Min(0), Tooltip("각 점선 조각의 뒤에 남는 꼬리 길이(월드 단위)입니다. 0은 끔이며 실제 길이는 점선 간격의 95% 이내로 제한합니다. 경로 변경 시 새 경로를 따릅니다.")]
        public float dashTrailLength;
        [Range(0, 1), Tooltip("점선 꼬리의 불투명도입니다. 0은 끔이며 공통 불투명도와 상태 색상의 알파도 반영합니다.")]
        public float dashTrailOpacity;
        [Range(.2f, 4), Tooltip("꼬리 불투명도의 감쇠 지수입니다. 1은 선형 감쇠, 클수록 빠르게 투명해지고 작을수록 긴 구간이 밝게 남습니다. 굵기 감쇠와 독립적입니다.")]
        public float dashTrailFalloff;
        [Range(0, 4), Tooltip("꼬리 끝으로 갈수록 굵기를 줄이는 감쇠 지수입니다. 0은 일정한 굵기, 1은 선형으로 가늘어짐, 1보다 크면 더 빠르게 가늘어집니다. 불투명도 감쇠와 독립적이며 글로우도 좁아진 윤곽을 따릅니다.")]
        public float dashTrailWidthFalloff;
        [Range(0, .2f), Tooltip("점선과 꼬리 주변 빛 번짐의 폭(월드 단위)입니다. 0은 끔입니다. 씬 Bloom과 독립적이며 선 굵기와 그림자는 바꾸지 않습니다.")]
        public float dashGlowWidth;
        [Range(0, 1), Tooltip("점선과 꼬리 주변의 같은 색 빛 번짐 강도입니다. 0은 끔이며 높일수록 번짐이 선명해집니다.")]
        public float dashGlowStrength;
        [Range(0, 1), Tooltip("점선·꼬리·글로우가 흰색으로 반짝이는 최대 강도입니다. 0은 끔, 1은 가장 강할 때 흰색까지 변합니다. 기본보다 어두워지거나 채도가 높아지지 않으며 기본 색조·색상 설정과 알파를 유지합니다.")]
        public float dashFlickerStrength;
        [Min(0), Tooltip("흰색 반짝임 하나가 경로를 따라 진행하는 속도입니다. 클수록 빨라지고 0은 현재 위치에서 정지합니다. 마지막 지점의 원색 복귀가 끝난 뒤 다음 흐름을 시작합니다. 전환·복귀 배율과 점선 자체의 흐름 속도는 별도로 조절합니다.")]
        public float dashFlickerSpeed;
        [Range(.1f, 10), Tooltip("흰색으로 밝아지는 전환 곡선의 속도 배율입니다. 1은 기존 속도, 클수록 흰색에 빠르게 접근하고 작을수록 늦게 접근합니다. 패턴 진행 속도·원색 복귀 배율과 독립적이며 초 단위의 고정 시간은 아닙니다.")]
        public float dashFlickerRiseSpeed;
        [Range(.1f, 10), Tooltip("흰색 반짝임이 원래 색으로 돌아오는 곡선의 속도 배율입니다. 1은 기존 속도, 클수록 원색에 빠르게 접근하고 작을수록 반짝임이 오래 남습니다. 패턴 진행 속도·흰색 전환 배율과 독립적입니다.")]
        public float dashFlickerFallSpeed;
        [Tooltip("흰색 반짝임이 경로를 따라 진행하는 방향입니다. 기본은 도착 방향이며 출발 방향을 선택하면 반전합니다. 점선과 꼬리 자체의 이동 방향은 유지됩니다.")]
        public JcDashFlickerDirection dashFlickerDirection;

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
            waveWidth = .085f, waveSpeed = .55f, wavePeriod = 1.8f,
            extensionVersion = 6, markerThickness = .06f, bevelWidth = .012f, curveSegments = 16, sideBrightness = .55f,
            markerHeightOffset = 0, highlightColorCyclePeriod = 0,
            dashTrailLength = .07f, dashTrailOpacity = .45f, dashTrailFalloff = 1.5f, dashTrailWidthFalloff = 1,
            dashGlowWidth = .035f, dashGlowStrength = .25f,
            dashFlickerStrength = .08f, dashFlickerSpeed = 3, dashFlickerRiseSpeed = 1, dashFlickerFallSpeed = 1, dashFlickerDirection = JcDashFlickerDirection.TowardDestination
        };

        public JcMovementIndicatorSettings Sanitized()
        {
            var s = this;
            if (s.extensionVersion < 1)
            {
                var defaults = Default;
                s.markerThickness = defaults.markerThickness; s.bevelWidth = defaults.bevelWidth;
                s.curveSegments = defaults.curveSegments; s.sideBrightness = defaults.sideBrightness;
                s.extensionVersion = 1;
            }
            if (s.extensionVersion < 2)
            {
                s.markerHeightOffset = 0;
                s.extensionVersion = 2;
            }
            if (s.extensionVersion < 3)
            {
                s.highlightColorCyclePeriod = 0;
                s.extensionVersion = 3;
            }
            if (s.extensionVersion < 4)
            {
                var defaults = Default;
                s.dashTrailLength = defaults.dashTrailLength; s.dashTrailOpacity = defaults.dashTrailOpacity;
                s.dashTrailFalloff = defaults.dashTrailFalloff;
                s.dashGlowWidth = defaults.dashGlowWidth; s.dashGlowStrength = defaults.dashGlowStrength;
                s.dashFlickerStrength = defaults.dashFlickerStrength; s.dashFlickerSpeed = defaults.dashFlickerSpeed;
                s.extensionVersion = 4;
            }
            if (s.extensionVersion < 5)
            {
                s.dashTrailWidthFalloff = 1;
                s.dashFlickerDirection = JcDashFlickerDirection.TowardDestination;
                s.extensionVersion = 5;
            }
            if (s.extensionVersion < 6)
            {
                s.dashFlickerRiseSpeed = 1; s.dashFlickerFallSpeed = 1;
                s.extensionVersion = 6;
            }
            s.dashFlickerRiseSpeed = Mathf.Clamp(s.dashFlickerRiseSpeed, .1f, 10);
            s.dashFlickerFallSpeed = Mathf.Clamp(s.dashFlickerFallSpeed, .1f, 10);
            s.dashTrailWidthFalloff = Mathf.Clamp(s.dashTrailWidthFalloff, 0, 4);
            if (s.dashFlickerDirection != JcDashFlickerDirection.TowardStart)
                s.dashFlickerDirection = JcDashFlickerDirection.TowardDestination;
            s.dashTrailLength = Mathf.Max(0, s.dashTrailLength);
            s.dashTrailOpacity = Mathf.Clamp01(s.dashTrailOpacity);
            s.dashTrailFalloff = Mathf.Clamp(s.dashTrailFalloff, .2f, 4);
            s.dashGlowWidth = Mathf.Clamp(s.dashGlowWidth, 0, .2f);
            s.dashGlowStrength = Mathf.Clamp01(s.dashGlowStrength);
            s.dashFlickerStrength = Mathf.Clamp01(s.dashFlickerStrength);
            s.dashFlickerSpeed = Mathf.Max(0, s.dashFlickerSpeed);
            s.highlightColorCyclePeriod = Mathf.Max(0, s.highlightColorCyclePeriod);
            s.markerThickness = Mathf.Clamp(s.markerThickness, .001f, .3f);
            s.bevelWidth = Mathf.Clamp(s.bevelWidth, 0, .08f);
            s.curveSegments = Mathf.Clamp((s.curveSegments + 1) / 2 * 2, 4, 32);
            s.sideBrightness = Mathf.Clamp01(s.sideBrightness);
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
        private void OnEnable() { settings = settings.Sanitized(); }
        private void OnValidate() { settings = settings.Sanitized(); }
    }
}
