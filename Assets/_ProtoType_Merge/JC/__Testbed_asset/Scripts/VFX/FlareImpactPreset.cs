using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「플레어 봄」 발사·탄착 연출 프리셋.
    /// 스타버스트(FlareImpactBurst)·지면 링(FlareImpactRing) 재질 + FlareBombImpact 타이밍 +
    /// FlareBombVfx 비행 파라미터를 한 곳에서 튜닝한다(라이브 프리뷰 MPB).
    /// 에디트 모드에서는 previewProgress로 원샷 진행도를 정지 프레임으로 스크럽할 수 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Flare Impact Preset (발사·탄착)", fileName = "FX_FlareImpactPreset")]
    public class FlareImpactPreset : ScriptableObject
    {
        [Header("배치(월드 크기)")]
        [Tooltip("지면 확산 버스트 쿼드 폭(m). 가로로 넓게 = 지면 방사 확산.")]
        [Range(0.5f, 6f)] public float groundWidth = 2.6f;
        [Tooltip("지면 확산 버스트 쿼드 높이(m).")]
        [Range(0.3f, 4f)] public float groundHeight = 1.5f;
        [Tooltip("수직 스파이크 버스트 쿼드 폭(m).")]
        [Range(0.3f, 4f)] public float spikeWidth2 = 1.3f;
        [Tooltip("수직 스파이크 버스트 쿼드 높이(m). 세로로 길게 = 솟는 불기둥 스파이크.")]
        [Range(0.5f, 6f)] public float spikeHeight = 2.8f;
        [Tooltip("지면 충격 링 쿼드 크기(m).")]
        [Range(0.5f, 8f)] public float ringSize = 3f;

        [Header("버스트 — 색")]
        [Tooltip("백열 코어 색(스파이크 밑동·플래시).")]
        [ColorUsage(true, true)] public Color hotColor = new Color(1.7f, 1.6f, 1.4f);
        [Tooltip("외곽 골드 색(스파이크 끝).")]
        [ColorUsage(true, true)] public Color goldColor = new Color(1.2f, 0.72f, 0.22f);
        [Tooltip("버스트 발광 배수.")]
        [Range(0, 6)] public float burstEmission = 1.5f;
        [Tooltip("백열 구간(스파이크-로컬 0~1). 클수록 밑동 백열이 길다.")]
        [Range(0, 1)] public float hotCore = 0.4f;

        [Header("버스트 — 진행")]
        [Tooltip("성장 구간(진행도 비율). 이 비율 시점에 스파이크가 최대 길이 도달(easeOut).")]
        [Range(0.05f, 1f)] public float growFrac = 0.35f;
        [Tooltip("전체 페이드 시작(진행도). 이후 1.0까지 서서히 소멸.")]
        [Range(0, 1)] public float fadeStart = 0.5f;

        [Header("버스트 — 대 스파이크")]
        [Tooltip("대 스파이크 개수.")]
        [Range(4, 48)] public int spikeCount = 22;
        [Tooltip("대 스파이크 최대 길이(uv).")]
        [Range(0.1f, 1f)] public float spikeLen = 0.9f;
        [Tooltip("스파이크별 길이 지터.")]
        [Range(0, 1)] public float lenJitter = 0.55f;
        [Tooltip("대 스파이크 밑동 절반 폭(uv).")]
        [Range(0.005f, 0.2f)] public float spikeWidth = 0.05f;
        [Tooltip("스파이크별 폭 지터.")]
        [Range(0, 1)] public float widthJitter = 0.5f;
        [Tooltip("끝 테이퍼 샤프니스. 클수록 바늘처럼 예리.")]
        [Range(0.5f, 6f)] public float taperSharp = 2.2f;

        [Header("버스트 — 소 스파이크")]
        [Tooltip("소 스파이크 개수(대 스파이크 사이를 메우는 잔가시). 0=없음.")]
        [Range(0, 96)] public int subCount = 44;
        [Tooltip("소 스파이크 최대 길이(uv).")]
        [Range(0.05f, 1f)] public float subLen = 0.45f;
        [Tooltip("소 스파이크 밑동 절반 폭(uv).")]
        [Range(0.003f, 0.15f)] public float subWidth = 0.025f;

        [Header("버스트 — 랜덤")]
        [Tooltip("스파이크 각도 위치 지터(셀 단위).")]
        [Range(0, 1)] public float posJitter = 0.7f;
        [Tooltip("랜덤 시드. 바꾸면 스파이크 배치가 리롤.")]
        [Range(0, 64)] public float seed = 0f;

        [Header("센터 플래시")]
        [Tooltip("플래시 반경(uv).")]
        [Range(0.02f, 0.8f)] public float flashSize = 0.24f;
        [Tooltip("플래시 강도.")]
        [Range(0, 6)] public float flashIntensity = 2.2f;
        [Tooltip("플래시 소멸 시점(진행도). 초반에 급감쇠.")]
        [Range(0.05f, 1f)] public float flashFade = 0.45f;

        [Header("지면 링")]
        [Tooltip("링 색.")]
        [ColorUsage(true, true)] public Color ringColor = new Color(1.25f, 0.78f, 0.3f);
        [Tooltip("링 발광 배수.")]
        [Range(0, 6)] public float ringEmission = 1.4f;
        [Tooltip("링 최대 반경(uv).")]
        [Range(0.1f, 1f)] public float ringMaxR = 0.85f;
        [Tooltip("링 밴드 폭(uv).")]
        [Range(0.01f, 0.4f)] public float ringWidth = 0.09f;
        [Tooltip("링 내부 잔광량.")]
        [Range(0, 1)] public float innerGlow = 0.25f;
        [Tooltip("링 페이드 시작(진행도).")]
        [Range(0, 1)] public float ringFadeStart = 0.35f;

        [Header("타이밍(임팩트)")]
        [Tooltip("버스트 재생 시간(초).")]
        [Range(0.1f, 3f)] public float burstDuration = 0.7f;
        [Tooltip("링 재생 시간(초).")]
        [Range(0.1f, 3f)] public float ringDuration = 0.55f;
        [Tooltip("탄착점에서 링을 내릴 거리(m). 몸통 명중 높이→지면.")]
        [Range(0, 2f)] public float ringGroundOffset = 0.8f;

        [Header("소환·비행(FlareBombVfx)")]
        [Tooltip("오브 전체 스케일 배율(차징·비행 공통). 1=오브 프리팹 원본. 0.667=반지름 1/3 축소.")]
        [Range(0.2f, 1.5f)] public float orbScale = 0.667f;
        [Tooltip("소환 위치(캐스터 로컬 오프셋). 기획서 = 캐릭터 좌측 상단.")]
        public Vector3 summonOffset = new Vector3(-0.45f, 1.6f, 0f);
        [Tooltip("차징 유지 시간(초).")]
        [Range(0.1f, 3f)] public float chargeDuration = 0.8f;
        [Tooltip("오브 성장 시간(초).")]
        [Range(0.05f, 1f)] public float chargeGrowTime = 0.25f;
        [Tooltip("비행 속력(m/s).")]
        [Range(2f, 30f)] public float speed = 9f;
        [Tooltip("포물선 정점 높이(m).")]
        [Range(0, 3f)] public float arcHeight = 0.6f;
        [Tooltip("비행 중 오브 스케일 배율.")]
        [Range(0.2f, 1.5f)] public float flightScale = 0.8f;
        [Tooltip("명중점 높이(m, 타깃 피벗 기준).")]
        [Range(0, 2f)] public float targetHeight = 0.8f;

        [Header("궤적·불씨(비행 FX)")]
        [Tooltip("궤적 트레일 유지 시간(초). 길수록 궤적이 길게 남는다.")]
        [Range(0.05f, 2f)] public float trailTime = 0.55f;
        [Tooltip("궤적 트레일 시작 폭(m). 끝은 0으로 수렴.")]
        [Range(0.01f, 0.5f)] public float trailWidth = 0.2f;
        [Tooltip("불씨 방출률(이동 1m당 개수).")]
        [Range(0, 120f)] public float emberRate = 30f;
        [Tooltip("불씨 크기(m). 실제로는 0.6~1.3배 랜덤.")]
        [Range(0.01f, 0.2f)] public float emberSize = 0.05f;
        [Tooltip("불씨 휘날림(노이즈 강도). 0=직선 낙하.")]
        [Range(0, 2f)] public float emberNoise = 0.35f;
        [Tooltip("탄착 후 잔불 유지 시간(초). 트레일 페이드·불씨 소멸 대기.")]
        [Range(0, 2f)] public float lingerTime = 0.5f;

        [Header("디버그")]
        [Tooltip("에디트 모드 정지 프레임 진행도(0~1). 라이브 프리뷰로 버스트·링에 푸시된다. 플레이 중에는 재생이 덮어씀.")]
        [Range(0, 1)] public float previewProgress = 0.35f;
    }
}
