using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 #7] 힐 오라 바닥 장판(E-5)의 튜닝 값.
    /// 바닥에 눕는 방사형 샤인 스파크 디스크. 중심 광채 + 회전 스파크 광선 + 외곽 소프트 수렴은
    /// 셰이더(JC/VFX/HealGroundShine)가 절차적으로 처리.
    /// 커스텀 인스펙터(HealGroundShinePresetEditor)의 "프리팹에 적용"으로 HealOrbit 프리팹의 GroundShine에 반영.
    /// livePreview가 켜져 있으면 플레이 중에도 즉시 반영(재질은 비파괴 MPB). 에셋명: 7_HealGroundShinePreset.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/6_Heal Ground Shine Preset", fileName = "6_HealGroundShinePreset")]
    public class HealGroundShinePreset : ScriptableObject
    {
        [Header("★따름 (Alter 전용)")]
        [Tooltip("켜면 트랜스폼(반지름·위치)을 Basic 프리셋에서 읽는다. Alter 는 기본 ON.")]
        public bool followBasic;
        public HealGroundShinePreset basicRef;

        /// <summary>트랜스폼 정본 — Alter 가 따름이면 Basic.</summary>
        public HealGroundShinePreset TransformSource => followBasic && basicRef != null ? basicRef : this;

        [Header("크기 / 위치 (월드 m)")]
        [Tooltip("중심장판 반지름(m). 이 안쪽은 항상 채워진 디스크")]
        public float discRadius = 0.8f;
        [Tooltip("레이가 닿는 외곽선 반지름(m). 디스크와 같거나 작으면 레이 없이 순수 원형 디스크")]
        public float rayRadius = 1.1f;
        [Tooltip("지면 위 오프셋(m). z-fight 방지용 소량")]
        public float groundOffsetY = 0.02f;

        [Header("색 / 밝기")]
        [ColorUsage(true, true)] public Color color = new Color(1f, 0.88f, 0.35f);
        [Range(0f, 8f)] public float intensity = 1.8f;
        [Tooltip("전체 투명도(0~1). intensity와 별개 최종 배수")]
        [Range(0f, 1f)] public float opacity = 1.0f;

        [Header("경계 수렴 / 밝기")]
        [Tooltip("외곽 수렴 폭(원호·레이 팁, 반지름 방향). 클수록 넓게 부드럽게 사라짐")]
        [Range(0.01f, 1f)] public float edgeSoft = 0.35f;
        [Tooltip("레이 옆면 소프트 폭(SDF 거리 기준). 클수록 옆면이 부드러움. 0=칼같은 옆면. 레이 두께보다 크면 레이가 흐려짐")]
        [Range(0f, 0.3f)] public float sideSoft = 0.06f;
        [Tooltip("중심 몰림. 클수록 중심만 밝고 경계로 빨리 어두워짐")]
        [Range(0.2f, 6f)] public float centerFalloff = 1.6f;

        [Header("레이 (별 포인트)")]
        [Tooltip("별 포인트 수(정수 반올림). 5=5망성, 6=6망성… 0=원판")]
        [Range(0f, 48f)] public float rayDensity = 5f;
        [Tooltip("별 형태: 1=직선 엣지 별(정통 망성) / 1 미만=뚱뚱·둥근 별 / 1 초과=오목 샤인(✦)")]
        [Range(0.5f, 8f)] public float raySharp = 1.0f;
        [Tooltip("레이 회전 속도(도/초). 음수=반대방향")]
        [Range(-180f, 180f)] public float rayRotSpeed = 18f;
        [Tooltip("골 플래토(주기 대비 평탄 비율). 0=연속 별, 클수록 포인트 사이 원호↑ = 디스크+돌출 레이 느낌")]
        [Range(0f, 0.95f)] public float valleyWidth = 0f;

        [Header("솟아남")]
        [Tooltip("생성 시 지름 팝(퍼짐). 0=끔, 1=0에서 자라남. 페이드인 구간 동안 diameter×lerp")]
        [Range(0f, 1f)] public float spreadGrow = 0.5f;
    }
}
