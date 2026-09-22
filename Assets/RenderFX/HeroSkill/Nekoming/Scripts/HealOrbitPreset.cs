using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 힐 오라 궤도 구체(E-1)의 튜닝 값을 한곳에 모은 프리셋.
    /// 커스텀 인스펙터(HealOrbitPresetEditor)의 "프리팹에 적용"으로
    /// HealOrbitCoreInner/RimShell.mat(전용 재질) + HealOrbit 프리팹(HealOrbitVfx + Orb 크기/코어림 사이즈)에 일괄 반영.
    /// ★셰이더 재질은 발사체와 분리된 전용 재질이라, 여기 값이 발사체에 영향 없음.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/3_Heal Orbit Preset", fileName = "3_HealOrbitPreset")]
    public class HealOrbitPreset : ScriptableObject
    {
        [Header("★따름 (Alter 전용)")]
        [Tooltip("켜면 트랜스폼(궤도·크기·착지점)을 Basic 프리셋에서 읽는다. Alter 는 기본 ON.")]
        public bool followBasic;
        [Tooltip("Basic 따름이 켜져 있을 때 위치·형태 기준으로 읽을 프리셋입니다. 연결이 비어 있으면 자기 값을 사용합니다.")]
        public HealOrbitPreset basicRef;

        /// <summary>트랜스폼 정본 — Alter 가 따름이면 Basic.</summary>
        public HealOrbitPreset TransformSource => followBasic && basicRef != null ? basicRef : this;

        [Header("★착지점 (대상 기준)")]
        [Tooltip("착지 오라 생성 오프셋(m). 기본 0 = 대상 발밑. 폐기된 위치 프리셋의 heal_land 항목이 여기로 이관됨.")]
        public Vector3 landOffset = Vector3.zero;

        [Header("궤도 (거동)")]
        [Tooltip("궤도 반경(m)")]
        public float orbitRadius = 1.0f;
        [Tooltip("궤도면 높이(중심 기준, m)")]
        public float orbitHeight = 0.35f;
        [Tooltip("각속도(도/초). +면 시계 반대")]
        public float angularSpeed = 220f;
        [Tooltip("시작 각도(도)")]
        public float startAngle = 0f;
        [Tooltip("궤도면 기울기(도). 0=수평 링")]
        public float tiltDeg = 0f;
        // ※ 총 재생시간·전후 페이드는 이펙트 전체 대상 → 0_HealAuraMasterPreset로 이관.

        [Header("오브 크기")]
        [Tooltip("궤도 구체 월드 지름(m)")]
        public float orbWorldSize = 0.11f;
        [Tooltip("CoreInner 상대 크기(0~1). 작을수록 코어가 안쪽 → 갭↑")]
        [Range(0.1f, 1.2f)] public float coreSize = 0.72f;
        [Tooltip("RimShell 상대 크기(보통 1.0). core/rim 차이가 갭")]
        [Range(0.3f, 1.5f)] public float rimSize = 1.0f;

        [Header("코어 — 색/밝기 (CoreInner)")]
        [Tooltip("중심 코어의 색입니다. 테두리 색과 별도로 중심부의 인상을 조절합니다.")]
        [ColorUsage(true, true)] public Color coreColor = new Color(1f, 0.88f, 0.14f);
        [Tooltip("중심 코어의 발광 배수입니다. 높일수록 밝아지며 0이면 코어 발광을 없앱니다.")]
        [Range(0, 8)] public float coreIntensity = 1.7f;
        [Tooltip("코어 타이트니스 — 클수록 코어가 작고 또렷")]
        [Range(0.5f, 10)] public float corePower = 3.4f;

        [Header("외곽선 — 색/밝기 (RimShell)")]
        [Tooltip("림(경계 밴드) 색")]
        [ColorUsage(true, true)] public Color rimColor = new Color(1f, 0.60f, 0.10f);
        [Tooltip("림 밝기")]
        [Range(0, 8)] public float rimIntensity = 2.0f;
        [Tooltip("림 타이트니스 — 클수록 외곽선이 얇음")]
        [Range(0.5f, 16)] public float rimPower = 4.5f;

        [Header("코어 표면 스파이크 (기본 매끈 0/0)")]
        [Tooltip("코어 표면의 뾰족한 요철 강도입니다. 0이면 요철 없이 매끈해집니다.")]
        [Range(0, 0.6f)] public float coreSpikeAmount = 0f;
        [Tooltip("테두리 실루엣의 뾰족한 요철 강도입니다. 높일수록 외곽이 거칠어지며 0이면 요철이 사라집니다.")]
        [Range(0, 0.6f)] public float rimSpikeAmount = 0f;
        [Tooltip("표면 요철 무늬의 반복 밀도입니다. 높일수록 더 촘촘한 돌기가 생깁니다.")]
        [Range(0.5f, 12)] public float spikeFreq = 5.5f;
        [Tooltip("표면 요철이 변화하는 속도입니다. 0이면 무늬의 시간 변화가 멈춥니다.")]
        [Range(0, 8)] public float spikeSpeed = 2.5f;
        [Tooltip("표면 요철의 날카로움입니다. 높일수록 돌기 끝이 좁고 뾰족해집니다.")]
        [Range(1, 8)] public float spikeSharp = 3.0f;
    }
}
