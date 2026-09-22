using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 T2b] 기초 방사 스프레이 스커트(TaoBaseSpray)의 튜닝 값.
    /// 위로 벌어지는 콘 표면에 갈퀴 스트릭이 바깥·위로 뿜어지는 방사형 버스트.
    /// 본 오라(T2)와 셰이더는 공유하되 쓰는 노브만 분리 노출. 에셋명: T2b_TaoBaseSpray.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Taosenaiyo/T3_Tao Base Spray Preset", fileName = "T3_TaoBaseSpray")]
    public class TaoBaseSprayPreset : ScriptableObject
    {
        [Header("★따름 (Alter 전용)")]
        [Tooltip("켜면 트랜스폼(형태·움직임)을 Basic 프리셋에서 읽는다. Alter 는 기본 ON — B/A는 색만 다르다.")]
        public bool followBasic;
        [Tooltip("Basic 따름이 켜져 있을 때 위치·형태 기준으로 읽을 프리셋입니다. 연결이 비어 있으면 자기 값을 사용합니다.")]
        public TaoBaseSprayPreset basicRef;

        /// <summary>트랜스폼 정본 — Alter 가 따름이면 Basic.</summary>
        public TaoBaseSprayPreset TransformSource => followBasic && basicRef != null ? basicRef : this;

        [Header("스커트 크기 / 기울기")]
        [Tooltip("바닥 지름(m). 상단은 flare만큼 더 벌어짐")]
        public float width = 0.9f;
        [Tooltip("스커트 높이(m)")]
        public float height = 0.7f;
        [Tooltip("지면 위 띄우는 높이(m)")]
        public float groundOffsetY = 0.02f;
        [Tooltip("페이드인 동안 커지는 팝 정도")]
        [Range(0f, 1f)] public float riseGrow = 0.4f;
        [Tooltip("스커트 벌어짐(위로 갈수록 바깥). 클수록 눕는 방사형")]
        [Range(0f, 2f)] public float flare = 1.6f;
        [Tooltip("벌어짐 곡률(1=직선 콘, 클수록 위쪽에서 급히 벌어짐)")]
        [Range(0.5f, 6f)] public float flareCurve = 1.0f;

        [Header("바탕 글로우 (갈퀴 뒤 은은한 채움)")]
        [Tooltip("바탕 글로우 색(은은한 배경)")]
        [ColorUsage(true, true)] public Color baseColor = new Color(1f, 0.82f, 0.3f);
        [Tooltip("바탕 밝기(0=갈퀴만)")]
        [Range(0f, 8f)] public float baseIntensity = 0.15f;
        [Tooltip("전체 불투명도(최종 곱)")]
        [Range(0f, 1f)] public float opacity = 1f;

        [Header("세로 형태")]
        [Tooltip("바닥 페이드인 구간(0~1 높이 비율)")]
        [Range(0f, 0.5f)] public float bottomFade = 0.06f;
        [Tooltip("세로 그라데이션 곡률(클수록 끝이 빨리 어두워짐)")]
        [Range(0.1f, 5f)] public float verticalBias = 0.7f;
        [Tooltip("갈퀴 끝 경계 최소 높이(0~1)")]
        [Range(0f, 1f)] public float topMin = 0.8f;
        [Tooltip("갈퀴 끝 경계 최대 높이(0~1)")]
        [Range(0f, 1f)] public float topMax = 1f;
        [Tooltip("끝 경계 페이드 폭(자연 소멸)")]
        [Range(0f, 0.5f)] public float topSoft = 0.35f;
        [Tooltip("끝 경계 노이즈 스케일")]
        [Range(0.5f, 10f)] public float topNoiseScale = 3f;
        [Tooltip("끝 경계 일렁임 속도")]
        [Range(0f, 4f)] public float topNoiseSpeed = 0.8f;
        [Tooltip("좌우 실루엣 소프트(|NdotV| 지수)")]
        [Range(0.3f, 8f)] public float facePower = 1f;

        [Header("갈퀴 스트릭")]
        [Tooltip("갈퀴 색(진한 오렌지 권장)")]
        [ColorUsage(true, true)] public Color sprayColor = new Color(1f, 0.62f, 0.12f);
        [Tooltip("갈퀴 밝기")]
        [Range(0f, 10f)] public float sprayIntensity = 4.5f;
        [Tooltip("갈퀴가 깔리는 존 높이(0~1, 스커트 높이 대비). 1=전체")]
        [Range(0.05f, 1f)] public float sprayHeight = 1f;
        [Tooltip("갈퀴 컬럼 수")]
        [Range(4f, 120f)] public float sprayCount = 36f;
        [Tooltip("갈퀴 밑동 폭")]
        [Range(0.02f, 0.6f)] public float sprayWidth = 0.24f;
        [Tooltip("갈퀴 가장자리 날카로움")]
        [Range(0.005f, 0.3f)] public float spraySharp = 0.05f;
        [Tooltip("갈퀴 끊김 빈도(낮을수록 긴 갈퀴)")]
        [Range(0.5f, 12f)] public float sprayDashFreq = 1.6f;
        [Tooltip("갈퀴 채움 비율(길이)")]
        [Range(0.1f, 1f)] public float sprayDashDuty = 0.55f;
        [Tooltip("흐름 속도(+=위·바깥으로, -=아래로)")]
        [Range(-6f, 6f)] public float spraySpeed = 2.4f;
        [Tooltip("갈퀴 끝 테이퍼: 0=직사각, 1=완전히 뾰족한 삼각형")]
        [Range(0f, 1f)] public float sprayTaper = 0.85f;
        [Tooltip("무작위성: 컬럼별 속도/길이 흩뜨림(0=일제 발사, 1=최대 랜덤)")]
        [Range(0f, 1f)] public float sprayRandom = 0.7f;
    }
}
