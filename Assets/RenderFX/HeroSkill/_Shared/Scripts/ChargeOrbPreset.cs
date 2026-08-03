using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 차징 구체 VFX의 모든 튜닝 값을 한곳에 모은 프리셋.
    /// 커스텀 인스펙터(ChargeOrbPresetEditor)의 "프리팹에 적용" 버튼으로
    /// 재질(ChargeOrbCore.mat) + 오브 프리팹(ChargeOrb) + 팔로워 프리팹(ChargeTrailFollower)에 일괄 반영.
    /// "현재값 캡처"로 역방향(현 상태 → 프리셋)도 가능.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Charge Orb Preset", fileName = "ChargeOrbPreset")]
    public class ChargeOrbPreset : ScriptableObject
    {
        [Header("코어 — 색/밝기")]
        [ColorUsage(true, true)] public Color coreColor = new Color(1f, 0.88f, 0.14f);
        [ColorUsage(true, true)] public Color rimColor = new Color(1f, 0.60f, 0.10f);
        [Range(0, 8)] public float coreIntensity = 1.7f;
        [Range(0, 8)] public float rimIntensity = 2.0f;
        [Range(0.5f, 10)] public float corePower = 3.4f;
        [Range(0.5f, 16)] public float rimPower = 4.5f;
        [Range(0, 1)] public float gapStrength = 0.5f;

        [Header("코어 — 표면 스파이크 (코어/림 분리)")]
        [Range(0, 0.6f)] public float coreSpikeAmount = 0.10f;   // 정면(코어)
        [Range(0, 0.6f)] public float rimSpikeAmount = 0.22f;    // 실루엣(림)
        [Range(0.5f, 12)] public float spikeFreq = 5.5f;
        [Range(0, 8)] public float spikeSpeed = 2.5f;
        [Range(1, 8)] public float spikeSharp = 3.0f;

        [Header("오브 — 크기/등장")]
        public float startWorldSize = 0.015f;
        public float endWorldSize = 0.11f;
        public float growDuration = 0.5f;

        [Header("스파크(코어→외곽 입자)")]
        public float sparkSizeMin = 0.06f;
        public float sparkSizeMax = 0.16f;
        public float sparkRate = 40f;
        public float sparkSpeed = 1.0f;
        [ColorUsage(true, true)] public Color sparkColor = new Color(1f, 0.82f, 0.25f) * 2f;

        [Header("트레일 밴드(파티클 트레일)")]
        public float bandRate = 6f;
        public float bandStartLifetime = 1.1f;
        public float bandStartSize = 0.018f;
        public float bandTrailLifetime = 1.0f;
        [Range(0, 1.5f)] public float bandInheritVelocity = 0.7f;
        public float bandShapeRadius = 0.035f;
        [ColorUsage(true, true)] public Color bandColor = new Color(1f, 0.95f, 0.62f) * 1.5f;

        [Header("반짝임(4각 스타 / 스타버스트)")]
        [Tooltip("발생 주기(초당 개수)")]
        public float sparkleRate = 6f;
        public float sparkleSizeMin = 0.02f;
        public float sparkleSizeMax = 0.05f;
        [Tooltip("각 스타 수명(초)")]
        public float sparkleLifetime = 0.45f;
        [Tooltip("코어 주변 흩뿌림 반경(m)")]
        public float sparkleShapeRadius = 0.03f;
        [ColorUsage(true, true)] public Color sparkleColor = new Color(1f, 0.97f, 0.7f) * 1.8f;
    }
}
