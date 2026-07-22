using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 P1] 고양이 발 빌보드(PawSprite)의 형태/룩 튜닝 값.
    /// 색 바리에이션(은백/금빛)은 이 SO의 에셋 2개(P1_PawSprite_Silver / P1_PawSprite_Gold)로 구현 —
    /// PawForYouVfx.ApplyVariant 또는 하네스 키로 런타임 교체.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/PawForYou/P1_Paw Sprite Preset", fileName = "P1_PawSpritePreset")]
    public class PawSpritePreset : ScriptableObject
    {
        [Header("크기 / 거동")]
        [Tooltip("발 월드 크기(m, 쿼드 한 변)")]
        public float size = 1.6f;
        [Tooltip("쿼드 내 발 형태 점유 비율. 작을수록 여백↑(글로우가 사각 경계에 잘리지 않음)")]
        [Range(0.2f, 1f)] public float canvasScale = 0.55f;
        [Tooltip("빌보드 Y축만(수직 유지). 끄면 카메라 완전 정면")]
        public bool billboardYOnly = false;
        [Tooltip("둥실 bob 진폭(m)")]
        [Range(0f, 0.3f)] public float bobAmp = 0.04f;
        [Tooltip("둥실 bob 빈도(Hz)")]
        [Range(0f, 6f)] public float bobFreq = 1.2f;

        [Header("형태 (SDF)")]
        [Tooltip("발가락 개수")]
        [Range(2, 6)] public int toeCount = 4;
        [Tooltip("발가락 배치 각도 범위(도)")]
        [Range(30f, 160f)] public float toeSpreadDeg = 95f;
        [Tooltip("손바닥 중심→발가락 거리")]
        [Range(0.2f, 0.9f)] public float toeDist = 0.52f;
        [Tooltip("발가락 반지름")]
        [Range(0.05f, 0.35f)] public float toeRadius = 0.16f;
        [Tooltip("손바닥 가로 반지름")]
        [Range(0.1f, 0.8f)] public float palmRadiusX = 0.42f;
        [Tooltip("손바닥 세로 반지름")]
        [Range(0.1f, 0.8f)] public float palmRadiusY = 0.34f;
        [Tooltip("손바닥 중심 Y 오프셋(발가락 위 공간 확보)")]
        [Range(-0.6f, 0.2f)] public float palmOffsetY = -0.25f;
        [Tooltip("손바닥-발가락 융합 정도(smooth-min k)")]
        [Range(0.01f, 0.3f)] public float fusion = 0.08f;

        [Header("패드 프린트 (젤리)")]
        [Tooltip("큰 패드 반지름")]
        [Range(0f, 0.4f)] public float padMainRadius = 0.2f;
        [Tooltip("큰 패드 세로 눌림(1=원)")]
        [Range(0.5f, 1.2f)] public float padMainSquash = 0.8f;
        [Tooltip("발가락 패드 반지름")]
        [Range(0f, 0.2f)] public float padToeRadius = 0.08f;
        [Tooltip("발가락 패드 위치(발가락 거리 대비 비율)")]
        [Range(0.3f, 1f)] public float padToeDistMul = 0.62f;
        [Tooltip("패드 프린트 발광 색(HDR)")]
        [ColorUsage(true, true)] public Color padColor = new Color(1f, 0.95f, 0.8f);
        [Tooltip("패드 프린트 발광 세기")]
        [Range(0f, 8f)] public float padIntensity = 2f;

        [Header("레이어 룩")]
        [Tooltip("몸통 필 색(반투명 채움)")]
        [ColorUsage(true, true)] public Color fillColor = new Color(0.85f, 0.9f, 1f);
        [Tooltip("몸통 불투명도(배경 가림 정도)")]
        [Range(0f, 1f)] public float fillOpacity = 0.8f;
        [Tooltip("내부 가장자리 밝힘(경계 근처가 밝아짐)")]
        [Range(0f, 3f)] public float innerGrad = 0.8f;
        [Tooltip("림(경계 밴드) 색")]
        [ColorUsage(true, true)] public Color rimColor = Color.white;
        [Tooltip("림 밝기")]
        [Range(0f, 8f)] public float rimIntensity = 2.5f;
        [Tooltip("림 밴드 폭")]
        [Range(0.005f, 0.2f)] public float rimWidth = 0.05f;
        [Tooltip("외곽 글로우 색(실루엣 밖 번짐)")]
        [ColorUsage(true, true)] public Color glowColor = new Color(0.8f, 0.9f, 1f);
        [Tooltip("외곽 글로우 밝기")]
        [Range(0f, 8f)] public float glowIntensity = 1.5f;
        [Tooltip("외곽 글로우 감쇠 거리")]
        [Range(0.02f, 0.6f)] public float glowRange = 0.25f;

        [Header("움직임")]
        [Tooltip("실루엣 일렁임 진폭")]
        [Range(0f, 0.1f)] public float wobbleAmp = 0.015f;
        [Tooltip("일렁임 속도")]
        [Range(0f, 10f)] public float wobbleSpeed = 2f;

        [Header("워프 플래시")]
        [Tooltip("워프 순간 플래시 색(이 변형의 색과 매칭)")]
        [ColorUsage(true, true)] public Color warpFlashColor = Color.white;
    }
}
