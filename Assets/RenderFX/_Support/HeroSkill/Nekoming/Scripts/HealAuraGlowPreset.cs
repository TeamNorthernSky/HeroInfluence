using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 #5] 힐 오라 바닥 광채(E-3)의 튜닝 값.
    /// 형태 = 실린더 셸 표면(가산·양면). 네 변 하드 페이드 대신, 세로 그라데이션(오브젝트Y) +
    /// 좌우 실루엣 소프트(NdotV) + 미세 세로 질감(원주 각도)을 셰이더가 절차적으로 처리.
    /// 커스텀 인스펙터(HealAuraGlowPresetEditor)의 "프리팹에 적용"으로 HealOrbit 프리팹의 AuraGlow에 반영.
    /// livePreview가 켜져 있으면 플레이 중에도 즉시 반영(재질은 비파괴 MPB). 에셋명: 5_HealAuraGlowPreset.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/5_Heal Aura Glow Preset", fileName = "5_HealAuraGlowPreset")]
    public class HealAuraGlowPreset : ScriptableObject
    {
        [Header("★따름 (Alter 전용)")]
        [Tooltip("켜면 트랜스폼(크기·위치)을 Basic 프리셋에서 읽는다. Alter 는 기본 ON.")]
        public bool followBasic;
        [Tooltip("Basic 따름이 켜져 있을 때 위치·형태 기준으로 읽을 프리셋입니다. 연결이 비어 있으면 자기 값을 사용합니다.")]
        public HealAuraGlowPreset basicRef;

        /// <summary>트랜스폼 정본 — Alter 가 따름이면 Basic.</summary>
        public HealAuraGlowPreset TransformSource => followBasic && basicRef != null ? basicRef : this;

        [Header("크기 / 위치 (월드 m)")]
        [Tooltip("광채 기둥 지름(m)")]
        public float width = 1.6f;
        [Tooltip("광채 기둥 높이(m)")]
        public float height = 2.0f;
        [Tooltip("바닥(중심) 기준 Y 오프셋(m). 0=착지점 지면")]
        public float groundOffsetY = 0f;

        [Header("색 / 밝기")]
        [Tooltip("이 시각 요소의 색입니다. HDR 색은 발광 강도와 함께 최종 밝기에 영향을 줍니다.")]
        [ColorUsage(true, true)] public Color color = new Color(1f, 0.85f, 0.28f);
        [Tooltip("전체 밝기")]
        [Range(0f, 8f)] public float intensity = 1.8f;
        [Tooltip("전체 투명도(0=투명 ~ 1=불투명). intensity와 별개 최종 배수")]
        [Range(0f, 1f)] public float opacity = 1.0f;

        [Header("세로 형태")]
        [Tooltip("바닥 얕은 페이드(지면 하드림 방지). 작게")]
        [Range(0f, 0.5f)] public float bottomFade = 0.06f;
        [Tooltip("상단 falloff. 클수록 아래에 몰리고 위로 빨리 옅어짐")]
        [Range(0.1f, 5f)] public float verticalBias = 1.4f;

        [Header("상단 경계 (min==max → 평면)")]
        [Tooltip("상단 경계 최소 높이(0~1, 기둥 높이 비율). TopMax와 같으면 경계가 평면")]
        [Range(0f, 1f)] public float topMin = 0.70f;
        [Tooltip("상단 경계 최대 높이(0~1). 1 미만이면 실린더 상단에서 잘리지 않음. 뾰족 정도 = TopMax-TopMin")]
        [Range(0f, 1f)] public float topMax = 0.92f;
        [Tooltip("상단 경계 페이드 폭. 0에 가까울수록 날카로운 평면 경계(선이 또렷), 크면 페더. 평면을 보려면 낮게 + verticalBias도 낮게")]
        [Range(0f, 0.5f)] public float topSoft = 0.12f;
        [Tooltip("불규칙 조각 밀도(원주)")]
        [Range(0.5f, 10f)] public float topNoiseScale = 3.0f;
        [Tooltip("상단 경계가 꿈틀대는 속도")]
        [Range(0f, 4f)] public float topNoiseSpeed = 0.6f;

        [Header("실루엣 소프트")]
        [Tooltip("좌우 가장자리 부드러움. 클수록 정면만 밝고 실루엣이 빨리 사라짐(딱딱한 세로 경계 방지)")]
        [Range(0.3f, 8f)] public float facePower = 1.6f;

        [Header("세로 광선(불규칙 빛줄기)")]
        [Tooltip("빛줄기 밀도(원주). 0=끔")]
        [Range(0f, 80f)] public float streakTiling = 18f;
        [Tooltip("빛줄기 대비 강도(미세하게). 노이즈 기반이라 규칙 밴딩 없음")]
        [Range(0f, 1f)] public float streakStrength = 0.12f;
        [Tooltip("위로 흐르는 속도")]
        [Range(-4f, 4f)] public float streakScroll = 0.7f;

        [Header("일렁임")]
        [Tooltip("전체 밝기 일렁이는 정도")]
        [Range(0f, 1f)] public float wobbleAmount = 0.15f;
        [Tooltip("일렁임 속도")]
        [Range(0f, 6f)] public float wobbleSpeed = 1.2f;

        [Header("솟아남")]
        [Tooltip("생성 시 높이 팝(솟아오름). 0=끔, 1=0에서 자라남. 페이드인 구간 동안 height×lerp")]
        [Range(0f, 1f)] public float riseGrow = 0.4f;
    }
}
