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
}
