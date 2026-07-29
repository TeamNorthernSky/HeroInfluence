using System;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 저스티스 「등장!」 주먹 연출 프리셋.
    ///
    /// 세 요소가 완전히 분리되어 있고, **각 요소가 자기에게 의미 있는 항목만** 가진다.
    /// ★색도 마찬가지로 요소마다 독립이다. 공용 팔레트는 폐기했다 —
    ///   하나를 만지면 무관한 요소까지 함께 바뀌어 따로 맞출 수 없었기 때문.
    ///   1) 궤적(PenStrokes) — Local 시뮬레이션. 주먹을 따라 이동하며 입자마다 선을 끈다.
    ///      색은 자기 팔레트(머리/중간/꼬리) + 하이라이트(수명 앞단의 색 전환)로 만든다.
    ///   2) 입자(SparkDots) — World 시뮬레이션. 진행 반대 방향으로 원뿔 분사되는 점.
    ///      색은 전용 셰이더가 「몸통 + 가운데 코어」 구조로 그린다.
    ///      ★코어가 하이라이트 역할을 대신하므로 하이라이트 항목을 갖지 않는다.
    ///   3) 타격(Impact) — 스파크 + 섬광. 스파크는 자기 팔레트, 섬광은 자기 몸통·코어 색을 갖는다.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Justice Trail Preset (저스티스 주먹 궤적)", fileName = "FX_JusticeTrailPreset")]
    public class JusticeTrailPreset : ScriptableObject
    {
        /// <summary>궤적·입자가 실제로 함께 쓰는 항목만 둔다.</summary>
        [Serializable]
        public abstract class EmitGroupBase
        {
            [Tooltip("이 계열을 사용할지. 끄면 방출이 0이 된다.")]
            public bool enabled = true;

            [Header("발광")]
            [Tooltip("본체 색에 곱해지는 발광 배수.")]
            [Range(0f, 8f)] public float emission = 1f;

            [Tooltip("발광을 올릴 때 채도를 지키는 정도.\n" +
                     "0 = 전 채널 균등 배수 → 밝아질수록 흰색으로 탈색.\n" +
                     "1 = 지배 채널 위주 → 밝아져도 색이 유지된다.")]
            [Range(0f, 1f)] public float chromaHold = 1f;

            [Header("방출")]
            [Tooltip("시간당 방출 수.")]
            [Range(0f, 200f)] public float rateOverTime = 30f;
            [Tooltip("이동 거리당 방출 수. 주먹이 빠를수록 촘촘해지고 멈추면 0이 된다.")]
            [Range(0f, 200f)] public float rateOverDistance = 28f;
            [Tooltip("동시 존재 상한. 실제 개수는 방출률 × 수명으로 정해지며 이 값은 뚜껑이다.")]
            [Min(1)] public int maxParticles = 250;

            [Header("수명 · 속도")]
            [Range(0.01f, 2f)] public float lifeMin = 0.05f;
            [Range(0.01f, 2f)] public float lifeMax = 0.22f;
            [Tooltip("초기 속도(방향은 각 계열의 분포가 정한다).")]
            [Range(0f, 6f)] public float speedMin = 0f;
            [Range(0f, 6f)] public float speedMax = 1.2f;

            [Header("크기")]
            [Range(0.001f, 0.5f)] public float sizeMin = 0.020f;
            [Tooltip("최소값과 벌릴수록 편차가 커진다.")]
            [Range(0.001f, 0.5f)] public float sizeMax = 0.070f;

            [Header("불투명도")]
            [Tooltip("완전 불투명을 유지하는 수명 구간 비율. 이후 0까지 감쇠한다.")]
            [Range(0f, 0.95f)] public float alphaHold = 0.5f;
        }

        /// <summary>궤적 계열 — 입자마다 per-particle 트레일을 끈다. 색은 자기 팔레트+하이라이트.</summary>
        [Serializable]
        public class TrailGroup : EmitGroupBase
        {
            [Header("색 (수명 진행: 머리 → 중간 → 꼬리)")]
            [Tooltip("갓 태어난 구간의 색.")]
            [ColorUsage(true, true)] public Color headColor = new Color(1f, 0.40f, 0.36f);
            [Tooltip("수명 중반의 색. 선의 인상을 좌우한다.")]
            [ColorUsage(true, true)] public Color midColor = new Color(1f, 0.10f, 0.11f);
            [Tooltip("사라지기 직전의 색.")]
            [ColorUsage(true, true)] public Color tailColor = new Color(0.50f, 0.02f, 0.04f);

            [Header("분포")]
            [Tooltip("방출 구(sphere) 반경. 작을수록 주먹에 밀착한다.")]
            [Range(0.005f, 1.5f)] public float shapeRadius = 0.07f;

            // ★하이라이트는 「수명 앞단의 색 전환」에서 「획 중심선 코어」로 개념 교체(260728).
            // 수명 기반은 획 전체가 잠깐 물들었다 돌아오는 방식이라 사실상 안 보였다.
            // 코어는 셰이더(Testbed/Justice/StrokeCore)가 폭 방향 단면에 그리므로
            // 모든 획의 중심선이 전 길이에 걸쳐 항상 빛난다. 본체 색과 완전 독립.
            [Header("코어 (획 중심선 하이라이트 — 셰이더 단면)")]
            [Tooltip("중심선 색.")]
            [ColorUsage(true, true)] public Color coreColor = Color.white;
            [Tooltip("코어 발광 배수. 0이면 코어 없음.")]
            [Range(0f, 8f)] public float coreEmission = 2f;
            [Tooltip("코어 폭(획 폭 대비 비율).")]
            [Range(0.01f, 1f)] public float coreWidth = 0.35f;
            [Tooltip("코어 경계 날카로움. 낮을수록 부드럽게 번진다.")]
            [Range(0.1f, 6f)] public float coreSharpness = 2f;

            // ★코어(하이라이트) 속성이 아니라 획 몸통의 형태 속성 — 헤더를 분리해 혼동을 막는다.
            [Header("획 형태 (몸통 단면 · 양끝 테이퍼)")]
            [Tooltip("몸통 단면 감쇠 지수. 클수록 중심의 얇은 심만 남아 샤프해진다.\n" +
                     "※체감 폭 = 설정 굵기 × 감쇠에 살아남는 비율 (7.5면 약 26%).")]
            [Range(0.5f, 8f)] public float bodyFalloff = 2.5f;
            [Tooltip("획 양끝(머리·꼬리) 테이퍼 구간(수명 비율). 폭이 0에서 자라나고 0으로 줄어든다.")]
            [Range(0f, 0.5f)] public float endFade = 0.15f;

            // ★트레일 배율(trails.lifetime)은 1로 고정한다(260728). Unity의 이 값은 초가 아니라
            // 입자 수명에 대한 배율이라 직관을 깨서, 잔상은 「수명 Min/Max」 하나로 다스린다.
            // 규칙: 선의 각 지점은 그어진 뒤 입자 수명만큼 남는다.
            [Header("궤적")]
            [Tooltip("선의 정점 간격(m). 작을수록 곡선이 매끄럽다.")]
            [Range(0.001f, 0.05f)] public float trailMinVertexDistance = 0.003f;
        }

        /// <summary>
        /// 입자 계열 — 궤적 없이 원뿔로 분사되는 점.
        /// 입자 하나가 「몸통 + 가운데 코어」 구조를 가지며 수명에 따라 코어 비율이 커진다.
        /// 이 구조는 전용 셰이더(Testbed/Justice/SparkDot)가 그린다.
        /// ★코어가 하이라이트 역할을 대신하므로 하이라이트 항목이 없다.
        /// </summary>
        [Serializable]
        public class SparkGroup : EmitGroupBase
        {
            [Header("입자 내부 구조")]
            [Tooltip("몸통 색. 코어 바깥을 채우는 부분.")]
            [ColorUsage(true, true)] public Color bodyColor = new Color(1f, 0.10f, 0.11f);
            [Tooltip("가운데 코어 색.")]
            [ColorUsage(true, true)] public Color coreColor = Color.white;
            [Tooltip("코어 발광 배수. 0이면 코어가 검정이 되어 가산 합성에서 구멍처럼 보인다.")]
            [Range(0f, 8f)] public float coreEmission = 2f;

            [Tooltip("태어날 때 코어가 차지하는 반경 비율.")]
            [Range(0f, 1f)] public float coreRatioStart = 0.18f;
            [Tooltip("사라질 때 코어가 차지하는 반경 비율. 클수록 말기에 속이 밝게 뜬다.")]
            [Range(0f, 1f)] public float coreRatioEnd = 0.95f;
            [Tooltip("코어 경계의 날카로움. 낮을수록 부드럽게 번진다.")]
            [Range(0.01f, 1f)] public float coreSharpness = 0.35f;
            [Tooltip("입자 바깥 경계의 부드러움.")]
            [Range(0.01f, 1f)] public float bodySoftness = 0.30f;

            [Header("크기 변화")]
            [Tooltip("수명 끝에서의 크기 배율. 1보다 작으면 점점 작아지다 사라진다.")]
            [Range(0f, 1f)] public float sizeEndScale = 0.15f;

            [Header("방사형 분사")]
            [Tooltip("분사 원뿔의 반각(도). 0이면 일직선, 90이면 반구 전체.")]
            [Range(0f, 90f)] public float spreadAngle = 22f;
            [Tooltip("분사구 반경(m). 클수록 시작점이 흩어진다.")]
            [Range(0f, 0.5f)] public float nozzleRadius = 0.04f;
            [Tooltip("뒤로 분출 세기. startSpeed에 (이 값 × 소켓속도)를 더한다. 방향은 원뿔이 정한다.")]
            [Range(0f, 6f)] public float backwardEject = 1.2f;
            [Tooltip("분출 후 감속. 클수록 빨리 멈춰 뒤에 머문다.")]
            [Range(0f, 8f)] public float drag = 0f;

            [Tooltip("중력 배수. 음수면 위로 떠오른다.\n" +
                     "★입자 계열에만 있다. 궤적·타격 스파크는 현실의 물체가 아닌 방사광이라 중력을 받지 않는 게 자연스럽다.\n" +
                     "입자는 바닥 마찰 불꽃처럼 실체 있는 연출로 변주할 여지가 있어 남겨 둔다.")]
            [Range(-2f, 2f)] public float gravity = -0.05f;

            [Header("방향 클램프")]
            [Tooltip("분사 축이 최초 기준 방향에서 벗어날 수 있는 최대 각도(도).\n" +
                     "주먹 회수 구간에서 속도가 뒤집혀 분사가 적 쪽을 향하는 것을 막는다. 0이면 완전 고정.")]
            [Range(0f, 180f)] public float alignMaxAngle = 60f;

            [Header("속도 연동 (소켓 위치 변화 기반 — 캐릭터와 결합 없음)")]
            [Tooltip("속도에 따라 방출을 조절할지. 타격 순간 감속하면 분출이 줄어든다.")]
            public bool scaleEmissionBySpeed = true;
            [Tooltip("이 속도(m/s)에서 방출 100%.")]
            [Min(0.01f)] public float speedForFullEmission = 6f;
            [Tooltip("완전히 멈췄을 때의 방출 배율.")]
            [Range(0f, 1f)] public float emissionAtRest = 0.15f;
        }

        /// <summary>타격 연출 — 스파크·하이라이트·섬광을 한 묶음으로 관리한다.</summary>
        [Serializable]
        public class ImpactGroup
        {
            [Tooltip("타격 이펙트 전체를 켜고 끈다. 끄면 스파크·섬광 모두 방출되지 않는다.")]
            public bool enabled = true;

            [Header("스파크 — 색 (수명 진행: 머리 → 중간 → 꼬리)")]
            [Tooltip("갓 터져 나온 구간의 색.")]
            [ColorUsage(true, true)] public Color headColor = new Color(1f, 0.40f, 0.36f);
            [Tooltip("수명 중반의 색. 폭발의 인상을 좌우한다.")]
            [ColorUsage(true, true)] public Color midColor = new Color(1f, 0.10f, 0.11f);
            [Tooltip("사라지기 직전의 색.")]
            [ColorUsage(true, true)] public Color tailColor = new Color(0.50f, 0.02f, 0.04f);

            [Header("스파크")]
            [Tooltip("스파크 재질 색. 중립(흰색)으로 두면 색은 위 팔레트가 전담한다.")]
            [ColorUsage(true, true)] public Color tint = Color.white;
            [Tooltip("스파크 재질 발광 배수.")]
            [Range(0f, 6f)] public float emission = 1.6f;
            [Tooltip("한 번에 터지는 스파크 수.")]
            [Range(0, 200)] public int burstCount = 70;
            [Range(0f, 40f)] public float speedMin = 6f;
            [Range(0f, 40f)] public float speedMax = 16f;
            [Range(0.02f, 2f)] public float lifeMin = 0.25f;
            [Range(0.02f, 2f)] public float lifeMax = 0.55f;

            // 스파크는 Stretch 렌더라 화면상 모습이 세 값의 조합으로 정해진다.
            //     굵기 = startSize
            //     길이 = startSize × lengthScale  +  속도 × velocityScale
            // 예전엔 startSize(Min/Max)와 lengthScale을 그대로 노출했는데,
            //   ① startSize를 만지면 굵기와 길이가 동시에 변해 "굵기만" 조절할 수 없었고
            //   ② velocityScale은 프리팹에 박혀 있어 조절 불가인데도 실측상 길이의 절반 이상을 만들고 있었다.
            // 그래서 화면에 보이는 치수(굵기·길이 m)를 그대로 받고, 위 세 값은 역산해서 넣는다.
            [Header("스파크 — 크기 (화면상 실제 치수)")]
            [Tooltip("스파크의 굵기(m). 가장 굵은 입자 기준.")]
            [Range(0.01f, 2f)] public float width = 0.60f;
            [Tooltip("굵기 편차. 0 = 전부 같은 굵기, 1 = 가장 가는 입자가 0까지 얇아진다.")]
            [Range(0f, 1f)] public float widthVariation = 0.63f;
            [Tooltip("스파크의 길이(m). 정지 상태 기준이며, 실제로는 아래 속도 반영분이 더해진다.")]
            [Range(0.01f, 8f)] public float length = 1.41f;
            [Tooltip("속도가 길이에 반영되는 정도. 빠른 스파크일수록 길게 늘어난다.\n" +
                     "속도 6~16m/s × 이 값 만큼이 길이에 더해진다(0.1이면 0.6~1.6m).")]
            [Range(0f, 0.5f)] public float velocityScale = 0.10f;

            [Header("하이라이트")]
            [Tooltip("스파크·섬광이 갓 태어난 구간에 얹는 색.")]
            [ColorUsage(true, true)] public Color highlightColor = Color.white;
            [Tooltip("하이라이트에서 본체 색으로 넘어가는 수명 비율. 0이면 하이라이트 없음.")]
            [Range(0f, 0.5f)] public float highlightRatio = 0.16f;
            [Tooltip("하이라이트 발광 배수.")]
            [Range(0f, 8f)] public float highlightEmission = 3f;

            [Header("섬광")]
            [Tooltip("타격 순간의 큰 단발 섬광을 사용할지.")]
            public bool flashEnabled = true;
            [Tooltip("섬광 몸통 색. 스파크와 독립적으로 지정한다.")]
            [ColorUsage(true, true)] public Color flashColor = new Color(1f, 0.10f, 0.11f);
            [Tooltip("섬광 가운데 하이라이트 색.")]
            [ColorUsage(true, true)] public Color flashCoreColor = Color.white;
            [Tooltip("섬광 하이라이트 발광 배수.")]
            [Range(0f, 8f)] public float flashCoreEmission = 2f;

            [Tooltip("섬광 길이(m). 타점에서 전방으로 뻗는 거리.")]
            [Range(0.05f, 15f)] public float flashLength = 4.7f;
            [Tooltip("섬광 최대 넓이(m). 실제 폭은 아래 프로파일이 정한다.")]
            [Range(0.01f, 6f)] public float flashWidth = 1.2f;

            [Tooltip("타점 쪽 폭 비율. 끝 쪽보다 크면 테이퍼, 작으면 확대(부채꼴)가 된다.")]
            [Range(0f, 1f)] public float flashStartWidth = 1f;
            [Tooltip("끝 쪽 폭 비율. 0이면 한 점으로 수렴한다.")]
            [Range(0f, 1f)] public float flashEndWidth = 0f;
            [Tooltip("폭 변화 곡선. 1=선형, 클수록 끝에서 급변, 작을수록 초반에 급변.")]
            [Range(0.1f, 6f)] public float flashWidthCurve = 1f;

            [Tooltip("하이라이트 영역 넓이(그 지점 폭 대비 비율). 0이면 하이라이트 없음.")]
            [Range(0f, 1f)] public float flashCoreWidth = 0.25f;
            [Tooltip("몸통 가장자리의 부드러움.")]
            [Range(0.1f, 6f)] public float flashSoftness = 1.2f;
            [Tooltip("하이라이트 경계의 날카로움.")]
            [Range(0.1f, 6f)] public float flashCoreSharpness = 1.6f;

            [Tooltip("타점 쪽 페이드 구간. 0이면 시작부터 꽉 찬다.")]
            [Range(0f, 1f)] public float flashHeadFade = 0f;
            [Tooltip("끝 쪽 페이드 구간. 끝이 뚝 잘리지 않게 한다.")]
            [Range(0f, 1f)] public float flashTailFade = 0.35f;

            [Tooltip("나타남·사라짐을 위/아래 경계부터 잠식시키는 정도.\n" +
                     "0 = 전체가 균일하게 옅어짐(기존).\n" +
                     "1 = 폭이 안쪽으로 수축하며 중심선만 남다가 사라짐.")]
            [Range(0f, 1f)] public float flashEdgeFade = 1f;

            [Tooltip("나타나는 데 쓰는 수명 비율. 0이면 즉시 최대, 키우면 경계가 바깥으로 자라며 등장한다.")]
            [Range(0f, 0.9f)] public float flashFadeIn = 0.15f;

            [Tooltip("섬광 수명(초).")]
            [Range(0.02f, 1f)] public float flashLifetime = 0.16f;
            [Tooltip("섬광 시작 알파. 낮출수록 은은해진다.")]
            [Range(0f, 1f)] public float flashAlpha = 0.85f;

            [Tooltip("섬광 위치 오프셋(m). 스폰 지점(소켓) 기준의 **빔 로컬 축**.\n" +
                     "z = 타깃 방향(전방) / x = 우측 / y = 월드 상방.\n" +
                     "캐릭터 뒤쪽으로 밀면 캐릭터가 앞부분을 가려 관통 연출처럼 보인다.")]
            public Vector3 flashOffset = Vector3.zero;

            [Tooltip("섬광의 화면상 회전 부호를 뒤집는다. 방향이 반대로 누우면 켠다.")]
            public bool flashFlipRotation;

            [Tooltip("투명 오브젝트 사이의 그리기 순서 보정. 음수면 다른 이펙트보다 뒤에 그려진다.\n" +
                     "※ 불투명한 캐릭터와의 앞뒤는 깊이 판정이 정하므로 이 값이 아니라 오프셋으로 조절한다.")]
            [Range(-50f, 50f)] public float flashSortingFudge = 0f;
        }

        /// <summary>
        /// 호 획 V2 — 재설계(260729). 앵커가 호 궤도를 등속 주행하고, 앵커를 따라 뿌려진
        /// 입자들이 획을 긋는 최소 구성. 구버전(SlashGroup)은 레거시로 보존.
        ///
        /// ★획 하나의 일생 규칙 (사용자 확정):
        ///   두께: 무조건 0에서 시작 → 증가 시간에 걸쳐 최대 두께 → 소멸 구간에서 다시 0.
        ///        Width 값은 「최대 두께」를 뜻한다.
        ///   소멸: 뒤에서부터 지워지지 않는다(혜성 금지). 그어진 전체가 유지되다가
        ///        소멸 구간에서 알파 페이드(+두께 수렴)로 한 번에 사라진다.
        ///   파편 없음 — 본 획 로직이 검증된 뒤 별도 재설계.
        /// </summary>
        [Serializable]
        public class ArcStrokeGroup
        {
            [Tooltip("호 획을 사용할지.")]
            public bool enabled = true;

            [Header("색 (획 수명 진행: 머리 → 중간 → 꼬리)")]
            [ColorUsage(true, true)] public Color headColor = new Color(0.92f, 0.97f, 1f);
            [ColorUsage(true, true)] public Color midColor = new Color(0.70f, 0.85f, 1f);
            [ColorUsage(true, true)] public Color tailColor = new Color(0.32f, 0.48f, 0.72f);
            [Tooltip("본체 발광 배수.")]
            [Range(0f, 8f)] public float emission = 1.2f;
            [Tooltip("발광을 올릴 때 채도를 지키는 정도. 0이면 밝아질수록 흰색으로 탈색된다.")]
            [Range(0f, 1f)] public float chromaHold = 1f;

            // ★코어(획 중심선) 기능은 V2에서 제거(사용자 확정, 260729). 몸통 단면만 그린다.
            [Header("획 형태 (몸통 단면)")]
            [Tooltip("몸통 단면 감쇠 지수. 클수록 중심의 얇은 심만 남아 샤프해진다.\n" +
                     "※체감 폭 = 최대 두께 × 감쇠에 살아남는 비율.")]
            [Range(0.5f, 8f)] public float bodyFalloff = 2.5f;
            [Tooltip("획 양끝(길이 방향)의 테이퍼 구간(획 길이 비율). 방추형 — 시작·끝이 가늘고 중간이 굵다.\n" +
                     "0이면 전 구간 균일 폭(시작점도 함께 굵어짐).")]
            [Range(0f, 0.5f)] public float edgeTaper = 0.25f;

            [Header("궤도 (xz 평면 · 시계 각도: 타깃 방향=12시)")]
            [Tooltip("호 시작각(도). 210 = 7시.")]
            [Range(-360f, 720f)] public float angleStart = 210f;
            [Tooltip("호 끝각(도). 끝 < 시작 = 반시계, 끝 > 시작 = 시계, 차이 360 초과 = 한 바퀴 이상.")]
            [Range(-360f, 720f)] public float angleEnd = 30f;
            [Tooltip("궤도 반경(m).")]
            [Range(0.2f, 6f)] public float radius = 1.6f;
            [Tooltip("호를 다 긋는 데 걸리는 시간(초). 등속 주행.")]
            [Range(0.05f, 3f)] public float sweepDuration = 0.5f;

            [Header("획 방출")]
            [Tooltip("주행 거리당 방출 수. 획의 개수 밀도.")]
            [Range(0f, 60f)] public float rateOverDistance = 6f;
            [Tooltip("생성 밀도의 점진 구간(스윕 전체=1.0 기준).\n" +
                     "0 = 처음부터 풀 밀도.  0.25 = 스윕 앞 25% 동안 밀도가 0 → 100%로 선형 증가.\n" +
                     "호의 시작은 성기고 진행할수록 빽빽해진다.")]
            [Range(0f, 0.5f)] public float emissionRamp = 0f;
            [Tooltip("생성 밀도의 감소 구간(스윕 전체=1.0 기준).\n" +
                     "0 = 끝까지 풀 밀도 유지(마지막에 한 번에 종료).  0.5 = 중간부터 밀도가 100% → 0으로 선형 감소.\n" +
                     "호의 끝으로 갈수록 획이 성겨진다.")]
            [Range(0f, 0.5f)] public float emissionDecay = 0f;

            // ★강제 수축 — 살아 있는 획의 「머리」를 강제로 끝낸다: 성장을 멈추고 현재 두께에서
            // 이어서 가늘어져 0으로(붓을 떼는 동작). 수명·최대 두께 목표는 전부 무시된다.
            // 이미 그어진 몸통은 보존된다. 궤도 중심에서 먼 획일수록 빨리 0에 닿고,
            // 가장 가까운 획도 스윕 종료에는 0에 도달한다.
            [Header("강제 수축 (획 머리 성장 정지 → 가늘어져 0)")]
            [Tooltip("수축 시작 구간(스윕 전체=1.0 기준). Emission Decay와 같은 문법.\n" +
                     "0 = 수축 없음.  0.5 = 스윕 중간부터 수축 시작 → 종료 시점에 전원 마무리.")]
            [Range(0f, 0.5f)] public float thinDecay = 0f;
            [Tooltip("먼 곳(반경 바깥쪽) 가속 배수. 1 = 차등 없음, 클수록 바깥 획이 먼저 마무리된다.")]
            [Range(1f, 5f)] public float thinFarBoost = 2f;
            [Tooltip("동시 존재 상한.")]
            [Min(1)] public int maxParticles = 60;
            // ★노즐은 구가 아니라 「공전하는 직사각형」(260729). 구형은 반경 편차가 중앙에 몰려
            // 강제 수축의 거리 차등이 죽는다. 상자는 반경 대역을 균일하게 채운다.
            // 상자의 반경 축은 컴포넌트가 주행 각도에 맞춰 매 서브스텝 회전시킨다(신규 입자에만 적용).
            [Tooltip("노즐 반경 방향 반폭(m). 획들이 안쪽~바깥쪽으로 고르게 벌어지는 범위.")]
            [Range(0f, 2f)] public float nozzleRadius = 0.2f;
            [Tooltip("노즐 두께(m) — 진행 방향·수직 방향의 폭. 얇을수록 획들이 한 평면 대역에 정렬된다.")]
            [Range(0.005f, 0.5f)] public float nozzleThickness = 0.05f;

            [Header("획 일생 (두께 0 → 최대 → 0)")]
            // ★잔여 시간 클램프 규칙(사용자 확정, 260729): Min/Max와 무관하게, 획은 자신이
            // 태어난 시점에 스윕 종료까지 남은 시간을 넘겨 살 수 없다. 늦게 태어난 획일수록
            // 일생이 짧아지고(전체 생명주기가 압축), 모든 획이 스윕 종료와 함께 소멸한다.
            [Tooltip("획 일생 최소(초). Max와 벌릴수록 획마다 수명이 제각각이다.")]
            [Range(0.05f, 4f)] public float strokeLifeMin = 0.6f;
            [Tooltip("획 일생 최대(초). ※실제 일생은 「태어난 시점의 잔여 스윕 시간」을 넘지 못한다.")]
            [Range(0.1f, 4f)] public float strokeLifetime = 1.2f;
            [Tooltip("켜면: 잔여 스윕 시간이 자기 수명보다 짧아질 획은 **아예 그리지 않는다**(수명 압축 없음).\n" +
                     "잔여가 Max 아래로 내려가면 긴 수명 획부터 빠져 밀도가 자연 감소하고, Min 아래면 방출이 멈춘다.\n" +
                     "끄면: 잔여 시간으로 수명을 조인다(생명주기 압축 — 모든 획이 끝까지 태어남).")]
            public bool skipShortRemainder = true;
            [Tooltip("★두께 증가 시간(초). 0에서 최대 두께에 닿기까지 — 작을수록 빠르게 굵어진다.")]
            [Range(0.01f, 2f)] public float growTime = 0.15f;
            [Tooltip("소멸 구간(초). 이 시간 동안 알파가 빠지며 두께도 0으로 수렴한다.")]
            [Range(0.01f, 2f)] public float fadeTime = 0.3f;
            [Tooltip("최대 두께 최소(m).")]
            [Range(0.005f, 2.5f)] public float widthMin = 0.15f;
            [Tooltip("최대 두께 최대(m). Min과 벌릴수록 굵기가 제각각인 획이 된다.")]
            [Range(0.005f, 2.5f)] public float widthMax = 0.45f;

            [Header("궤적")]
            [Tooltip("획의 정점 간격(m). 작을수록 곡선이 매끄럽다.")]
            [Range(0.001f, 0.05f)] public float trailMinVertexDistance = 0.004f;

            [Header("배치")]
            [Tooltip("스폰 지점(소켓) 기준 오프셋(m). 소켓 스케일은 무시된다.")]
            public Vector3 spawnOffset = new Vector3(0f, -0.2f, 0f);
        }

        /// <summary>
        /// 호 참격 — 가상 앵커가 캐릭터 주위 호 궤도를 주행하고, 앵커에 실린 입자들이
        /// per-particle 트레일로 획을 긋는다(발 궤적과 같은 원리).
        /// 7시 → 1시 반시계 초승달을 거친 펜선으로 휘갈긴 참격이 촤악 지나간다.
        ///
        /// ★캐릭터 모션과 완전히 무관하다. 치비 체형의 짧은 다리에 이펙트를 의존하면
        /// 위험하다는 판단으로 가상 궤도로 확정(260728).
        /// ★처음엔 셰이더가 쿼드에 호를 그리는 방식이었으나, 스윕 경계가 매 프레임
        /// 이동하며 픽셀이 켜졌다 꺼지는 깜빡임이 구조적으로 발생해 앵커+트레일로 전환.
        /// 트레일은 정점이 누적되는 방식이라 경계 자체가 없다. 획의 이어짐/끊어짐은
        /// 입자 수명 Min/Max가 만든다 — 긴 수명 = 관통하는 획, 짧은 수명 = 끊긴 획.
        ///
        /// 궤도 평면: xz(바닥과 평행). 시계 각도는 캐릭터→타깃 방향이 12시.
        /// </summary>
        [Serializable]
        public class SlashGroup
        {
            [Tooltip("호 참격을 사용할지.")]
            public bool enabled = true;

            [Header("색 (수명 진행: 머리 → 중간 → 꼬리)")]
            [ColorUsage(true, true)] public Color headColor = new Color(0.92f, 0.97f, 1f);
            [ColorUsage(true, true)] public Color midColor = new Color(0.70f, 0.85f, 1f);
            [ColorUsage(true, true)] public Color tailColor = new Color(0.32f, 0.48f, 0.72f);
            [Tooltip("본체 발광 배수.")]
            [Range(0f, 8f)] public float emission = 2f;
            [Tooltip("발광을 올릴 때 채도를 지키는 정도. 0이면 밝아질수록 흰색으로 탈색된다.")]
            [Range(0f, 1f)] public float chromaHold = 1f;

            [Header("코어 (획 중심선 하이라이트 — 셰이더 단면)")]
            [Tooltip("중심선 색. 본체 색과 완전 독립.")]
            [ColorUsage(true, true)] public Color coreColor = Color.white;
            [Tooltip("코어 발광 배수. 0이면 코어 없음.")]
            [Range(0f, 8f)] public float coreEmission = 2f;
            [Tooltip("코어 폭(획 폭 대비 비율).")]
            [Range(0.01f, 1f)] public float coreWidth = 0.35f;
            [Tooltip("코어 경계 날카로움. 낮을수록 부드럽게 번진다.")]
            [Range(0.1f, 6f)] public float coreSharpness = 2f;

            // ★코어(하이라이트) 속성이 아니라 획 몸통의 형태 속성 — 헤더를 분리해 혼동을 막는다.
            [Header("획 형태 (몸통 단면 · 양끝 테이퍼)")]
            [Tooltip("몸통 단면 감쇠 지수. 클수록 중심의 얇은 심만 남아 샤프해진다.\n" +
                     "※체감 폭 = 설정 굵기 × 감쇠에 살아남는 비율 (7.5면 약 26%).")]
            [Range(0.5f, 8f)] public float bodyFalloff = 2.5f;
            [Tooltip("획 양끝(머리·꼬리) 테이퍼 구간(수명 비율). 폭이 0에서 자라나고 0으로 줄어든다.")]
            [Range(0f, 0.5f)] public float endFade = 0.15f;

            [Header("궤도 (xz 평면 · 시계 각도: 타깃 방향=12시)")]
            [Tooltip("호 시작각(도). 210 = 7시 방향.")]
            [Range(-360f, 720f)] public float angleStart = 210f;
            [Tooltip("호 끝각(도). 시작각에서 이 값까지 **숫자 그대로** 보간한다.\n" +
                     "끝 < 시작 = 반시계, 끝 > 시작 = 시계. 차이가 360을 넘으면 한 바퀴 이상 돈다.\n" +
                     "예) 210→30: 반시계 180°  /  210→-150: 반시계 한 바퀴  /  210→570: 시계 한 바퀴.")]
            [Range(-360f, 720f)] public float angleEnd = 30f;
            [Tooltip("궤도 반경(m). 캐릭터 중심에서 호까지의 거리.")]
            [Range(0.2f, 6f)] public float radius = 1.6f;
            [Tooltip("앵커 주위 방출 반경(m). 클수록 획들이 넓게 흩어져 겹친다.")]
            [Range(0f, 1f)] public float nozzleRadius = 0.25f;

            [Header("주행 (촤악 지나가는 타이밍)")]
            [Tooltip("호를 다 긋는 데 걸리는 시간(초).")]
            [Range(0.05f, 1.5f)] public float sweepDuration = 0.18f;
            [Tooltip("주행 가속 곡선. 1=등속, 클수록 초반이 빠르고 끝에서 감속(촤악).")]
            [Range(0.3f, 4f)] public float easeOut = 2f;

            [Header("획 (이어짐 · 끊어짐)")]
            [Tooltip("주행 거리당 방출 수. 획의 시작점 밀도.")]
            [Range(0f, 60f)] public float rateOverDistance = 10f;
            [Tooltip("동시 존재 상한.")]
            [Min(1)] public int maxParticles = 80;
            [Tooltip("★짧은 수명 = 중간에 끊기는 획.")]
            [Range(0.01f, 1.5f)] public float lifeMin = 0.06f;
            [Tooltip("★긴 수명 = 호를 관통하는 획. Min과 벌릴수록 긴 획·짧은 획이 섞인다.")]
            [Range(0.01f, 1.5f)] public float lifeMax = 0.30f;
            [Tooltip("획 굵기 최소(m).")]
            [Range(0.005f, 2.5f)] public float sizeMin = 0.04f;
            [Tooltip("획 굵기 최대(m). Min과 벌릴수록 굵기가 제각각인 붓자국이 된다.")]
            [Range(0.005f, 2.5f)] public float sizeMax = 0.12f;
            [Tooltip("완전 불투명을 유지하는 수명 구간 비율. 이후 0까지 감쇠한다.")]
            [Range(0f, 0.95f)] public float alphaHold = 0.4f;

            // ★트레일 배율은 1 고정 — 잔상은 「수명 Min/Max」로 다스린다(궤적 계열과 동일 규칙).
            [Header("궤적")]
            [Tooltip("획의 정점 간격(m). 작을수록 곡선이 매끄럽다.")]
            [Range(0.001f, 0.05f)] public float trailMinVertexDistance = 0.004f;

            // 파편도 앵커에 실리므로 궤도는 본 획과 같다. 두 손잡이가 두 인상을 만든다:
            //   퍼짐 반경 ↑ + 이탈 속도 0  = 같은 궤도인데 거리가 불규칙하게 벌어진 호선들
            //   이탈 속도 ↑              = 궤도에서 갈라져 바깥으로 뻗어나가는 파편
            [Header("파편 (궤도에서 갈라지는 호선)")]
            [Tooltip("주행 거리당 파편 방출 수. 0이면 파편 없음.")]
            [Range(0f, 30f)] public float fragmentRate = 4f;
            [Tooltip("파편이 태어나는 퍼짐 반경(m). 궤도 주위 원판에 불규칙하게 흩어진다.")]
            [Range(0f, 2f)] public float fragmentSpread = 0.5f;
            [Tooltip("바깥으로 벌어지는 속도 최소(m/s). 0이면 궤도에 나란히 남는다.")]
            [Range(0f, 6f)] public float fragmentDriftMin = 0f;
            [Tooltip("바깥으로 벌어지는 속도 최대(m/s). Min과 벌릴수록 파편마다 이탈이 제각각이다.")]
            [Range(0f, 6f)] public float fragmentDriftMax = 1.5f;
            [Tooltip("파편 수명 최소(초). 잔상도 이만큼 남는다.")]
            [Range(0.01f, 1.5f)] public float fragmentLifeMin = 0.10f;
            [Tooltip("파편 수명 최대(초).")]
            [Range(0.01f, 1.5f)] public float fragmentLifeMax = 0.35f;
            [Tooltip("파편 굵기 최소(m). 본 획보다 가늘게 두면 부스러기로 읽힌다.")]
            [Range(0.005f, 2.5f)] public float fragmentSizeMin = 0.02f;
            [Tooltip("파편 굵기 최대(m).")]
            [Range(0.005f, 2.5f)] public float fragmentSizeMax = 0.06f;

            [Header("배치 · 정리")]
            [Tooltip("스폰 지점(소켓) 기준 오프셋(m). 소켓 스케일은 무시된다.")]
            public Vector3 spawnOffset = new Vector3(0f, -0.2f, 0f);
            [Tooltip("주행 종료 후 잔광이 사라질 때까지의 추가 여유(초).")]
            [Range(0f, 2f)] public float fadeOutExtraSeconds = 0.4f;
        }

        /// <summary>
        /// 용권풍 — 수직축 회오리가 타깃 쪽으로 전진한다(하오마루 열풍참·야스오 Q3 계열).
        ///
        /// 입자를 원형으로 뿌리고 수직축 주위로 공전시키면서 상승시킨다. 실제 그림은
        /// per-particle 트레일이 그리는 나선 줄기다(궤적 계열과 같은 원리).
        ///
        /// ★프리뷰 카메라가 70° 내려다보기라 수직 성분이 화면에서 34%로 압축된다.
        /// 높이를 키우는 것보다 반경을 키우는 쪽이 인상에 훨씬 크게 기여한다.
        /// 위에서 보면 나선은 동심원으로 읽히므로, 반경 프로파일이 곧 실루엣이다.
        /// </summary>
        [Serializable]
        public class VortexGroup
        {
            [Tooltip("용권풍을 사용할지. 끄면 방출되지 않는다(등장!·펀치는 꺼 둔다).")]
            public bool enabled;

            [Header("색 (수명 진행: 머리 → 중간 → 꼬리)")]
            [ColorUsage(true, true)] public Color headColor = new Color(0.92f, 0.97f, 1f);
            [ColorUsage(true, true)] public Color midColor = new Color(0.70f, 0.85f, 1f);
            [ColorUsage(true, true)] public Color tailColor = new Color(0.32f, 0.48f, 0.72f);
            [Tooltip("본체 색에 곱해지는 발광 배수.")]
            [Range(0f, 8f)] public float emission = 2f;
            [Tooltip("발광을 올릴 때 채도를 지키는 정도. 0이면 밝아질수록 흰색으로 탈색된다.")]
            [Range(0f, 1f)] public float chromaHold = 1f;

            [Header("하이라이트 (수명 앞단의 색 전환)")]
            [ColorUsage(true, true)] public Color highlightColor = Color.white;
            [Range(0f, 0.5f)] public float highlightRatio = 0.12f;
            [Range(0f, 8f)] public float highlightEmission = 3f;

            [Header("형상 — ★반경이 인상을 지배한다 (카메라 70° 내려다보기)")]
            [Tooltip("바닥 쪽 반경(m). 위에서 보면 이 값이 안쪽 원이 된다.")]
            [Range(0.05f, 5f)] public float radiusStart = 0.55f;
            [Tooltip("꼭대기 쪽 반경(m). 시작보다 크면 나팔처럼 벌어진다.")]
            [Range(0.05f, 5f)] public float radiusEnd = 1.6f;
            [Tooltip("회오리 높이(m). 참고로 유닛 키가 약 2.05m다.")]
            [Range(0.2f, 8f)] public float height = 2.6f;
            [Tooltip("반경이 벌어지는 곡선. 1=선형, 크면 위쪽에서 급격히 벌어진다.")]
            [Range(0.2f, 4f)] public float radiusCurve = 1.6f;

            [Header("회전")]
            [Tooltip("수직축 공전 속도. 클수록 빽빽하게 감긴다.")]
            [Range(0f, 30f)] public float orbitSpeed = 8f;
            [Tooltip("입자마다 공전 속도를 무작위로 흔드는 폭(비율). 층이 갈려 도넛처럼 읽힌다.")]
            [Range(0f, 1f)] public float orbitJitter = 0.25f;

            [Header("전진")]
            [Tooltip("타깃 쪽으로 나아가는 속도(m/s). 0이면 제자리에서 돈다.")]
            [Range(0f, 20f)] public float travelSpeed = 6f;
            [Tooltip("최대 전진 거리(m). 이 거리에 닿거나 수명이 끝나면 멈춘다.")]
            [Range(0f, 20f)] public float travelDistance = 5f;
            [Tooltip("전진 시작까지의 지연(초). 발이 최대 신전에 머무는 동안 제자리에서 감기게 한다.")]
            [Range(0f, 1f)] public float travelDelay = 0.12f;

            [Header("방출 · 수명")]
            [Range(0f, 400f)] public float rateOverTime = 120f;
            [Min(1)] public int maxParticles = 300;
            [Range(0.05f, 3f)] public float lifeMin = 0.45f;
            [Range(0.05f, 3f)] public float lifeMax = 0.85f;
            [Range(0.001f, 0.5f)] public float sizeMin = 0.03f;
            [Range(0.001f, 0.5f)] public float sizeMax = 0.08f;
            [Tooltip("완전 불투명을 유지하는 수명 구간 비율. 이후 0까지 감쇠한다.")]
            [Range(0f, 0.95f)] public float alphaHold = 0.45f;

            // ★트레일 배율은 1 고정 — 잔상은 「수명 Min/Max」로 다스린다(궤적 계열과 동일 규칙).
            [Header("궤적")]
            [Range(0.001f, 0.05f)] public float trailMinVertexDistance = 0.004f;

            [Header("수명 · 정리")]
            [Tooltip("회오리 전체 수명(초). 이후 방출을 멈추고 잔광이 사라지면 스스로 정리된다.")]
            [Range(0.1f, 5f)] public float duration = 0.9f;
            [Tooltip("방출을 멈춘 뒤 오브젝트를 지우기까지의 여유(초).")]
            [Range(0f, 2f)] public float fadeOutExtraSeconds = 0.5f;
            [Tooltip("스폰 지점(발 소켓) 기준 오프셋(m). y를 내려 바닥에 붙인다.")]
            public Vector3 spawnOffset = new Vector3(0f, -0.6f, 0f);
        }

        /// <summary>
        /// 이 프리셋이 다루는 자산들.
        ///
        /// 예전엔 에디터가 등장! 자산 경로를 상수로 박아 두었다. 프리셋이 하나일 땐 문제가 없었지만
        /// 펀치 프리셋에서 「적용/캡처」를 누르면 **등장! 프리팹·재질에 쓰이는** 버그가 있었다.
        /// 프리셋이 자기 대상을 들고 있게 해서 근본적으로 막는다.
        /// </summary>
        [Serializable]
        public class TargetSet
        {
            [Tooltip("궤적·입자가 올라탄 프리팹.")]
            public GameObject trailPrefab;
            [Tooltip("타격(스파크·섬광) 프리팹.")]
            public GameObject impactPrefab;

            [Tooltip("궤적 선 재질. 색은 그라데이션이 전담하므로 항상 중립(흰색)으로 유지된다.")]
            public Material strokeMaterial;
            [Tooltip("입자 재질. Testbed/Justice/SparkDot 셰이더.")]
            public Material sparkMaterial;
            [Tooltip("타격 스파크 재질.")]
            public Material impactSparkMaterial;
            [Tooltip("타격 섬광 재질. Testbed/Justice/ImpactFlash 셰이더.")]
            public Material flashMaterial;

            [Tooltip("용권풍 프리팹. 쓰지 않는 스킬은 비워 둔다.")]
            public GameObject vortexPrefab;
            [Tooltip("용권풍 나선 줄기 재질. 색은 그라데이션이 전담하므로 중립(흰색)으로 유지된다.")]
            public Material vortexMaterial;

            [Tooltip("[레거시] 구 호 참격 프리팹.")]
            public GameObject slashPrefab;
            [Tooltip("[레거시] 구 호 참격 재질.")]
            public Material slashMaterial;

            [Tooltip("호 획 V2 프리팹.")]
            public GameObject arcStrokePrefab;
            [Tooltip("호 획 V2 재질(Testbed/Justice/StrokeCore).")]
            public Material arcStrokeMaterial;
        }

        /// <summary>궤적 계열의 동시 개수 상한. 소수의 굵은 선으로 읽히도록 좁게 제한한다.</summary>
        public const int TrailMaxParticlesLimit = 10;

        [Header("── 대상 자산 (적용·캡처가 쓰는 곳) ──")]
        public TargetSet targets = new TargetSet();

        [Header("── 궤적 (PenStrokes · Local) ──")]
        [Tooltip("maxParticles가 1~10으로 제한된다.")]
        public TrailGroup trail = new TrailGroup();

        [Header("── 입자 (SparkDots · World) ──")]
        public SparkGroup spark = new SparkGroup();

        [Header("── 이펙트 객체 (궤적·입자가 함께 올라탄 오브젝트) ──")]
        [Tooltip("Stop Cue 이후 오브젝트 정리까지의 추가 여유(초). 궤적·입자 잔광이 잘리면 늘린다.\n" +
                 "※ 두 계열이 같은 오브젝트에 있으므로 한쪽 전용이 아니다.")]
        [Range(0f, 1.5f)] public float fadeOutExtraSeconds = 0.15f;
        [Tooltip("소켓 기준 오프셋(m). 소켓의 200배 스케일은 무시하고 회전만 적용된다.")]
        public Vector3 socketOffset = Vector3.zero;

        [Header("── 타격 (스파크 · 하이라이트 · 섬광) ──")]
        public ImpactGroup impact = new ImpactGroup();

        [Header("── 호 획 V2 (앵커 주행 · 재설계) ──")]
        public ArcStrokeGroup arcStroke = new ArcStrokeGroup();

        [Header("── [레거시] 호 참격 V1 ──")]
        public SlashGroup slash = new SlashGroup();

        [Header("── 용권풍 (보류 자산 — 재설계 예정) ──")]
        public VortexGroup vortex = new VortexGroup();

        private void OnValidate()
        {
            if (trail != null) trail.maxParticles = Mathf.Clamp(trail.maxParticles, 1, TrailMaxParticlesLimit);
            if (spark != null) spark.maxParticles = Mathf.Clamp(spark.maxParticles, 1, 600);
            if (vortex != null) vortex.maxParticles = Mathf.Clamp(vortex.maxParticles, 1, 1500);
        }
    }
}
