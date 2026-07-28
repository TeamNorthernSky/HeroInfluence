using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「프리즘 익스플로전」 프리셋 — 솔라 프리즘의 대형·단일·강화판.
    /// 전용 재질(PrismCrystalX/StarFlareX/GlowDiscX/PrismBurstX/PrismImpactRingX) +
    /// PrismExplosionVfx 배치·모션·상승조준·발사·폭발을 한 곳에서 튜닝(라이브 프리뷰 MPB).
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Prism Explosion Preset (프리즘 익스플로전)", fileName = "FX_PrismExplosionPreset")]
    public class PrismExplosionPreset : ScriptableObject
    {
        [Header("크리스탈 — 색")]
        [Tooltip("유리 몸통 색.")]
        [ColorUsage(true, true)] public Color bodyColor = new Color(0.82f, 0.9f, 1.05f);
        [Tooltip("프레넬 림(에지) 색.")]
        [ColorUsage(true, true)] public Color rimColor = new Color(1.45f, 1.55f, 1.75f);
        [Tooltip("크리스탈 발광 배수.")]
        [Range(0, 6)] public float crystalEmission = 1.3f;

        [Header("크리스탈 — 유리")]
        [Tooltip("몸통 기본 알파.")]
        [Range(0, 1)] public float bodyAlpha = 0.22f;
        [Tooltip("림 알파.")]
        [Range(0, 1)] public float rimAlpha = 0.85f;
        [Tooltip("프레넬 지수.")]
        [Range(0.5f, 8f)] public float fresnelPow = 2.6f;
        [Tooltip("면 단위 유리 계조 강도.")]
        [Range(0, 1)] public float facetAmount = 0.65f;

        [Header("스타 플레어")]
        [Tooltip("플레어 색.")]
        [ColorUsage(true, true)] public Color flareColor = new Color(1.6f, 1.65f, 1.8f);
        [Tooltip("플레어 발광 배수.")]
        [Range(0, 6)] public float flareEmission = 1.8f;
        [Tooltip("플레어 점화 정렬 임계(0~1).")]
        [Range(0.5f, 0.999f)] public float flareThreshold = 0.94f;
        [Tooltip("플레어 강도 곡선 지수.")]
        [Range(0.5f, 8f)] public float flareSharp = 2.5f;
        [Tooltip("플레어 쿼드 월드 크기(m).")]
        [Range(0.3f, 1.6f)] public float flareSize = 0.9f;

        [Header("바닥 반사광")]
        [Tooltip("바닥광 색.")]
        [ColorUsage(true, true)] public Color groundColor = new Color(1.2f, 1.15f, 0.95f);
        [Tooltip("바닥광 발광 배수.")]
        [Range(0, 6)] public float groundEmission = 1.2f;
        [Tooltip("바닥광 쿼드 크기(m).")]
        [Range(0.6f, 2.8f)] public float groundSize = 1.6f;
        [Tooltip("바닥광 상시 강도.")]
        [Range(0, 1)] public float groundBase = 0.6f;
        [Tooltip("플레어 연동 맥동 강도.")]
        [Range(0, 1)] public float groundPulse = 0.4f;

        [Header("배치")]
        [Tooltip("캐스터 전방 거리(m).")]
        [Range(0.7f, 2.5f)] public float forwardOffset = 1.5f;
        [Tooltip("수정 중심 높이(m).")]
        [Range(0.5f, 1.8f)] public float hoverHeight = 1.1f;
        [Tooltip("수정 스케일(메시 원본 5.8m 기준 배율).")]
        [Range(0.2f, 0.6f)] public float prismScale = 0.4f;

        [Header("모션")]
        [Tooltip("호버 진폭(m).")]
        [Range(0, 0.3f)] public float hoverAmp = 0.06f;
        [Tooltip("호버 주파수(Hz).")]
        [Range(0.1f, 3f)] public float hoverFreq = 1.0f;
        [Tooltip("차지 자전 속도(도/초).")]
        [Range(0, 240f)] public float spinSpeed = 60f;
        [Tooltip("소환 팝 시간(초).")]
        [Range(0.1f, 1.5f)] public float summonTime = 0.5f;

        [Header("상승·조준")]
        [Tooltip("상승·조준 시간(초).")]
        [Range(0.2f, 2f)] public float riseTime = 0.8f;
        [Tooltip("추가 상승 높이(m).")]
        [Range(0, 2f)] public float riseHeight = 0.8f;
        [Tooltip("조준 진동 진폭(m). 발사 직전으로 갈수록 커진다.")]
        [Range(0, 0.15f)] public float tremorAmp = 0.05f;
        [Tooltip("조준 진동 주파수(Hz).")]
        [Range(5f, 60f)] public float tremorFreq = 26f;
        [Tooltip("조준 중 자전 가속 배율(도달값).")]
        [Range(1f, 8f)] public float aimSpinMul = 3f;

        [Header("발사")]
        [Tooltip("비행 속력(m/s).")]
        [Range(5f, 30f)] public float launchSpeed = 14f;
        [Tooltip("비행 중 자전 가속 배율.")]
        [Range(1f, 12f)] public float launchSpinMul = 5f;
        [Tooltip("비행 포물선 정점 높이(m).")]
        [Range(0, 1.5f)] public float launchArc = 0.3f;
        [Tooltip("탄착점 높이(m, 진형 중앙 기준).")]
        [Range(0, 2f)] public float targetHeight = 0.9f;
        [Tooltip("본 트레일 유지 시간(초).")]
        [Range(0.1f, 1.5f)] public float trailTime = 0.45f;
        [Tooltip("본 트레일 시작 폭(m).")]
        [Range(0.05f, 0.8f)] public float trailWidth = 0.4f;
        [Tooltip("나선 보조 트레일 반경(m).")]
        [Range(0.1f, 0.8f)] public float spiralRadius = 0.35f;
        [Tooltip("나선 회전 속도(회전/초).")]
        [Range(0.5f, 8f)] public float spiralRate = 3f;

        [Header("대폭발")]
        [Tooltip("폭발 백열 코어 색.")]
        [ColorUsage(true, true)] public Color impactHotColor = new Color(1.75f, 1.75f, 1.9f);
        [Tooltip("폭발 외곽 색(청보라).")]
        [ColorUsage(true, true)] public Color impactGlowColor = new Color(0.75f, 0.85f, 1.5f);
        [Tooltip("폭발 발광 배수.")]
        [Range(0, 6)] public float impactEmission = 1.8f;
        [Tooltip("지면 링 색.")]
        [ColorUsage(true, true)] public Color impactRingColor = new Color(0.8f, 0.9f, 1.4f);
        [Tooltip("폭발 버스트 재생 시간(초).")]
        [Range(0.2f, 2.5f)] public float impactBurstDuration = 0.85f;
        [Tooltip("지면 링 재생 시간(초).")]
        [Range(0.2f, 2f)] public float impactRingDuration = 0.7f;
        [Tooltip("수평 확산 버스트 크기(m).")]
        [Range(2f, 8f)] public float impactSizeH = 4.5f;
        [Tooltip("수직 불기둥 버스트 크기(m).")]
        [Range(2f, 8f)] public float impactSizeV = 4f;
        [Tooltip("지면 링 크기(m).")]
        [Range(2f, 10f)] public float impactRingSize = 5f;
        [Tooltip("폭발 후 연기 잔류 대기(초).")]
        [Range(0, 3f)] public float smokeLinger = 1.2f;

        [Header("빛 입자")]
        [Tooltip("차지 입자 방출률(초당).")]
        [Range(0, 40f)] public float emberRate = 12f;
        [Tooltip("차지 입자 크기(m).")]
        [Range(0.005f, 0.12f)] public float emberSize = 0.04f;
    }
}
