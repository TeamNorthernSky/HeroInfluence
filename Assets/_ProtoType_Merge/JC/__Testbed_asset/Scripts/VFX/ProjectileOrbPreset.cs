using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 발사체 구체 VFX의 튜닝 값을 한곳에 모은 프리셋(ChargeOrbPreset의 발사체판).
    /// 커스텀 인스펙터(ProjectileOrbPresetEditor)의 "프리팹에 적용"으로
    /// ProjectileOrbCore.mat + ProjectileOrb 프리팹(ProjectileVfx + Sparkles PS) + 전용 스파클 재질에 일괄 반영.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Projectile Orb Preset", fileName = "ProjectileOrbPreset")]
    public class ProjectileOrbPreset : ScriptableObject
    {
        [Header("코어 — 색/밝기")]
        [ColorUsage(true, true)] public Color coreColor = new Color(1f, 0.88f, 0.14f);
        [ColorUsage(true, true)] public Color rimColor = new Color(1f, 0.60f, 0.10f);
        [Range(0, 8)] public float coreIntensity = 1.7f;
        [Range(0, 8)] public float rimIntensity = 2.0f;
        [Tooltip("코어 타이트니스 — 클수록 코어가 작고 또렷")]
        [Range(0.5f, 10)] public float corePower = 3.4f;
        [Tooltip("림 타이트니스 — 클수록 외곽선이 얇음")]
        [Range(0.5f, 16)] public float rimPower = 4.5f;

        [Header("코어/림 구조 (2오브젝트 사이즈)")]
        [Tooltip("CoreInner 상대 크기(0~1). 작을수록 코어가 안쪽 → 갭↑, 스파이크가 외곽선 안에 갇힘")]
        [Range(0.1f, 1.2f)] public float coreSize = 0.72f;
        [Tooltip("RimShell 상대 크기(보통 1.0). core/rim 크기 차이가 갭을 만듦")]
        [Range(0.3f, 1.5f)] public float rimSize = 1.0f;

        [Header("코어 — 표면 스파이크 (발사체 기본 매끈 0/0)")]
        [Range(0, 0.6f)] public float coreSpikeAmount = 0f;
        [Range(0, 0.6f)] public float rimSpikeAmount = 0f;
        [Range(0.5f, 12)] public float spikeFreq = 5.5f;
        [Range(0, 8)] public float spikeSpeed = 2.5f;
        [Range(1, 8)] public float spikeSharp = 3.0f;

        [Header("오브 크기")]
        public float worldSize = 0.11f;

        [Header("비행")]
        public float speed = 6f;
        public float arcHeight = 1.2f;
        public float trailTime = 0.4f;
        public bool trailAutoBySpeed = false;
        public float trailLengthWorld = 2.5f;

        [Header("도착 버스트")]
        public float burstScaleMul = 1.5f;
        public float burstDuration = 0.3f;

        [Header("스파이크 버스트 (방사형 레이 = 전담 오브젝트)")]
        [Tooltip("촘촘함 — 초당 레이 개수")]
        public float rayRate = 60f;
        [Tooltip("중심부 크기 — 레이가 시작하는 방출 구 반경(m)")]
        public float rayCenterSize = 0.015f;
        [Tooltip("스파이크 도달 반경(m) — 레이가 뻗는 최대 거리")]
        public float rayReachRadius = 0.18f;
        [Tooltip("레이 크기 범위(두께/베이스)")]
        public float raySizeMin = 0.008f;
        public float raySizeMax = 0.025f;
        [Tooltip("방사 속도(m/s) — 수명 = 도달반경/속도")]
        public float raySpeed = 0.7f;
        [Tooltip("늘어남 배수 — 클수록 길고 뾰족")]
        public float rayLengthScale = 2.2f;
        [ColorUsage(true, true)] public Color rayColor = new Color(1f, 0.95f, 0.6f) * 2.2f;

        [Header("스타버스트(스파클)")]
        public float sparkleRate = 10f;
        public float sparkleSizeMin = 0.03f;
        public float sparkleSizeMax = 0.07f;
        public float sparkleLifetime = 0.45f;
        public float sparkleShapeRadius = 0.06f;
        [ColorUsage(true, true)] public Color sparkleColor = new Color(1f, 0.97f, 0.7f) * 1.8f;

        [Header("스타 플래시 (뾰족한 별 한 덩어리 = 조합용)")]
        [Tooltip("별 크기(월드 m)")]
        public float starSize = 0.14f;
        [Tooltip("펄스 주기(각 별 수명 초). 커졌다 작아지는 반복")]
        public float starLifetime = 0.8f;
        [Tooltip("동시 개수(초당 발생) — 보통 1~3")]
        public float starRate = 2f;
        [Tooltip("회전 속도(도/초)")]
        public float starSpinSpeed = 35f;
        [ColorUsage(true, true)] public Color starColor = new Color(1f, 0.9f, 0.35f) * 2.0f;
    }
}
