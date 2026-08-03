using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 T2] 발산 오라(TaoAuraFlare)의 튜닝 값.
    /// 초사이언풍: 콘 플레어(발산각) + 얇고 진한 펜선 스트릭 상승. 에셋명: T2_TaoAuraFlare.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Taosenaiyo/T2_Tao Aura Flare Preset", fileName = "T2_TaoAuraFlare")]
    public class TaoAuraFlarePreset : ScriptableObject
    {
        [Header("크기")]
        [Tooltip("바닥 지름(m). 상단은 플레어만큼 더 벌어짐")]
        public float width = 1.0f;
        [Tooltip("기둥 높이(m)")]
        public float height = 2.2f;
        [Tooltip("지면 위 띄우는 높이(m)")]
        public float groundOffsetY = 0.02f;
        [Tooltip("페이드인 동안 커지는 팝 정도")]
        [Range(0f, 1f)] public float riseGrow = 0.35f;
        [Tooltip("기초 발산량: 바닥에서 방사로 벌어지는 정도(0=수직 기둥)")]
        [Range(0f, 2f)] public float flare = 0.7f;
        [Tooltip("발산 집중 곡률: 클수록 벌어짐이 바닥에만 집중되고 위쪽은 빠르게 수직")]
        [Range(0.5f, 6f)] public float flareCurve = 2.2f;
        [Tooltip("기초(바닥) 밝기 부스트 — 바닥이 이글거리는 정도")]
        [Range(0f, 3f)] public float baseBoost = 0.6f;

        [Header("색 / 밝기")]
        [Tooltip("바탕 글로우 색(은은한 배경)")]
        [ColorUsage(true, true)] public Color baseColor = new Color(1f, 0.82f, 0.3f);
        [Tooltip("바탕 글로우 밝기(은은한 채움)")]
        [Range(0f, 8f)] public float baseIntensity = 1.2f;
        [Tooltip("펜선 색(진한 스트로크)")]
        [ColorUsage(true, true)] public Color lineColor = new Color(1f, 0.7f, 0.15f);
        [Tooltip("펜선 밝기")]
        [Range(0f, 10f)] public float lineIntensity = 3.2f;
        [Tooltip("전체 불투명도(최종 곱)")]
        [Range(0f, 1f)] public float opacity = 1f;

        [Header("세로 형태 (힐 오라 계승)")]
        [Tooltip("바닥 페이드인 구간(0~1 높이 비율)")]
        [Range(0f, 0.5f)] public float bottomFade = 0.06f;
        [Tooltip("몸통 세로 그라데이션 곡률(클수록 위가 빨리 어두워짐)")]
        [Range(0.1f, 5f)] public float verticalBias = 1.2f;
        [Tooltip("상단 경계 최소 높이(0~1). max와 같으면 평면 경계")]
        [Range(0f, 1f)] public float topMin = 0.72f;
        [Tooltip("상단 경계 최대 높이(0~1). 노이즈가 min~max 사이에서 출렁임")]
        [Range(0f, 1f)] public float topMax = 0.95f;
        [Tooltip("상단 경계 페이드 폭(0=칼 경계)")]
        [Range(0f, 0.5f)] public float topSoft = 0.15f;
        [Tooltip("상단 경계 노이즈 스케일(뾰족 봉우리 개수 느낌)")]
        [Range(0.5f, 10f)] public float topNoiseScale = 3f;
        [Tooltip("상단 경계 노이즈 일렁임 속도")]
        [Range(0f, 4f)] public float topNoiseSpeed = 0.8f;
        [Tooltip("좌우 실루엣 소프트(|NdotV| 지수)")]
        [Range(0.3f, 8f)] public float facePower = 1.4f;

        [Header("펜선 스트릭")]
        [Tooltip("원주 컬럼 수(라인 개수 느낌)")]
        [Range(4f, 80f)] public float lineCount = 26f;
        [Tooltip("컬럼 내 라인 폭")]
        [Range(0.02f, 0.6f)] public float lineWidth = 0.16f;
        [Tooltip("라인 가장자리 날카로움(작을수록 칼선)")]
        [Range(0.005f, 0.3f)] public float lineSharp = 0.04f;
        [Tooltip("상승 대시 빈도(높이당 끊김 수)")]
        [Range(0.5f, 8f)] public float dashFreq = 2.2f;
        [Tooltip("대시 채움 비율(1=끊김 없는 통선)")]
        [Range(0.1f, 1f)] public float dashDuty = 0.65f;
        [Tooltip("대시 상승 스크롤 속도")]
        [Range(0f, 6f)] public float scrollSpeed = 1.6f;
    }
}
