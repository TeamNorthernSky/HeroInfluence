using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 #6] 힐 오라 녹색 십자(E-4)의 튜닝 값.
    /// 캐릭터 주변 바닥에서 위로 솟는 녹색 십자 빌보드(스크립트 풀). 스폰 시 스케일 오버슛 팝 + 상승 오버슛 = 포보보봉.
    /// 커스텀 인스펙터(HealCrossPresetEditor)의 "프리팹에 적용"으로 HealOrbit 프리팹의 CrossBurst(HealCrossBurst)에 반영.
    /// livePreview가 켜져 있으면 플레이 중에도 즉시 반영(새 십자부터, 룩은 MPB 비파괴). 에셋명: 6_HealCrossPreset.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/6_Heal Cross Preset", fileName = "6_HealCrossPreset")]
    public class HealCrossPreset : ScriptableObject
    {
        [Header("스폰")]
        [Tooltip("동시 최대 십자 수")]
        public int maxCrosses = 14;
        [Tooltip("새 십자 생성 간격 최소(초)")]
        public float spawnIntervalMin = 0.06f;
        [Tooltip("새 십자 생성 간격 최대(초)")]
        public float spawnIntervalMax = 0.16f;
        [Tooltip("한 십자 수명 최소(초)")]
        public float lifetimeMin = 0.9f;
        [Tooltip("한 십자 수명 최대(초)")]
        public float lifetimeMax = 1.3f;

        [Header("배치 / 상승")]
        [Tooltip("스폰 반경(m). 중심 주위 원판 내 랜덤")]
        public float radius = 0.7f;
        [Tooltip("시작 높이(중심 기준, m). 보통 0=발밑")]
        public float baseYOffset = 0.05f;
        [Tooltip("최종(정착) 상승 속도 최소(m/s)")]
        public float riseSpeedMin = 1.2f;
        [Tooltip("최종(정착) 상승 속도 최대(m/s)")]
        public float riseSpeedMax = 1.8f;
        [Tooltip("초반 가속 배수. 1=등속(가속 없음), 클수록 초반에 최종속도×이배로 빠르게 솟음")]
        [Range(1f, 8f)] public float riseAccelMul = 3.0f;
        [Tooltip("초반 가속 시간(초). 초반 속도가 최종 속도로 잦아드는 시간(0=즉시 등속)")]
        [Range(0f, 1.5f)] public float riseAccelTime = 0.25f;

        [Header("바운스 팝 / bob / 스핀")]
        [Tooltip("스케일 팝 오버슛(스폰 시 튐). 0=팝 없음")]
        [Range(0f, 4f)] public float popOvershoot = 2.0f;
        [Tooltip("팝 구간 비율(수명 대비). 이 구간에서 0→오버슛→1")]
        [Range(0.05f, 0.6f)] public float popFrac = 0.25f;
        [Tooltip("종료 축소 구간 비율(수명 대비)")]
        [Range(0.05f, 0.6f)] public float endFrac = 0.3f;
        [Tooltip("상하 흔들림 진폭(m)")]
        [Range(0f, 0.3f)] public float bobAmp = 0.05f;
        [Tooltip("상하 흔들림 빈도(Hz)")]
        [Range(0f, 6f)] public float bobFreq = 2.0f;
        [Tooltip("면내 회전 최대(±도/초). 십자가 살짝 돎")]
        [Range(0f, 180f)] public float spinMax = 25f;
        [Tooltip("빌보드 Y축만: 켜면 수직 유지·수평만 카메라 향함(위/아래로 안 눕음). 끄면 카메라 완전 정면")]
        public bool billboardYOnly = true;

        [Header("크기 / 페이드")]
        [Tooltip("십자 크기 최소(m)")]
        public float sizeMin = 0.28f;
        [Tooltip("십자 크기 최대(m)")]
        public float sizeMax = 0.44f;
        [Tooltip("페이드인 구간 비율(수명 대비)")]
        [Range(0f, 0.5f)] public float fadeInFrac = 0.12f;
        [Tooltip("페이드아웃 구간 비율(수명 대비)")]
        [Range(0f, 0.6f)] public float fadeOutFrac = 0.3f;

        [Header("룩 (십자 셰이더)")]
        [ColorUsage(true, true)] public Color color = new Color(0.35f, 1f, 0.45f);
        [Range(0f, 8f)] public float intensity = 2.2f;
        [Tooltip("팔 두께")]
        [Range(0.02f, 0.5f)] public float barWidth = 0.13f;
        [Tooltip("팔 길이")]
        [Range(0.1f, 0.5f)] public float barLength = 0.42f;
        [Tooltip("가장자리 부드러움")]
        [Range(0.001f, 0.3f)] public float softness = 0.06f;
    }
}
