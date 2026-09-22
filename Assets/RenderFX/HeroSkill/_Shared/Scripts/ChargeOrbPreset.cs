using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 차징 구체 VFX의 모든 튜닝 값을 한곳에 모은 프리셋.
    /// 커스텀 인스펙터(ChargeOrbPresetEditor)의 "프리팹에 적용" 버튼으로
    /// 재질(ChargeOrbCore.mat) + 오브 프리팹(ChargeOrb) + 팔로워 프리팹(ChargeTrailFollower)에 일괄 반영.
    /// "현재값 캡처"로 역방향(현 상태 → 프리셋)도 가능.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/1_Heal Charge Orb Preset", fileName = "1_HealChargeOrbPreset")]
    public class ChargeOrbPreset : ScriptableObject
    {
        [Header("★따름 (Alter 전용)")]
        [Tooltip("켜면 크기·성장·발사 시작점을 연결된 Basic 프리셋에서 읽습니다. 끄면 이 프리셋의 값을 독립적으로 사용합니다. 색상은 따름과 관계없이 각 변종의 값을 사용합니다.")]
        public bool followBasic;
        [Tooltip("따를 Basic 프리셋. followBasic 이 켜져 있을 때만 쓰인다.")]
        public ChargeOrbPreset basicRef;

        [Header("★발사 시작점 (시전자 기준) — 차지가 차오르는 자리 = 투사체가 출발하는 자리")]
        [Tooltip("기준 소켓(비면 캐릭터 루트 = 발밑 기준점).")]
        public string spawnSocketName;
        [Tooltip("오프셋(m, 기준점 회전 적용·스케일 무시).")]
        public Vector3 spawnOffset = new Vector3(0.55f, 2.0f, 0.15f);

        /// <summary>트랜스폼 정본 — Alter 가 따름이면 Basic, 아니면 자기 자신.</summary>
        public ChargeOrbPreset TransformSource => followBasic && basicRef != null ? basicRef : this;

        [Header("코어 — 색/밝기")]
        [Tooltip("중심 코어의 색입니다. 테두리 색과 별도로 중심부의 인상을 조절합니다.")]
        [ColorUsage(true, true)] public Color coreColor = new Color(1f, 0.88f, 0.14f);
        [Tooltip("림(경계 밴드) 색")]
        [ColorUsage(true, true)] public Color rimColor = new Color(1f, 0.60f, 0.10f);
        [Tooltip("중심 코어의 발광 배수입니다. 높일수록 밝아지며 0이면 코어 발광을 없앱니다.")]
        [Range(0, 8)] public float coreIntensity = 1.7f;
        [Tooltip("림 밝기")]
        [Range(0, 8)] public float rimIntensity = 2.0f;
        [Tooltip("코어 타이트니스 — 클수록 코어가 작고 또렷")]
        [Range(0.5f, 10)] public float corePower = 3.4f;
        [Tooltip("림 타이트니스 — 클수록 외곽선이 얇음")]
        [Range(0.5f, 16)] public float rimPower = 4.5f;
        [Tooltip("코어와 테두리 사이의 어두운 간격 강도입니다. 0이면 간격에 의한 감쇠를 없앱니다.")]
        [Range(0, 1)] public float gapStrength = 0.5f;

        [Header("코어 — 표면 스파이크 (코어/림 분리)")]
        [Tooltip("코어 표면의 뾰족한 요철 강도입니다. 0이면 요철 없이 매끈해집니다.")]
        [Range(0, 0.6f)] public float coreSpikeAmount = 0.10f;   // 정면(코어)
        [Tooltip("테두리 실루엣의 뾰족한 요철 강도입니다. 높일수록 외곽이 거칠어지며 0이면 요철이 사라집니다.")]
        [Range(0, 0.6f)] public float rimSpikeAmount = 0.22f;    // 실루엣(림)
        [Tooltip("표면 요철 무늬의 반복 밀도입니다. 높일수록 더 촘촘한 돌기가 생깁니다.")]
        [Range(0.5f, 12)] public float spikeFreq = 5.5f;
        [Tooltip("표면 요철이 변화하는 속도입니다. 0이면 무늬의 시간 변화가 멈춥니다.")]
        [Range(0, 8)] public float spikeSpeed = 2.5f;
        [Tooltip("표면 요철의 날카로움입니다. 높일수록 돌기 끝이 좁고 뾰족해집니다.")]
        [Range(1, 8)] public float spikeSharp = 3.0f;

        [Header("오브 — 크기/등장")]
        [Tooltip("차징 구체가 나타날 때의 월드 크기(m)입니다. 종료 크기까지 성장합니다.")]
        public float startWorldSize = 0.015f;
        [Tooltip("차징 구체의 성장이 끝났을 때 월드 크기(m)입니다.")]
        public float endWorldSize = 0.11f;
        [Tooltip("차징 구체가 시작 크기에서 종료 크기까지 커지는 시간(초)입니다. 짧을수록 빠르게 커집니다.")]
        public float growDuration = 0.5f;

        [Header("스파크(코어→외곽 입자)")]
        [Tooltip("스파크 입자 크기의 최솟값(m)입니다. 최댓값과의 사이에서 크기가 선택됩니다.")]
        public float sparkSizeMin = 0.06f;
        [Tooltip("스파크 입자 크기의 최댓값(m)입니다. 최솟값과 벌릴수록 크기 차이가 커집니다.")]
        public float sparkSizeMax = 0.16f;
        [Tooltip("스파크 입자의 초당 생성 수입니다. 0이면 새 입자를 만들지 않습니다.")]
        public float sparkRate = 40f;
        [Tooltip("스파크 입자의 초기 이동 속력(m/s)입니다. 높일수록 빠르게 퍼집니다.")]
        public float sparkSpeed = 1.0f;
        [Tooltip("구체 주변으로 흩어지는 스파크 입자의 색입니다.")]
        [ColorUsage(true, true)] public Color sparkColor = new Color(1f, 0.82f, 0.25f) * 2f;

        [Header("트레일 밴드(파티클 트레일)")]
        [Tooltip("트레일 밴드 입자의 초당 생성 수입니다. 0이면 새 입자를 만들지 않습니다.")]
        public float bandRate = 6f;
        [Tooltip("밴드 입자의 수명(초)입니다. 길수록 입자가 오래 남습니다.")]
        public float bandStartLifetime = 1.1f;
        [Tooltip("밴드 입자가 생성될 때의 크기(m)입니다.")]
        public float bandStartSize = 0.018f;
        [Tooltip("밴드 트레일의 수명 배수입니다. 입자 수명에 곱해 잔상이 남는 시간을 정합니다.")]
        public float bandTrailLifetime = 1.0f;
        [Tooltip("밴드 입자가 생성 지점의 이동 속도를 물려받는 배수입니다. 0이면 이동 속도를 물려받지 않습니다.")]
        [Range(0, 1.5f)] public float bandInheritVelocity = 0.7f;
        [Tooltip("밴드 입자가 생성되는 구형 영역의 반경(m)입니다. 클수록 구체 주변으로 넓게 퍼집니다.")]
        public float bandShapeRadius = 0.035f;
        [Tooltip("차징 구체 주변을 도는 트레일 밴드의 색입니다.")]
        [ColorUsage(true, true)] public Color bandColor = new Color(1f, 0.95f, 0.62f) * 1.5f;

        [Header("반짝임(4각 스타 / 스타버스트)")]
        [Tooltip("발생 주기(초당 개수)")]
        public float sparkleRate = 6f;
        [Tooltip("별 모양 반짝임 크기의 최솟값(m)입니다.")]
        public float sparkleSizeMin = 0.02f;
        [Tooltip("별 모양 반짝임 크기의 최댓값(m)입니다. 최솟값과 벌릴수록 크기 차이가 커집니다.")]
        public float sparkleSizeMax = 0.05f;
        [Tooltip("각 스타 수명(초)")]
        public float sparkleLifetime = 0.45f;
        [Tooltip("코어 주변 흩뿌림 반경(m)")]
        public float sparkleShapeRadius = 0.03f;
        [Tooltip("구체 주변의 별 모양 반짝임 색입니다.")]
        [ColorUsage(true, true)] public Color sparkleColor = new Color(1f, 0.97f, 0.7f) * 1.8f;
    }
}
