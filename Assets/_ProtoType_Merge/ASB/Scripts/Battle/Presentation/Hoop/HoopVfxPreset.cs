using UnityEngine;

/// <summary>
/// 절망의 굴렁쇠(FV40005) 외형·돌진·파괴 연출 설정. 수치·대상 판정은 소유하지 않는다.
/// 원본: JcYuliaVfxPreset. 실행 설정은 본게임용으로 독립되어 있다.
/// [2026-10-13 JC 결정] 재질은 현재 JC 경로의 공유 참조를 유지하고, 1차 완성 후 리팩토링을 검토한다.
/// HoopVfxPreset.asset -> JC/BattleTesting/Vfx/JC_YuliaEnergy.mat, JC_YuliaSmoke.mat, JC_DespairWheelFrame.mat.
/// 앞의 두 재질은 JcYuliaEnergy.shader도 공유한다. 테스트 폴더라는 이유로 이 자산·meta를 삭제하면 본게임도 깨진다.
/// JC 프리셋의 수치 변경은 이 프리셋과 자동 동기화되지 않지만, 공유 재질·셰이더 수정은 양쪽에 영향을 준다.
/// </summary>
[CreateAssetMenu(menuName = "ASB/Battle/Hoop VFX Preset", fileName = "HoopVfxPreset")]
public sealed class HoopVfxPreset : ScriptableObject
{
    [Header("Color")]
    [Tooltip("보랏빛 에너지 외곽의 HDR 색.")]
    [ColorUsage(true, true)] public Color energy = new Color(1.15f, .15f, 2.2f, 1f);
    [Tooltip("섬광 중심의 HDR 색.")]
    [ColorUsage(true, true)] public Color core = new Color(2.4f, 1.6f, 3f, 1f);
    [Tooltip("파괴 연기의 색. 알파는 최대 불투명도.")]
    public Color smoke = new Color(.12f, .035f, .19f, .65f);

    [Header("Material (JC 에셋 읽기 참조)")]
    [Tooltip("선·구체를 그리는 가산 재질 (JC_YuliaEnergy).")] public Material energyMaterial;
    [Tooltip("파괴 연기 재질 (JC_YuliaSmoke).")] public Material smokeMaterial;
    [Tooltip("바퀴 외곽 프레임·금속 파편 재질 (JC_DespairWheelFrame).")] public Material frameMaterial;

    [Header("Timing (전투 배속 기준 초)")]
    [Tooltip("에너지 선 두께(m).")]
    [Min(.005f)] public float lineWidth = .055f;
    [Tooltip("충전(Charge) 섬광 지속 시간.")]
    [Min(.05f)] public float pulseSeconds = .45f;
    [Tooltip("파괴 연기·파편이 흩어지는 시간.")]
    [Min(.1f)] public float destroySeconds = 2f;
    [Tooltip("소환진과 바퀴 상승 시간.")]
    [Min(.1f)] public float summonSeconds = .8f;

    [Header("Idle")]
    [Tooltip("대기 부유 진폭(m). 논리 슬롯은 움직이지 않는다.")]
    [Min(0f)] public float hoverHeight = .07f;
    [Tooltip("대기 부유 한 번 왕복 시간(전투 배속 기준 초). 율리아 연출 시뮬레이션 기준 3.8초.")]
    [Min(.1f)] public float hoverPeriodSeconds = 3.8f;

    [Header("Dash")]
    [Tooltip("돌진 전 충전 대기 시간.")]
    [Min(0f)] public float chargeSeconds = .35f;
    [Tooltip("대상까지 돌진하는 시간.")]
    [Min(.05f)] public float dashSeconds = .4f;
    [Tooltip("도착 후 피격 확정까지의 대기 시간.")]
    [Min(0f)] public float impactDelaySeconds = .08f;
    [Tooltip("피격 확정 후 시퀀스 종료까지의 대기 시간.")]
    [Min(0f)] public float afterImpactSeconds = .3f;
    [Tooltip("대상 위치에서 이 거리(m)만큼 앞에서 멈춘다. 0이면 대상과 겹친다.")]
    [Min(0f)] public float stopDistance = .5f;
    [Tooltip("돌진 궤적이 남는 시간(배속 1 기준 초).")]
    [Min(.01f)] public float trailSeconds = .28f;

