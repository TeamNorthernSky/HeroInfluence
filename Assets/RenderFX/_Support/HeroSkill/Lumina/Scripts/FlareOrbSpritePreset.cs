using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「플레어 봄」 차징 오브 F2(2D 스프라이트 방식) 프리셋.
    /// 하단 발산점 E에서 경계 타원을 향해 그은 직선을 레인으로 삼아,
    /// 직선-경계 교점에서 획을 생성해 바깥으로 발산시킨다(FlareAuraSprite 셰이더).
    /// 임계높이 아래의 생성은 도메인 리매핑으로 원천 배제된다.
    /// 후면 레이어 없음, 큐 3002(코어 위). F1과는 FlareOrbLayerToggle로 택일.
    /// 녹색원(maxReach)은 강제 종료 경계일 뿐, 생명주기 로직과 독립이다.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Flare Orb Sprite Preset (F2 2D)", fileName = "F2_FlareOrbSpritePreset")]
    public class FlareOrbSpritePreset : ScriptableObject
    {
        // ★변종 대응(260730) — F0/F1과 같은 규칙. 이 SO는 FlareOrbPresetBase를 상속하지 않으므로
        // 같은 이름의 필드를 자체 보유한다. 빈 값이면 종전과 완전히 동일하게 동작한다.
        [Header("변종 (대상 자산 선택)")]
        [Tooltip("대상 자산 이름의 접미사. 비우면 기본(크림) 세트, \"_Dark\"면 흑염 세트를 대상으로 한다.\n" +
                 "적용·캡처·라이브 프리뷰의 대상이 이 값에 따라 갈린다.\n" +
                 "예) 비움 → FlareAuraSprite.mat / \"_Dark\" → FlareAuraSprite_Dark.mat")]
        public string variantSuffix = "";

        [Header("스프라이트 배치")]
        [Tooltip("빌보드 쿼드 월드 크기(m). 도달한계 지름보다 넉넉하게(경계 클리핑 방지 여백).")]
        public float worldSize = 1.8f;
        [Tooltip("경계(파란 타원)의 가로 반경(uv 0~1). 획 생성 경계 — 코어 실루엣에 맞춘다.")]
        [Range(0.05f, 0.9f)] public float baseRadius = 0.42f;
        [Tooltip("경계 세로 비율. 1=정원, 1보다 크면 세로로 길쭉한 타원.")]
        [Range(0.5f, 1.8f)] public float ellipseRatio = 1f;
        [Tooltip("도달 한계(녹색 원) 반경(uv). 획이 닿으면 폭이 강제로 줄며 소멸(생명주기와 독립).")]
        [Range(0.1f, 1f)] public float maxReach = 0.75f;
        [Tooltip("도달 한계 세로 비율. 1=정원, 1보다 크면 세로로 길쭉한 타원.")]
        [Range(0.5f, 1.8f)] public float reachRatio = 1f;
        [Tooltip("강제 종료 경계의 소멸 소프트니스.")]
        [Range(0.01f, 0.3f)] public float reachSoft = 0.08f;
        [Tooltip("패턴 전체 Y 오프셋(uv). 경계·발산점·임계선·도달한계가 쿼드 내에서 함께 상하 이동.")]
        [Range(-0.5f, 0.5f)] public float patternYOffset = 0f;

        [Header("발산점/임계높이")]
        [Tooltip("발산점 E 깊이. 1=경계 하단 접점, 2=접점에서 반지름만큼 아래. 접점에 붙을수록 부채가 넓게 퍼지고, 깊을수록 평행 발산에 가깝다.")]
        [Range(1f, 2f)] public float emitterDepth = 1.2f;
        [Tooltip("생성 임계높이(-1=하단 극점, 1=상단 극점). 교점이 이보다 낮아지는 수평에 가까운 레인은 생성 자체가 배제되고, 전체 레인 수는 유효 부채꼴에 전량 재배분된다.")]
        [Range(-1f, 0.85f)] public float cutHeight = -0.6f;

        [Header("혀 — 색")]
        [Tooltip("획 기본 색(진한 골드/주황).")]
        [ColorUsage(true, true)] public Color tongueColor = new Color(1.05f, 0.62f, 0.16f);
        [Tooltip("내부 하이라이트 색(크림).")]
        [ColorUsage(true, true)] public Color tongueHighlightColor = new Color(1.45f, 1.25f, 0.80f);
        [Tooltip("발광 배수.")]
        [Range(0, 6)] public float tongueEmission = 1.2f;

        [Header("섹터/폭")]
        [Tooltip("유효 부채꼴 내 레인(획) 개수. 임계높이를 올려도 이 수가 전량 유지된다.")]
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

        [Header("생성 대역(경계 상대)")]
        [Tooltip("생성 오프셋 최소(uv, 경계 교점 기준. 음수=경계 안쪽). 생성 범위는 여기서 이동거리만큼 안쪽으로 확장된다(유량 정상화).")]
        [Range(-0.5f, 0.5f)] public float birthOffsetMin = -0.06f;
        [Tooltip("생성 오프셋 최대(uv, 경계 교점 기준).")]
        [Range(-0.5f, 0.5f)] public float birthOffsetMax = 0.05f;
        [Tooltip("내측 생성 가중. 1=균등, 클수록 안쪽(오프셋 최소 근처)에서 더 자주 생성.")]
        [Range(1f, 4f)] public float birthInnerBias = 1.8f;
        [Tooltip("획별 길이 지터.")]
        [Range(0, 1)] public float laneLengthJitter = 0.35f;
        [Tooltip("획 최대 길이(uv 반경 단위).")]
        [Range(0.05f, 0.8f)] public float laneMaxLength = 0.22f;
        [Tooltip("외측 생성 축소. 경계 밖에서 태어난 획일수록 길이·두께가 줄어든다.")]
        [Range(0, 1)] public float outerShrink = 0.6f;
        [Tooltip("임계선 원거리 축소. 임계선에서 높이 멀리(위쪽에서) 태어난 획일수록 길이·두께가 줄어든다. 0=균일, 1=상단 극점 생성이 0으로 수렴.")]
        [Range(0, 1)] public float cutFarShrink = 0f;

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
        [Tooltip("획별 S커브 진폭(라디안). 위상·방향은 획마다 랜덤. 셀 폭 초과분 자동 클램프.")]
        [Range(0, 1.2f)] public float sCurveAmount = 0.45f;
        [Tooltip("S커브 굴곡 수. 획 길이는 S 1주기를 넘지 않는다.")]
        [Range(0.2f, 6f)] public float sCurveFreq = 2.0f;

        [Header("디버그")]
        [Tooltip("디버그 가이드 표시(경계 타원=청백, 도달한계=녹색, 임계선=주황, E=적). 완성 시 0.")]
        [Range(0, 1)] public float debugCircles = 0.15f;
    }
}
