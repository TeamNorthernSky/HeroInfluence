using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 발사체 구체 VFX의 튜닝 값을 한곳에 모은 프리셋(ChargeOrbPreset의 발사체판).
    /// 커스텀 인스펙터(ProjectileOrbPresetEditor)의 "프리팹에 적용"으로
    /// ProjectileOrbCore.mat + ProjectileOrb 프리팹(ProjectileVfx + Sparkles PS) + 전용 스파클 재질에 일괄 반영.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/2_Heal Projectile Preset", fileName = "2_HealProjectilePreset")]
    public class ProjectileOrbPreset : ScriptableObject
    {
        [Header("★따름 (Alter 전용)")]
        [Tooltip("켜면 트랜스폼(비행·크기·시작점·탄착점)을 Basic 프리셋에서 읽는다. Alter 는 기본 ON.\n" +
                 "Basic 과 Alter 는 색만 다르고 형태·움직임은 같아야 한다는 규칙의 장치.")]
        public bool followBasic;
        [Tooltip("따를 Basic 프리셋. followBasic 이 켜져 있을 때만 쓰인다.")]
        public ProjectileOrbPreset basicRef;

        [Header("★구체 룩 공유 — 「거기 생겨난 구체가 날아간다」")]
        [Tooltip("차지 오브 프리셋. 지정하면 코어/림 룩(색·밝기·스파이크)은 이쪽 값을 쓴다.\n" +
                 "트레일 등 이동 이펙트만 이 프리셋 소유로 남는다.")]
        public ChargeOrbPreset chargeRef;

        [Header("★시작 위치 (시전자 기준)")]
        [Tooltip("켜면 차지 오브의 위치를 시작점으로 쓴다(기본).\n" +
                 "호출자가 차지의 실제 스폰 위치를 기억해 이어 주고, 없으면 chargeRef 의 발사 시작점을 읽는다.")]
        public bool useChargeOrbPosition = true;
        [Tooltip("토글 해제 시에만 쓰는 자체 시작점 — 기준 소켓(비면 캐릭터 루트).")]
        public string spawnSocketName;
        [Tooltip("토글 해제 시에만 쓰는 자체 시작점 — 오프셋(m, 기준점 회전 적용·스케일 무시).")]
        public Vector3 spawnOffset = new Vector3(0.55f, 2.0f, 0.15f);

        [Header("★탄착점 (대상 기준)")]
        [Tooltip("기준 소켓(비면 대상 루트 = 발밑).")]
        public string impactSocketName;
        [Tooltip("탄착 오프셋(m). 3등신 캐릭터는 머리 높이(~1.5)가 자연스럽다.")]
        public Vector3 impactOffset = new Vector3(0f, 1.5f, 0f);

        /// <summary>트랜스폼 정본 — Alter 가 따름이면 Basic, 아니면 자기 자신.</summary>
        public ProjectileOrbPreset TransformSource => followBasic && basicRef != null ? basicRef : this;

        [Header("코어 — 색/밝기 (chargeRef 지정 시 무시 — 차지 오브 룩을 따른다)")]
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

        [Header("코어/림 구조 (2오브젝트 사이즈)")]
        [Tooltip("CoreInner 상대 크기(0~1). 작을수록 코어가 안쪽 → 갭↑, 스파이크가 외곽선 안에 갇힘")]
        [Range(0.1f, 1.2f)] public float coreSize = 0.72f;
        [Tooltip("RimShell 상대 크기(보통 1.0). core/rim 크기 차이가 갭을 만듦")]
        [Range(0.3f, 1.5f)] public float rimSize = 1.0f;

        [Header("코어 — 표면 스파이크 (발사체 기본 매끈 0/0)")]
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

        [Header("오브 크기")]
        [Tooltip("투사체 구체의 전체 월드 크기입니다. 코어·주변 효과의 기준 크기를 함께 바꿉니다.")]
        public float worldSize = 0.11f;

        [Header("비행")]
        [Tooltip("비행 속력(m/s).")]
        public float speed = 6f;
        [Tooltip("포물선 정점 높이(m).")]
        public float arcHeight = 1.2f;
        [Tooltip("궤적 트레일 유지 시간(초). 길수록 궤적이 길게 남는다.")]
        public float trailTime = 0.4f;
        [Tooltip("켜면 trailTime을 무시하고 '트레일 길이(월드 m) / 속도'로 자동 계산.")]
        public bool trailAutoBySpeed = false;
        [Tooltip("trailAutoBySpeed일 때 목표 궤적 길이(월드 m).")]
        public float trailLengthWorld = 2.5f;

        [Header("도착 버스트")]
        [Tooltip("도착 버스트가 기존 크기에서 확대되는 배수입니다. 1이면 추가 확대가 없습니다.")]
        public float burstScaleMul = 1.5f;
        [Tooltip("버스트 전체 재생 시간(초).")]
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
        [Tooltip("광선 입자가 생성될 때 크기의 최댓값(m)입니다.")]
        public float raySizeMax = 0.025f;
        [Tooltip("방사 속도(m/s) — 수명 = 도달반경/속도")]
        public float raySpeed = 0.7f;
        [Tooltip("늘어남 배수 — 클수록 길고 뾰족")]
        public float rayLengthScale = 2.2f;
        [Tooltip("투사체 주변으로 뻗는 광선의 색입니다.")]
        [ColorUsage(true, true)] public Color rayColor = new Color(1f, 0.95f, 0.6f) * 2.2f;

        [Header("스타버스트(스파클)")]
        [Tooltip("발생 주기(초당 개수)")]
        public float sparkleRate = 10f;
        [Tooltip("별 모양 반짝임 크기의 최솟값(m)입니다.")]
        public float sparkleSizeMin = 0.03f;
        [Tooltip("별 모양 반짝임 크기의 최댓값(m)입니다. 최솟값과 벌릴수록 크기 차이가 커집니다.")]
        public float sparkleSizeMax = 0.07f;
        [Tooltip("각 스타 수명(초)")]
        public float sparkleLifetime = 0.45f;
        [Tooltip("코어 주변 흩뿌림 반경(m)")]
        public float sparkleShapeRadius = 0.06f;
        [Tooltip("구체 주변의 별 모양 반짝임 색입니다.")]
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
        [Tooltip("투사체의 별 모양 중심광 색입니다.")]
        [ColorUsage(true, true)] public Color starColor = new Color(1f, 0.9f, 0.35f) * 2.0f;
    }
}
