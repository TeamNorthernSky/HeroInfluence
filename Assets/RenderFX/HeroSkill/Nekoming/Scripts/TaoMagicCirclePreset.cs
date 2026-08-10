using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// [프리셋 T1] 마법진 장판(TaoMagicCircle)의 튜닝 값.
    /// 동심원 링 + 육망성 + 룬 눈금 밴드 + 역회전 + 펄스 + 중심 광채. 에셋명: T1_TaoMagicCircle.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Taosenaiyo/T1_Tao Magic Circle Preset", fileName = "T1_TaoMagicCircle")]
    public class TaoMagicCirclePreset : ScriptableObject
    {
        [Header("★따름 (Alter 전용)")]
        [Tooltip("켜면 트랜스폼(형태·움직임)을 Basic 프리셋에서 읽는다. Alter 는 기본 ON — B/A는 색만 다르다.")]
        public bool followBasic;
        public TaoMagicCirclePreset basicRef;

        /// <summary>트랜스폼 정본 — Alter 가 따름이면 Basic.</summary>
        public TaoMagicCirclePreset TransformSource => followBasic && basicRef != null ? basicRef : this;

        [Header("크기 / 위치")]
        [Tooltip("마법진 반경(m)")]
        public float radius = 1.1f;
        [Tooltip("위로 띄우는 높이(m). 장판 = z-fighting 방지 / 수직(배리지 손앞) = 위치 미세 조정(260807 작동 확장)")]
        public float groundOffsetY = 0.02f;
        [Tooltip("캐릭터 전방(바라보는 방향)으로 미는 거리(m) — 수직 모드(배리지 손앞) 전용. 장판은 무시.")]
        [Range(0f, 5f)] public float forwardOffset = 0f;
        [Header("등장 (선형보간, 260807)")]
        [Tooltip("등장 보간 시간(초). 0 = 기존 동작(즉시, 엔벨로프만) — 부활 장판(T1)은 0 유지.")]
        [Range(0f, 2f)] public float appearTime = 0f;
        [Tooltip("등장 시작 크기 배율(0~1). appearTime 동안 1로 선형 증가.")]
        [Range(0f, 1f)] public float appearStartScale = 0.3f;
        [Tooltip("등장 시작 알파(0~1). appearTime 동안 1로 선형 증가 — 가산이라 밝기 배율로 작동.")]
        [Range(0f, 1f)] public float appearStartAlpha = 0f;

        [Header("색 / 밝기")]
        [Tooltip("전체 문양 색(HDR)")]
        [ColorUsage(true, true)] public Color color = new Color(1f, 0.85f, 0.35f);
        [Tooltip("전체 밝기")]
        [Range(0f, 8f)] public float intensity = 2.2f;

        [Header("중심 광채 (원 중심에서 번져나오는 빛, 260807 재편)")]
        [Tooltip("중심광 퍼짐(0~1). 값↑ = 넓게 번짐 / 값↓ = 좁게 집중.")]
        [Range(0f, 1f)] public float centerSpread = 0.72f;
        [Tooltip("중심광 깜빡임 진폭(0=깜빡임 없음). 문양은 고정 밝기 — 중심 빛만 맥동한다.")]
        [Range(0f, 1f)] public float centerPulseAmp = 0.18f;
        [Tooltip("중심광 깜빡임 주기(초) — 한 번 숨쉬는 데 걸리는 시간.")]
        [Range(0.1f, 5f)] public float centerPulsePeriod = 1.2f;

        [Header("동심원 링 (0=끔)")]
        [Tooltip("바깥 링 반경(마법진 반경 대비 0~1)")]
        [Range(0f, 1f)] public float ring1 = 0.95f;
        [Tooltip("중간 링 반경")]
        [Range(0f, 1f)] public float ring2 = 0.78f;
        [Tooltip("안쪽 링 반경")]
        [Range(0f, 1f)] public float ring3 = 0.38f;
        [Tooltip("링 요소 밝기(0=숨김, 1=기본)")]
        [Range(0f, 3f)] public float ringIntensity = 1f;
        [Tooltip("링/위성/스포크 공용 선 폭")]
        [Range(0.002f, 0.08f)] public float lineWidth = 0.012f;
        [Tooltip("선 가장자리 소프트(안티에일리어싱 느낌)")]
        [Range(0.001f, 0.05f)] public float lineSoft = 0.008f;

        [Header("육망성 (0=끔)")]
        [Tooltip("육망성(삼각 2개) 외접 반경. 내륜과 함께 회전")]
        [Range(0f, 1f)] public float hexRadius = 0.72f;
        [Tooltip("육망성 선 폭")]
        [Range(0.002f, 0.08f)] public float hexWidth = 0.011f;
        [Tooltip("육망성 요소 밝기(0=숨김, 1=기본)")]
        [Range(0f, 3f)] public float hexIntensity = 1f;

        [Header("겹정사각 8망성 (0=끔)")]
        [Tooltip("겹정사각(45도 오프셋 2개) 외접 반경. 육망성과 역방향·다른 속도로 회전")]
        [Range(0f, 1f)] public float squareRadius = 0.6f;
        [Tooltip("겹정사각 선 폭")]
        [Range(0.002f, 0.08f)] public float squareWidth = 0.009f;
        [Tooltip("겹정사각 요소 밝기(0=숨김, 1=기본)")]
        [Range(0f, 3f)] public float squareIntensity = 1f;

        [Header("위성 소원 (0=끔)")]
        [Tooltip("위성 궤도 반경. 외륜과 함께 회전")]
        [Range(0f, 1f)] public float satOrbit = 0.78f;
        [Tooltip("위성 원 개수")]
        [Range(2f, 16f)] public float satCount = 6f;
        [Tooltip("위성 원 하나의 반지름")]
        [Range(0.01f, 0.2f)] public float satRadius = 0.055f;
        [Tooltip("위성 소원 요소 밝기(0=숨김, 1=기본)")]
        [Range(0f, 3f)] public float satIntensity = 1f;

        [Header("방사 스포크 (0=끔)")]
        [Tooltip("방사 선분 개수. 내륜과 함께 회전")]
        [Range(0f, 32f)] public float spokeCount = 8f;
        [Tooltip("스포크 시작 반경(안쪽)")]
        [Range(0f, 1f)] public float spokeInner = 0.4f;
        [Tooltip("스포크 끝 반경(바깥쪽)")]
        [Range(0f, 1f)] public float spokeOuter = 0.74f;
        [Tooltip("스포크 요소 밝기(0=숨김, 1=기본)")]
        [Range(0f, 3f)] public float spokeIntensity = 1f;

        [Header("룬 눈금 밴드 (0=끔)")]
        [Range(0f, 1f)] public float tickRadius = 0.86f;
        [Range(0.005f, 0.12f)] public float tickWidth = 0.045f;
        [Range(4f, 128f)] public float tickCount = 48f;
        [Tooltip("눈금 채움 비율")]
        [Range(0.05f, 0.95f)] public float tickDuty = 0.45f;
        [Tooltip("룬 눈금 요소 밝기(0=숨김, 1=기본)")]
        [Range(0f, 3f)] public float tickIntensity = 1f;

        [Header("회전 (도/초)")]
        [Tooltip("내륜(육망성) 회전")]
        [Range(-180f, 180f)] public float rotInner = 18f;
        [Tooltip("외륜(눈금) 회전 — 부호 반대면 역방향")]
        [Range(-180f, 180f)] public float rotOuter = -12f;

        [Header("중심 광채 밝기 / 경계")]
        [Tooltip("중심광 밝기(0=문양만). 퍼짐·깜빡임은 위의 「중심 광채」 섹션.")]
        [Range(0f, 3f)] public float centerGlow = 0.55f;
        [Tooltip("바깥 가장자리 페이드 폭(쿼드 경계 은폐)")]
        [Range(0.005f, 0.3f)] public float edgeFade = 0.06f;
    }
}
