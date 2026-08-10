using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「쓰러지면 안돼」(Taosenaiyo).
    /// [프리셋 T9] 광역 공격(tao_barrage, 「적 전체 광선 다발」)의 튜닝 값 — 260807 본 연출 1차.
    /// 구성: 발사 원점(시전자 상공 월드 오프셋) → 코어(구형 프레넬+스타) → 경사 스트릭 시트(수렴)
    ///        + 노이즈 베일 → 낙하 스타버스트(트레일 꼬리) → 피격(현재 광 기둥 플레이스홀더, 교체 예정).
    /// 색·룩은 변종별 재질(TaoBarrageSheet/Veil/Core ±)이 정본 — 프리셋은 기하·타이밍·밀도만.
    /// 에셋명: T9_TaoBarrage_Basic/_Alter.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Taosenaiyo/T9_Tao Barrage Preset", fileName = "T9_TaoBarrage")]
    public class TaoBarragePreset : ScriptableObject
    {
        /// <summary>피격 발동 순서. 동시가 기본(260807 사용자 확정) — 순차 연출 여지는 보존.</summary>
        public enum HitOrder
        {
            [InspectorName("동시 (기본)")] Simultaneous = 0,
            [InspectorName("원점 가까운 순")] ByOriginDistance = 1,
            [InspectorName("-X-Z 코너부터 (축합)")] ByAxisSum = 2,
        }

        [Header("★따름 (Alter 전용)")]
        [Tooltip("켜면 트랜스폼(원점·시트·타이밍·밀도)을 Basic 프리셋에서 읽는다. Alter 는 기본 ON.")]
        public bool followBasic;
        public TaoBarragePreset basicRef;

        /// <summary>트랜스폼 정본 — Alter 가 따름이면 Basic.</summary>
        public TaoBarragePreset TransformSource => followBasic && basicRef != null ? basicRef : this;

        [Header("★발사 원점 (시전자 기준 월드)")]
        [Tooltip("원점 높이(m). 코어·시트·낙하 스타가 전부 여기서 파생된다.")]
        [Range(2f, 20f)] public float originHeight = 7f;
        [Tooltip("타격 중심 반대쪽으로 물러나는 거리(m) — 시전자 뒤쪽 상공에서 적진으로 쏟아지는 경사.")]
        [Range(0f, 10f)] public float originBack = 2.5f;

        [Header("손앞 마법진 — ★위치만 여기(T9). 크기·문양·색은 T10_TaoBarrageCircle")]
        [Tooltip("마법진 손 소켓 이름(시전자 계층 검색). 비면 시전자 루트 기준.\n" +
                 "★소유 구분(260807): T9 = 어디에 놓는가(위치) / T10 = 어떻게 생겼는가(radius·문양·색).")]
        public string handSocketName = "Socket_R_VFX";
        [Tooltip("손 소켓 기준 오프셋(m, 회전 따름). 크기 조절은 T10.radius 로.")]
        public Vector3 handOffset = new Vector3(0f, 0f, 0.15f);

        [Header("스트릭 시트")]
        [Tooltip("시트 장수(빔 축 둘레 부채 배치). 3이면 near/mid/far 볼륨감.")]
        [Range(1, 5)] public int sheetCount = 3;
        [Tooltip("시트 부채각(도) — 시트들이 빔 축 둘레로 벌어지는 각.")]
        [Range(0f, 90f)] public float sheetSpreadDeg = 38f;
        [Tooltip("시트 폭(m, 지면 끝 기준 — 원점 쪽은 셰이더 수렴이 좁힌다).")]
        [Range(0.5f, 15f)] public float sheetWidth = 5.5f;
        [Tooltip("시트 길이 여유 배율 — 원점→타격 중심 거리에 곱해 지면 너머까지 덮는다.")]
        [Range(1f, 2f)] public float sheetLengthMul = 1.25f;
        [Tooltip("베일(노이즈 바탕) 폭 배율 — 시트보다 넓게 감싸 볼류메트릭 눈속임.")]
        [Range(1f, 3f)] public float veilWidthMul = 1.7f;

        [Header("코어 (원점)")]
        [Tooltip("구형 프레넬 코어 지름(m).")]
        [Range(0.2f, 5f)] public float coreSize = 1.4f;
        [Tooltip("코어 스타버스트 파티클 크기(m).")]
        [Range(0.2f, 6f)] public float coreStarSize = 2.6f;

        [Header("낙하 스타버스트")]
        [Tooltip("초당 방출 수.")]
        [Range(0f, 200f)] public float starRate = 70f;
        [Tooltip("낙하 속도 최소(m/s).")]
        [Range(1f, 40f)] public float starSpeedMin = 9f;
        [Tooltip("낙하 속도 최대(m/s).")]
        [Range(1f, 40f)] public float starSpeedMax = 15f;
        [Tooltip("스타 크기 최소(m).")]
        [Range(0.02f, 1.5f)] public float starSizeMin = 0.14f;
        [Tooltip("스타 크기 최대(m).")]
        [Range(0.02f, 1.5f)] public float starSizeMax = 0.34f;
        [Tooltip("방출 콘 반각(도) — 시트 부채와 맞출 것.")]
        [Range(1f, 45f)] public float starConeDeg = 14f;
        [Tooltip("트레일 꼬리 유지 시간(초) — 진행 반대쪽으로 늘어지는 유성 꼬리.")]
        [Range(0.05f, 1.5f)] public float starTrailTime = 0.3f;

        [Header("타이밍")]
        [Tooltip("전개(페이드인) 시간(초) — 마법진·코어·시트가 차오르는 구간.")]
        [Range(0.05f, 1.5f)] public float buildTime = 0.25f;
        [Tooltip("유지 시간(초).")]
        [Range(0.2f, 8f)] public float sustainTime = 1.4f;
        [Tooltip("페이드아웃(초).")]
        [Range(0.05f, 2f)] public float fadeOutTime = 0.45f;
        [Tooltip("피격(광 기둥) 발동 지연(초) — 전개 완료 기준.")]
        [Range(0f, 1f)] public float hitDelay = 0.12f;

        [Header("피격 (현재 플레이스홀더 — 교체 예정)")]
        [Tooltip("피격 발동 순서. 동시 = stagger 무시.")]
        public HitOrder hitOrder = HitOrder.Simultaneous;
        [Tooltip("순차 모드일 때 대상별 시차(초).")]
        [Range(0f, 0.5f)] public float stagger = 0f;
        [Tooltip("기둥 폭(m)")]
        [Range(0.05f, 2f)] public float pillarWidth = 0.45f;
        [Tooltip("기둥 높이(m)")]
        [Range(0.5f, 8f)] public float pillarHeight = 3f;
        [Tooltip("기둥 하나의 수명(초)")]
        [Range(0.1f, 3f)] public float pillarLife = 0.55f;

        [Header("색 (변종별 — 피격 기둥 전용. 시트·코어 색은 재질이 정본)")]
        [Tooltip("기둥 색(HDR)")]
        [ColorUsage(true, true)] public Color color = new Color(1f, 0.85f, 0.4f);
        [Tooltip("기둥 밝기 배율")]
        [Range(0f, 8f)] public float intensity = 2f;
    }
}
