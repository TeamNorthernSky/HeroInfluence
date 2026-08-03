using UnityEngine;
using UnityEngine.Serialization;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 T5] 유성 투사체 + 혜성(TaoMeteorTuner)의 튜닝 값.
    /// 비행(ProjectileVfx) + 리본 크기(CometShell) + 혜성 룩(TaoComet/TaoCometHead 셰이더, MPB)을 한 에셋에서 라이브 튜닝.
    /// ★색 = 머리→꼬리 그라데이션(headColor→tailColor, 리본을 따라 보간. 구체 머리 채움=headColor).
    /// ★밝기 = 마스터(intensity, 머리·꼬리 전체 곱) + 개별 노브(상대 배율).
    /// 에셋명: T5_TaoMeteor.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Taosenaiyo/T5_Tao Meteor Preset", fileName = "T5_TaoMeteor")]
    public class TaoMeteorPreset : ScriptableObject
    {
        [Header("비행 (ProjectileVfx)")]
        [Tooltip("구체 월드 지름(m)")]
        public float worldSize = 0.24f;
        [Tooltip("비행 속력(m/s). 비행 중 변경은 다음 발사부터 적용")]
        public float speed = 6f;
        [Tooltip("포물선 정점 높이(m). 낮을수록 직선에 가까운 곡선")]
        public float arcHeight = 0.6f;
        [Tooltip("구체 자체 트레일(TrailRenderer) 유지 시간(초)")]
        public float trailTime = 0.7f;

        [Header("혜성 리본 크기")]
        [Tooltip("꼬리 최대 길이(m, 궤적 호길이)")]
        public float cometLength = 1.8f;
        [Tooltip("리본 폭(m)")]
        public float cometWidth = 0.65f;

        [Header("색 (머리→꼬리 그라데이션)")]
        [Tooltip("머리 색: 구체 코마 채움 + 꼬리 그라데이션 시작(HDR)")]
        [FormerlySerializedAs("coreColor")]
        [ColorUsage(true, true)] public Color headColor = new Color(1f, 0.98f, 0.85f);
        [Tooltip("꼬리 끝 색: 리본을 따라 headColor에서 이 색으로 보간(HDR)")]
        [FormerlySerializedAs("glowColor")]
        [ColorUsage(true, true)] public Color tailColor = new Color(1f, 0.82f, 0.3f);
        [Tooltip("림 색: 구체 머리 윤곽 + 꼬리 측면 라인 공용(HDR)")]
        [ColorUsage(true, true)] public Color rimColor = new Color(1f, 0.95f, 0.7f);
        [Tooltip("★마스터 밝기 — 머리(채움·윤곽)와 꼬리 전체에 곱해짐. 전체 톤을 한 번에 조절")]
        [Range(0f, 10f)] public float intensity = 1.7f;

        [Header("머리 (구형 코마 — 어느 각도든 동그란 윤곽)")]
        [Tooltip("머리 코마 구체 지름(m). 구체(worldSize)보다 크게")]
        public float headSize = 0.42f;
        [Tooltip("코마 중심 채움 밝기(마스터 곱 전 상대값)")]
        [Range(0f, 8f)] public float headFillIntensity = 0.8f;
        [Tooltip("코마 채움 집중도(클수록 중심만)")]
        [Range(0.3f, 8f)] public float headFillPower = 1.5f;
        [Tooltip("코마 윤곽 밝기(마스터 곱 전 상대값)")]
        [Range(0f, 10f)] public float headRimIntensity = 2.5f;
        [Tooltip("코마 윤곽 두께(클수록 얇은 윤곽선)")]
        [Range(0.3f, 16f)] public float headRimPower = 2.5f;

        [Header("꼬리 (리본)")]
        [Tooltip("꼬리 시작(머리 쪽) 폭(리본 폭 대비 상대값)")]
        [FormerlySerializedAs("headWidth")]
        [Range(0.05f, 0.8f)] public float tailStartWidth = 0.5f;
        [Tooltip("꼬리 끝 폭(상대값)")]
        [Range(0.005f, 0.4f)] public float tailEndWidth = 0.04f;
        [Tooltip("폭 테이퍼 곡률(클수록 머리 근처에서 급히 가늘어짐)")]
        [Range(0.3f, 4f)] public float tailTaper = 1.2f;
        [Tooltip("꼬리 밝기 감쇠 지수(클수록 짧게 사라짐)")]
        [Range(0.3f, 6f)] public float tailFade = 1.6f;
        [Tooltip("꼬리 플리커 진폭")]
        [Range(0f, 0.6f)] public float flickerAmp = 0.2f;
        [Tooltip("꼬리 플리커 속도")]
        [Range(0f, 20f)] public float flickerSpeed = 7f;

        [Header("꼬리 림 (측면 라인 — 리본 곡선을 따라 흐름)")]
        [Tooltip("꼬리 림 밝기(마스터 곱 전 상대값)")]
        [Range(0f, 10f)] public float rimIntensity = 2f;
        [Tooltip("림 측면 위치(꼬리 폭 대비. 1=가장자리)")]
        [Range(0.3f, 1.2f)] public float rimPos = 0.8f;
        [Tooltip("림 선 부드러움")]
        [Range(0.02f, 0.5f)] public float rimSoft = 0.12f;
        [Tooltip("림 꼬리쪽 감쇠")]
        [Range(0.3f, 6f)] public float rimFade = 1.2f;
    }
}
