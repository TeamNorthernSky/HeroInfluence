using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 P2] 광선/스트릭(PawBeam)의 튜닝 값.
    /// 색 바리에이션(노랑/녹색)은 에셋 2개(P2_PawBeam_Yellow / P2_PawBeam_Green)로 구현.
    /// 워프 잔상 스트릭도 같은 SO 타입의 별도 에셋(P2_PawStreak)을 씀 — 짧고 가늘고 캡이 부드러운 값.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/PawForYou/P2_Paw Beam Preset", fileName = "P2_PawBeamPreset")]
    public class PawBeamPreset : ScriptableObject
    {
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
