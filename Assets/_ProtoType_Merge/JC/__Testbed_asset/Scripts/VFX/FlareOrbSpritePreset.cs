using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「플레어 봄」 차징 오브 F2(2D 스프라이트 방식) 프리셋.
    /// 시선축 빌보드 쿼드 위 2D 극좌표에 획을 그린다(FlareAuraSprite 셰이더).
    /// 후면 레이어 없음, 큐 3002(코어 위). F1과는 FlareOrbLayerToggle로 택일.
    /// 녹색원(maxReach)은 강제 종료 경계일 뿐, 생명주기 로직과 독립이다.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Flare Orb Sprite Preset (F2 2D)", fileName = "F2_FlareOrbSpritePreset")]
    public class FlareOrbSpritePreset : ScriptableObject
    {
        [Header("스프라이트 배치")]
        [Tooltip("빌보드 쿼드 월드 크기(m). 도달한계 지름보다 넉넉하게(경계 클리핑 방지 여백).")]
        public float worldSize = 1.8f;
        [Tooltip("기준 원(검은 원) 반경(uv 0~1). 획 생성의 기준 경계 — 코어 실루엣에 맞춘다.")]
        [Range(0.05f, 0.9f)] public float baseRadius = 0.42f;
        [Tooltip("도달 한계(녹색 원) 반경(uv). 획이 닿으면 폭이 강제로 줄며 소멸(생명주기와 독립).")]
        [Range(0.1f, 1f)] public float maxReach = 0.75f;
        [Tooltip("강제 종료 경계의 소멸 소프트니스.")]
        [Range(0.01f, 0.3f)] public float reachSoft = 0.08f;

        [Header("혀 — 색")]
        [Tooltip("획 기본 색(진한 골드/주황).")]
        [ColorUsage(true, true)] public Color tongueColor = new Color(1.05f, 0.62f, 0.16f);
        [Tooltip("내부 하이라이트 색(크림).")]
        [ColorUsage(true, true)] public Color tongueHighlightColor = new Color(1.45f, 1.25f, 0.80f);
        [Tooltip("발광 배수.")]
        [Range(0, 6)] public float tongueEmission = 1.2f;

        [Header("섹터/폭")]
        [Tooltip("둘레 섹터(획) 개수.")]
        [Range(3, 32)] public int laneCount = 12;
        [Tooltip("획 절반 폭(섹터 셀 기준 0~0.5).")]
        [Range(0.02f, 0.5f)] public float laneWidth = 0.16f;
        [Tooltip("획별 폭 지터.")]
        [Range(0, 1)] public float laneWidthJitter = 0.35f;
        [Tooltip("획별 위치 지터(셀 내 중심 오프셋).")]
        [Range(0, 0.8f)] public float lanePosJitter = 0.35f;
        [Tooltip("획별 기울기 지터.")]
        [Range(0, 2f)] public float laneTiltJitter = 0.5f;
        [Tooltip("폭 물결 스케일.")]
        [Range(0.5f, 12f)] public float widthNoiseScale = 3.5f;
        [Tooltip("폭 물결 폭. 0=일정한 굵기.")]
        [Range(0, 1)] public float widthNoiseAmount = 0.4f;

        [Header("생성 대역(반경)")]
        [Tooltip("생성 반경 시작(uv). 생성 범위는 여기서 이동거리만큼 안쪽으로 확장된다(내측 유량 정상화).")]
        [Range(0, 1)] public float birthRadiusStart = 0.34f;
        [Tooltip("생성 반경 끝(uv).")]
        [Range(0, 1)] public float birthRadiusEnd = 0.6f;
        [Tooltip("생성 반경 산포. 0=시작 반경 고정, 0.5=전체 대역 랜덤.")]
        [Range(0, 0.5f)] public float birthRadiusJitter = 0.25f;
        [Tooltip("내측 생성 가중. 1=균등, 클수록 기준원 근처에서 더 자주 생성.")]
        [Range(1f, 4f)] public float birthInnerBias = 1.8f;
        [Tooltip("획별 길이 지터.")]
        [Range(0, 1)] public float laneLengthJitter = 0.35f;
        [Tooltip("획 최대 길이(uv 반경 단위).")]
        [Range(0.05f, 0.8f)] public float laneMaxLength = 0.22f;
        [Tooltip("외측 생성 축소. 기준원 밖에서 태어난 획일수록 길이·두께가 줄어든다.")]
        [Range(0, 1)] public float outerShrink = 0.6f;

        [Header("생명주기")]
        [Tooltip("획 이동 거리(uv). 태어난 반경에서 바깥으로 이만큼 타고 나가며 소멸.")]
        [Range(0.02f, 0.5f)] public float laneTravelDist = 0.15f;
        [Tooltip("획 이동 속도(흐름 대비 비율). 0=제자리 생멸.")]
        [Range(0, 1)] public float laneTravelSpeed = 0.3f;
        [Tooltip("생명주기 페이드 피크. 0=시작 최대→끝 0, 0.5=중간 피크, 1=페이드 없음. 획 전체 균일 알파.")]
        [Range(0, 1)] public float laneLifeFadePeak = 0.4f;

        [Header("실루엣")]
        [Tooltip("동공 프로파일 지수. 클수록 획이 날씬하고 양끝이 예리.")]
        [Range(0.5f, 6f)] public float taperSharp = 1.4f;
        [Tooltip("내부 하이라이트 폭 비율.")]
        [Range(0, 1)] public float highlightRatio = 0.45f;
        [Tooltip("에지 소프트 비율. 0=하드컷.")]
        [Range(0, 1)] public float edgeSoftRatio = 0.25f;

        [Header("운동")]
        [Tooltip("흐름 속도. 생명주기·폭 물결이 이 흐름을 탄다.")]
        [Range(0, 6)] public float flowSpeed = 1.2f;
        [Tooltip("2D 패턴 회전 속도(도/초).")]
        [Range(-180f, 180f)] public float spinSpeed = 25f;
        [Tooltip("진행 방향 바이어스. 0=방사형(원→바깥), 1=화면 상향. S자 곡선은 유지된다.")]
        [Range(0, 1)] public float upBias = 0f;
        [Tooltip("획별 S커브 진폭(라디안). 위상·방향은 획마다 랜덤. 셀 폭 초과분 자동 클램프.")]
        [Range(0, 1.2f)] public float sCurveAmount = 0.45f;
        [Tooltip("S커브 굴곡 수. 획 길이는 S 1주기를 넘지 않는다.")]
        [Range(0.2f, 6f)] public float sCurveFreq = 2.0f;

        [Header("디버그")]
        [Tooltip("디버그 원 표시(기준원=청백, 도달한계=녹색). 완성 시 0.")]
        [Range(0, 1)] public float debugCircles = 0.15f;
    }
}
