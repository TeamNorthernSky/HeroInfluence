using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 P2] 광선/스트릭(PawBeam)의 튜닝 값.
    /// 변형은 에셋 3개(P2_PawBeam_Basic/은백 · _Alter/금 · _Miss/녹색 빗나감)로 구현 — Alter/Miss 는 Basic 따름.
    /// 워프 잔상 스트릭도 같은 SO 타입의 별도 에셋(P6_Legacy_PawStreak, 레거시 Mistake 전용)을 씀 — 짧고 가늘고 캡이 부드러운 값.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/PawForYou/P2_Paw Beam Preset", fileName = "P2_PawBeamPreset")]
    public class PawBeamPreset : ScriptableObject
    {
        [Header("★따름 (Alter/Miss 전용)")]
        [Tooltip("켜면 트랜스폼(위치·폭·단면·노이즈·캡)을 Basic 프리셋에서 읽는다. Alter/Miss 는 기본 ON. 스트릭 에셋(공용)은 끔.")]
        public bool followBasic;
        public PawBeamPreset basicRef;

        /// <summary>트랜스폼 정본 — Alter/Miss 가 따름이면 Basic.</summary>
        public PawBeamPreset TransformSource => followBasic && basicRef != null ? basicRef : this;

        [Header("★위치 — 광선 끝 (P6 위치 프리셋 폐기·이관, 260805. 스트릭 에셋에서는 무시)")]
        [Tooltip("광선 끝 기준 소켓(대상 계층에서 이름 검색). 비면 대상 루트.")]
        public string endSocketName = "";
        [Tooltip("광선 끝(몸통에 박히는 지점) 오프셋(m, 대상 기준). 광선 시작은 발(P1 재등장 좌표) 아래다.")]
        public Vector3 endOffset = new Vector3(0f, 0.35f, 0f);

        [Header("폭 / 색")]
        [Tooltip("광선 월드 폭(m)")]
        public float width = 0.35f;
        [Tooltip("코어(고휘도 중심선) 색")]
        [ColorUsage(true, true)] public Color coreColor = new Color(1f, 1f, 0.9f);
        [Tooltip("글로우(외곽 번짐) 색")]
        [ColorUsage(true, true)] public Color glowColor = new Color(1f, 0.8f, 0.25f);
        [Tooltip("전체 밝기")]
        [Range(0f, 10f)] public float intensity = 2.5f;

        [Header("단면")]
        [Tooltip("코어(고휘도 중심선) 폭 비율")]
        [Range(0.01f, 0.6f)] public float coreWidth = 0.18f;
        [Tooltip("글로우 감쇠 지수(클수록 좁고 진하게)")]
        [Range(0.5f, 8f)] public float glowFalloff = 2.2f;
        [Tooltip("코어 가장자리 소프트")]
        [Range(0.005f, 0.3f)] public float edgeSoft = 0.06f;

        [Header("노이즈 / 펄스")]
        [Tooltip("세로 노이즈 스케일")]
        [Range(0f, 20f)] public float noiseScale = 6f;
        [Tooltip("노이즈 스크롤 속도(+ = 시작→끝 방향)")]
        [Range(-10f, 10f)] public float noiseScroll = 3f;
        [Tooltip("노이즈에 의한 폭 흔들림 정도")]
        [Range(0f, 1f)] public float noiseAmount = 0.25f;
        [Tooltip("폭 펄스(호흡) 진폭")]
        [Range(0f, 0.5f)] public float pulseAmp = 0.12f;
        [Tooltip("폭 펄스 빈도")]
        [Range(0f, 20f)] public float pulseFreq = 7f;

        [Header("캡 / 신장")]
        [Tooltip("시작쪽(uv.y=0) 소프트 캡 폭")]
        [Range(0f, 0.5f)] public float capSoftStart = 0.08f;
        [Tooltip("끝쪽(uv.y=1) 소프트 캡 폭")]
        [Range(0f, 0.5f)] public float capSoftEnd = 0.12f;
        [Tooltip("신장 프론트(자라나는 머리) 소프트")]
        [Range(0.005f, 0.3f)] public float frontSoft = 0.05f;

        [Header("착탄 플래시")]
        [Tooltip("착탄 순간 플래시 색(이 변형의 색과 매칭)")]
        [ColorUsage(true, true)] public Color hitFlashColor = new Color(1f, 0.9f, 0.5f);
    }
}
