using System;
using UnityEngine;

namespace JC.Indicators
{
    [Serializable]
    public sealed class JcInteractionHoverOutlineValues
    {
        [InspectorName("윤곽선 색상"), ColorUsage(false), Tooltip("탐사 상호작용 대상에 호버할 때 표시하는 윤곽선과 순환 하이라이트의 기본 색상입니다. 튜토리얼 안내에는 적용하지 않습니다.")]
        public Color outlineColor = new Color(25f / 255f, 1f, 84f / 255f, 1f);
        [InspectorName("윤곽선 인텐시티"), Range(0f, 4f), Tooltip("고정 윤곽선의 강도입니다. 1은 기본값, 0은 고정 윤곽선만 숨깁니다. 하이라이트 강도는 별도로 조절합니다.")]
        public float outlineIntensity = 1f;
        [InspectorName("하이라이트 순환 속도"), Range(0f, 3f), Tooltip("초당 회전 수입니다. 기본 0.4167은 약 2.4초에 한 바퀴이며, 0은 정지합니다.")]
        public float highlightSpeed = 1f / 2.4f;
        [InspectorName("하이라이트 인텐시티"), Range(0f, 4f), Tooltip("마주 보는 두 순환 하이라이트의 강도입니다. 1은 기본값, 0은 하이라이트만 숨깁니다.")]
        public float highlightIntensity = 1f;
        [InspectorName("하이라이트 범위"), Range(0f, 1f), Tooltip("두 하이라이트가 차지하는 둘레 범위입니다. 0은 하이라이트 없음, 1은 윤곽선 전체 하이라이트입니다.")]
        public float highlightRange = .35f;
        [InspectorName("하이라이트 경계 스무딩"), Range(0f, 1f), Tooltip("하이라이트 양쪽 경계의 감쇠 비율입니다. 0은 선명한 경계, 1은 넓고 부드러운 경계입니다. 범위 1에서는 전체가 균일하게 강조됩니다.")]
        public float highlightSmoothing = .65f;
        [InspectorName("흰색 기본선 두께"), Range(0f, 12f), Tooltip("색상 윤곽선 바로 안쪽, 건물 표면 쪽에 그어지는 흰색 선의 두께입니다. 화면 픽셀 단위이며 기본 1px, 0이면 숨깁니다. 바깥쪽 색상 윤곽선과 분리되어 강도를 높여도 흰색 선을 가리지 않습니다.")]
        public float whiteOutlineWidth = 1f;
        [InspectorName("바깥선 두께"), Range(0f, 12f), Tooltip("상호작용 대상 바깥선의 화면 픽셀 두께입니다.")]
        public float outlineWidth = 2f;
        [InspectorName("이동 불가 대상도 표시"), Tooltip("이동력이 부족하거나 경로가 막힌 대상도 호버하면 표시합니다.")]
        public bool showUnreachable = true;

        [InspectorName("윤곽선 경계 스무딩"), Range(0f, 2f), Tooltip("완성된 녹색선과 안쪽 흰색선의 안팎 경계를 화면 픽셀 단위로 부드럽게 합니다. 기본 1px, 0이면 추가 합성 패스를 끕니다. 건물 표면은 흐리지 않으며 하이라이트 경계 스무딩과 별개입니다.")]
        public float edgeSmoothing = 1f;

        public JcInteractionHoverOutlineValues Copy() => (JcInteractionHoverOutlineValues)MemberwiseClone();
    }

    public sealed class JcInteractionHoverOutlineProfile : ScriptableObject
    {
        public JcInteractionHoverOutlineValues values = new JcInteractionHoverOutlineValues();
    }
}
