using UnityEngine;

namespace JC.BattleTesting.Vfx
{
    // 대응: RenderFX/HeroSkill의 ProjectileOrbPreset/ChainLightningPreset. 수치·대상 판정은 소유하지 않습니다.
    // 참고: 이기석/전투 컨셉/율리아 애니메이션 연출 기획_1.1, motion_01/09/10/11, amplifier_design_v2.
    [CreateAssetMenu(menuName = "JC/전투 테스트/율리아 VFX 설정", fileName = "JC_YuliaVfxPreset")]
    public sealed class JcYuliaVfxPreset : ScriptableObject
    {
        [Tooltip("보랏빛 에너지 외곽의 HDR 색입니다. 원본 히어로 재질에는 적용하지 않습니다.")]
        [ColorUsage(true, true)] public Color energy = new Color(1.15f, .15f, 2.2f, 1);
        [Tooltip("번개·섬광 중심의 HDR 색입니다.")]
        [ColorUsage(true, true)] public Color core = new Color(2.4f, 1.6f, 3f, 1);
        [Tooltip("증폭기와 굴렁쇠 파괴 연기의 색입니다. 알파는 최대 불투명도입니다.")]
        public Color smoke = new Color(.12f, .035f, .19f, .65f);
        [Tooltip("선과 구체를 그리는 JC 전용 가산 재질입니다.")] public Material energyMaterial;
        [Tooltip("부드러운 파괴 연기의 JC 전용 알파 재질입니다.")] public Material smokeMaterial;
        [Tooltip("굴렁쇠 임시 외곽 프레임 재질입니다. 정식 모델이 오면 별도 외형으로 교체할 수 있습니다.")] public Material frameMaterial;
        [Tooltip("번개 중심선 두께(m)입니다. 외곽 빛은 이 값의 3배입니다.")]
        [Min(.005f)] public float lineWidth = .055f;
        [Tooltip("피격 섬광과 에너지 축적 표시의 지속 시간(전투 배속 기준 초)입니다.")]
        [Min(.05f)] public float pulseSeconds = .45f;
        [Tooltip("증폭기 구체가 소멸하고 연기가 흩어지는 시간(전투 배속 기준 초)입니다.")]
        [Min(.1f)] public float destroySeconds = 2f;
        [Tooltip("구체가 아래에서 복구되는 시간(전투 배속 기준 초)입니다.")]
        [Min(.1f)] public float recoverSeconds = 1.1f;
        [Tooltip("소환진과 굴렁쇠 상승 연출 시간(전투 배속 기준 초)입니다.")]
        [Min(.1f)] public float summonSeconds = .8f;
        [Tooltip("스킬 공급 연출 시간(전투 배속 기준 초)입니다. 전투 규칙·예약 소비와 독립입니다.")]
        [Min(.1f)] public float supplySeconds = 1f;
        [Tooltip("장막 전환 연출 시간(전투 배속 기준 초)입니다. 현재 행동 경계에서 재생합니다.")]
        [Min(.1f)] public float phaseSeconds = 1.2f;
        [Tooltip("굴렁쇠의 대기 부유 진폭(m)입니다. 논리 슬롯이나 사거리는 움직이지 않습니다.")]
        [Min(0)] public float hoverHeight = .07f;
        [Tooltip("장막의 가로·세로·깊이 반지름(m)입니다. 임시 율리아 외형을 감싸는 시각 크기입니다.")]
        public Vector3 veilRadius = new Vector3(.65f, 1.05f, .55f);
    }
}