    [Header("Ultra Mega VFX (선택 — 비우면 코드로 그린 임시 외형)")]
    [Tooltip("바퀴 대신 띄울 외형 프리팹(Magic Orb). 부유·회전은 그대로 적용된다.")]
    public GameObject wheelVisualPrefab;
    [Tooltip("wheelVisualPrefab의 보이는 반지름(m). 프리팹 스케일을 바꾸면 함께 맞출 것. 중심 높이는 반지름+부유 진폭+0.2m 이상. " +
             "FX_HoopOrb(MagicOrb08 월드 배율 0.389 = 율리아 키 2.06 ÷ 5.3)는 0.814 → 중심 1.134m(키의 55%, 시뮬레이션과 같음).")]
    [Min(0f)] public float wheelVisualRadius = .75f;
    [Tooltip("소환 시 코드 소환진(보라 링) 대신 바닥에 재생할 포털 프리팹(Portal Effects, 기획 Y07). 회전·크기는 프리팹 루트 값을 쓴다.")]
    public GameObject summonPortalPrefab;
    [Tooltip("파괴 시 코드 연기·파편 대신 재생할 프리팹(Stylized Smoke).")]
    public GameObject wheelBreakPrefab;
    [Tooltip("파괴 직전 구체 중심광이 한 번 커지는 시간(전투 배속 기준 초, 기획 Y10). 0이면 바로 연기. wheelVisualPrefab 사용 시에만.")]
    [Min(0f)] public float breakFlashSeconds = .12f;
    [Tooltip("중심광 최고점의 색 밝기 배율(VFX 노출 색 속성에 곱한다). 너무 크면 흰색으로 포화된다.")]
    [Min(1f)] public float breakFlashIntensity = 2.5f;
    [Tooltip("중심광 최고점의 구체 크기 배율.")]
    [Min(1f)] public float breakFlashScale = 1.25f;
    [Tooltip("돌진 중 트레일 대신 뒤쪽에 붙일 전기 아크 프리팹(ElectricArcEndpoints 필요).")]
    public GameObject dashArcPrefab;
    [Tooltip("납품 패키지의 굴렁쇠 돌진 입자. 기존 돌진 이동·피격 시각에 따라간다.")]
    public GameObject productionRushPrefab;
    [Tooltip("납품 패키지의 개별 파괴 폭발. 기존 사망 이벤트에서 재생한다.")]
    public GameObject productionExplosionPrefab;
    [Tooltip("아크 뒤쪽 끝점이 돌진 진행도에서 얼마나 뒤처지는지(0~1).")]
    [Range(0f, 1f)] public float dashArcLag = .35f;

    [Header("Summon Burst (율리아 소환 연출로 나타날 때, HoopSummonCastEffect)")]
    [Tooltip("소환 에너지가 방출될 때 작은 구체에서 원래 크기로 나타나는 시간(전투 배속 기준 초).")]
    [Min(.05f)] public float summonRevealSeconds = .35f;
    [Tooltip("소환 순간 구체가 튀는 최대 크기 배율. 시뮬레이션 1.22.")]
    [Range(1f, 1.5f)] public float summonBurstPeak = 1.22f;
    [Tooltip("최대 크기까지 커지는 시간(전투 배속 기준 초). 시뮬레이션 0.045.")]
    [Min(.001f)] public float summonBurstRiseSeconds = .045f;
    [Tooltip("원래 크기로 돌아오는 시간(전투 배속 기준 초). 시뮬레이션 0.115.")]
    [Min(.001f)] public float summonBurstSettleSeconds = .115f;
}
