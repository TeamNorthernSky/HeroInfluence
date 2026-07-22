using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 T3] 휘날리는 깃털(TaoFeatherBurst)의 튜닝 값.
    /// 절차 SDF 깃털(베시카+샤프트+깃가지 결) — 추후 텍스처 교체 전제. 에셋명: T3_TaoFeather.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Taosenaiyo/T3_Tao Feather Preset", fileName = "T3_TaoFeather")]
    public class TaoFeatherPreset : ScriptableObject
    {
        [Header("스폰")]
        [Tooltip("동시 최대 깃털 수")]
        public int maxFeathers = 10;
        [Tooltip("새 깃털 생성 간격 최소(초)")]
        public float spawnIntervalMin = 0.12f;
        [Tooltip("새 깃털 생성 간격 최대(초)")]
        public float spawnIntervalMax = 0.3f;
        [Tooltip("한 깃털 수명 최소(초)")]
        public float lifetimeMin = 1.5f;
        [Tooltip("한 깃털 수명 최대(초)")]
        public float lifetimeMax = 2.3f;
        [Tooltip("스폰 반경(m). 중심 주위 원판 내 랜덤")]
        public float spawnRadius = 0.8f;
        [Tooltip("시작 높이(중심 기준, m)")]
        public float baseYOffset = 0.15f;

        [Header("모션")]
        [Tooltip("상승 속도 최소(m/s)")]
        public float riseSpeedMin = 0.45f;
        [Tooltip("상승 속도 최대(m/s)")]
        public float riseSpeedMax = 0.8f;
        [Tooltip("중심 둘레 나선 회전(도/초)")]
        [Range(-180f, 180f)] public float spiralSpeed = 40f;
        [Tooltip("좌우 플러터 진폭(m)")]
        [Range(0f, 0.5f)] public float swayAmp = 0.12f;
        [Tooltip("플러터 빈도(Hz)")]
        [Range(0f, 6f)] public float swayFreq = 1.4f;
        [Tooltip("자전(roll) 최대(±도/초)")]
        [Range(0f, 360f)] public float spinMax = 90f;

        [Header("크기 / 페이드")]
        [Tooltip("깃털 세로 크기 최소(m)")]
        public float sizeMin = 0.18f;
        [Tooltip("깃털 세로 크기 최대(m)")]
        public float sizeMax = 0.3f;
        [Tooltip("페이드인 구간 비율(수명 대비)")]
        [Range(0f, 0.5f)] public float fadeInFrac = 0.15f;
        [Tooltip("페이드아웃 구간 비율(수명 대비)")]
        [Range(0.05f, 0.9f)] public float fadeOutFrac = 0.4f;

        [Header("룩 (깃털 셰이더)")]
        [Tooltip("깃털 몸통 색(HDR)")]
        [ColorUsage(true, true)] public Color color = new Color(1f, 0.9f, 0.55f);
        [Tooltip("전체 밝기")]
        [Range(0f, 8f)] public float intensity = 2.2f;
        [Tooltip("깃대 휨(부호는 깃털마다 랜덤 반전)")]
        [Range(0f, 0.6f)] public float bend = 0.22f;
        [Tooltip("깃가지 결 빈도")]
        [Range(0f, 60f)] public float barbFreq = 22f;
        [Tooltip("깃가지 결 명암 진폭")]
        [Range(0f, 0.8f)] public float barbAmount = 0.3f;
    }
}
