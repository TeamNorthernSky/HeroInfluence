using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「솔라 프리즘」 차지 단계 프리셋.
    /// 크리스탈(PrismCrystal)·스타 플레어(StarFlare)·바닥광(GlowDisc) 재질 +
    /// SolarPrismVfx 배치·모션·플레어 판정을 한 곳에서 튜닝(라이브 프리뷰 MPB).
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Solar Prism Preset (솔라 프리즘)", fileName = "FX_SolarPrismPreset")]
    public class SolarPrismPreset : ScriptableObject
    {
        [Header("크리스탈 — 색")]
        [Tooltip("유리 몸통 색.")]
        [ColorUsage(true, true)] public Color bodyColor = new Color(0.82f, 0.9f, 1.05f);
        [Tooltip("프레넬 림(에지) 색.")]
        [ColorUsage(true, true)] public Color rimColor = new Color(1.45f, 1.55f, 1.75f);
        [Tooltip("크리스탈 발광 배수.")]
        [Range(0, 6)] public float crystalEmission = 1.3f;

        [Header("크리스탈 — 유리")]
        [Tooltip("몸통 기본 알파(투명도).")]
        [Range(0, 1)] public float bodyAlpha = 0.22f;
        [Tooltip("림 알파(에지 불투명도).")]
        [Range(0, 1)] public float rimAlpha = 0.85f;
        [Tooltip("프레넬 지수. 클수록 에지가 얇고 예리.")]
        [Range(0.5f, 8f)] public float fresnelPow = 2.6f;
        [Tooltip("면 단위 유리 계조 강도.")]
        [Range(0, 1)] public float facetAmount = 0.65f;

        [Header("스타 플레어")]
        [Tooltip("플레어 색.")]
        [ColorUsage(true, true)] public Color flareColor = new Color(1.6f, 1.65f, 1.8f);
        [Tooltip("플레어 발광 배수.")]
        [Range(0, 6)] public float flareEmission = 1.8f;
        [Tooltip("줄기 가늘기. 클수록 얇은 광선.")]
        [Range(4, 80)] public float spikeNarrow = 26f;
        [Tooltip("줄기 길이 감쇠 지수.")]
        [Range(1, 10)] public float spikeFall = 3.5f;
        [Tooltip("대각 보조 줄기 비율.")]
        [Range(0, 1)] public float diagRatio = 0.35f;
        [Tooltip("중심 글로우 크기(uv).")]
        [Range(0.02f, 0.6f)] public float flareCoreSize = 0.16f;
        [Tooltip("플레어 점화 정렬 임계(0~1). 클수록 정면 정렬 순간에만 점화.")]
        [Range(0.5f, 0.999f)] public float flareThreshold = 0.94f;
        [Tooltip("플레어 강도 곡선 지수. 클수록 날카롭게 점멸.")]
        [Range(0.5f, 8f)] public float flareSharp = 2.5f;
        [Tooltip("플레어 쿼드 월드 크기(m).")]
        [Range(0.2f, 0.9f)] public float flareSize = 0.55f;

        [Header("바닥 반사광")]
        [Tooltip("바닥광 색(따뜻한 반사).")]
        [ColorUsage(true, true)] public Color groundColor = new Color(1.2f, 1.15f, 0.95f);
        [Tooltip("바닥광 발광 배수.")]
        [Range(0, 6)] public float groundEmission = 1.2f;
        [Tooltip("바닥광 감쇠 지수.")]
        [Range(0.5f, 8f)] public float groundFalloff = 2.5f;
        [Tooltip("바닥광 쿼드 크기(m).")]
        [Range(0.3f, 1.5f)] public float groundSize = 0.9f;
        [Tooltip("바닥광 상시 강도.")]
        [Range(0, 1)] public float groundBase = 0.55f;
        [Tooltip("플레어 연동 맥동 강도.")]
        [Range(0, 1)] public float groundPulse = 0.45f;

        [Header("배치")]
        [Tooltip("활성 프리즘 수(테스트베드 기본 3).")]
        [Range(1, 5)] public int unitCount = 3;
        [Tooltip("유닛 간 가로 간격(m).")]
        [Range(0.4f, 1.5f)] public float spacing = 0.95f;
        [Tooltip("캐스터 전방 거리(m).")]
        [Range(0.5f, 2.1f)] public float forwardOffset = 1.3f;
        [Tooltip("프리즘 중심 높이(m).")]
        [Range(0.3f, 1.6f)] public float hoverHeight = 0.95f;
        [Tooltip("프리즘 스케일(메시 원본 5.8m 기준 배율).")]
        [Range(0.06f, 0.26f)] public float prismScale = 0.16f;

        [Header("모션")]
        [Tooltip("호버 진폭(m).")]
        [Range(0, 0.4f)] public float hoverAmp = 0.07f;
        [Tooltip("호버 주파수(Hz).")]
        [Range(0.1f, 4f)] public float hoverFreq = 1.1f;
        [Tooltip("자전 속도(도/초).")]
        [Range(0, 360f)] public float spinSpeed = 80f;
        [Tooltip("유닛별 자전 속도 지터(비율).")]
        [Range(0, 0.5f)] public float spinJitter = 0.15f;
        [Tooltip("소환 팝 시간(초).")]
        [Range(0.05f, 1.5f)] public float summonTime = 0.35f;
        [Tooltip("유닛 간 소환 시차(초).")]
        [Range(0, 0.6f)] public float summonStagger = 0.12f;

        [Header("벨트 꼭지점(메시 로컬)")]
        [Tooltip("벨트 꼭지점 반경. 큐브 유래 메시=대각 √2≈1.414.")]
        [Range(0.5f, 2f)] public float beltRadius = 1.414f;
        [Tooltip("벨트 꼭지점 높이.")]
        [Range(-2f, 2f)] public float beltLocalY = 0f;
        [Tooltip("벨트 기준 각도 오프셋(도). 큐브 유래=45.")]
        [Range(0, 90f)] public float beltAngleOffset = 45f;

        [Header("빛 입자")]
        [Tooltip("입자 방출률(초당).")]
        [Range(0, 40f)] public float emberRate = 7f;
        [Tooltip("입자 크기(m).")]
        [Range(0.005f, 0.1f)] public float emberSize = 0.03f;

        [Header("발사")]
        [Tooltip("발사 속력(m/s).")]
        [Range(3f, 30f)] public float launchSpeed = 12f;
        [Tooltip("유닛 간 발사 시차(초).")]
        [Range(0, 0.5f)] public float launchStagger = 0.08f;
        [Tooltip("발사 중 자전 가속 배율.")]
        [Range(1f, 10f)] public float launchSpinMul = 3f;
        [Tooltip("비행 포물선 정점 높이(m). 0=직선.")]
        [Range(0, 1.5f)] public float launchArc = 0.2f;
        [Tooltip("명중점 높이(m, 대상 피벗 기준).")]
        [Range(0, 2f)] public float targetHeight = 0.8f;
        [Tooltip("탄두 정렬 블렌드 구간(비행 진행도). 상단 극점이 전면을 향하도록 기우는 시간.")]
        [Range(0.02f, 0.6f)] public float aimBlend = 0.18f;
        [Tooltip("궤적 트레일 유지 시간(초).")]
        [Range(0.05f, 1.5f)] public float launchTrailTime = 0.35f;
        [Tooltip("궤적 트레일 시작 폭(m). 끝은 0으로 수렴.")]
        [Range(0.02f, 0.6f)] public float launchTrailWidth = 0.28f;

        [Header("개별 폭발")]
        [Tooltip("폭발 백열 코어 색.")]
        [ColorUsage(true, true)] public Color impactHotColor = new Color(1.7f, 1.7f, 1.85f);
        [Tooltip("폭발 외곽 색(옅은 청보라).")]
        [ColorUsage(true, true)] public Color impactGlowColor = new Color(0.75f, 0.85f, 1.5f);
        [Tooltip("폭발 발광 배수.")]
        [Range(0, 6)] public float impactEmission = 1.6f;
        [Tooltip("지면 링 색.")]
        [ColorUsage(true, true)] public Color impactRingColor = new Color(0.8f, 0.9f, 1.4f);
        [Tooltip("폭발 버스트 재생 시간(초).")]
        [Range(0.1f, 2f)] public float impactBurstDuration = 0.55f;
        [Tooltip("지면 링 재생 시간(초).")]
        [Range(0.1f, 2f)] public float impactRingDuration = 0.5f;
        [Tooltip("폭발 버스트 쿼드 크기(m).")]
        [Range(0.5f, 4f)] public float impactSize = 1.7f;
        [Tooltip("지면 링 쿼드 크기(m).")]
        [Range(0.5f, 5f)] public float impactRingSize = 2f;
    }
}
