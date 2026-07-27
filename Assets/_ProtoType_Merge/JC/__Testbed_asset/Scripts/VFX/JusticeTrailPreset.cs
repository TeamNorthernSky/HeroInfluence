using System;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 저스티스 「등장!」 주먹 연출 프리셋.
    ///
    /// 세 요소가 완전히 분리되어 있고, **각 요소가 자기에게 의미 있는 항목만** 가진다.
    ///   1) 궤적(PenStrokes) — Local 시뮬레이션. 주먹을 따라 이동하며 입자마다 선을 끈다.
    ///      색은 팔레트 + 하이라이트(수명 앞단의 색 전환)로 만든다.
    ///   2) 입자(SparkDots) — World 시뮬레이션. 진행 반대 방향으로 원뿔 분사되는 점.
    ///      색은 전용 셰이더가 「몸통 + 가운데 코어」 구조로 그린다.
    ///      ★코어가 하이라이트 역할을 대신하므로 하이라이트 항목을 갖지 않는다.
    ///   3) 타격(Impact) — 스파크 + 섬광.
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
            [Tooltip("중력 배수. 음수면 위로 떠오른다.")]
            [Range(-2f, 2f)] public float gravity = -0.05f;

            [Header("크기")]
            [Range(0.001f, 0.5f)] public float sizeMin = 0.020f;
            [Tooltip("최소값과 벌릴수록 편차가 커진다.")]
            [Range(0.001f, 0.5f)] public float sizeMax = 0.070f;

            [Header("불투명도")]
            [Tooltip("완전 불투명을 유지하는 수명 구간 비율. 이후 0까지 감쇠한다.")]
            [Range(0f, 0.95f)] public float alphaHold = 0.5f;
        }

        /// <summary>궤적 계열 — 입자마다 per-particle 트레일을 끈다. 색은 팔레트+하이라이트.</summary>
        [Serializable]
        public class TrailGroup : EmitGroupBase
        {
            [Header("분포")]
            [Tooltip("방출 구(sphere) 반경. 작을수록 주먹에 밀착한다.")]
            [Range(0.005f, 1.5f)] public float shapeRadius = 0.07f;

            [Header("하이라이트 (수명 앞단의 색 전환)")]
            [Tooltip("갓 태어난 구간에 얹는 색.")]
            [ColorUsage(true, true)] public Color highlightColor = Color.white;
            [Tooltip("하이라이트에서 본체 팔레트로 넘어가는 데 걸리는 수명 비율. 0이면 하이라이트 없음.")]
            [Range(0f, 0.5f)] public float highlightRatio = 0.1f;
            [Tooltip("하이라이트 발광 배수.")]
            [Range(0f, 8f)] public float highlightEmission = 2f;

            [Header("궤적")]
            [Tooltip("입자가 죽은 뒤 선이 남아 있는 시간(초).")]
            [Range(0.02f, 2f)] public float trailLifetime = 0.4f;
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

            [Header("스파크")]
            [Tooltip("스파크 재질 색. 중립(흰색)으로 두면 색은 팔레트가 전담한다.")]
            [ColorUsage(true, true)] public Color tint = Color.white;
            [Tooltip("스파크 재질 발광 배수.")]
            [Range(0f, 6f)] public float emission = 1.6f;
            [Tooltip("한 번에 터지는 스파크 수.")]
            [Range(0, 200)] public int burstCount = 70;
            [Range(0.02f, 2f)] public float sizeMin = 0.22f;
            [Range(0.02f, 2f)] public float sizeMax = 0.60f;
            [Range(0f, 40f)] public float speedMin = 6f;
            [Range(0f, 40f)] public float speedMax = 16f;
            [Range(0.02f, 2f)] public float lifeMin = 0.25f;
            [Range(0.02f, 2f)] public float lifeMax = 0.55f;
            [Range(-2f, 3f)] public float gravity = 0.5f;
            [Tooltip("늘어난 스파크 길이 배수(Stretch 렌더).")]
            [Range(0.5f, 12f)] public float lengthScale = 3.6f;

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

        /// <summary>궤적 계열의 동시 개수 상한. 소수의 굵은 선으로 읽히도록 좁게 제한한다.</summary>
        public const int TrailMaxParticlesLimit = 10;

        [Header("── 팔레트 (궤적·타격용. 입자는 셰이더가 색을 갖는다) ──")]
        [ColorUsage(true, true)] public Color strokeHeadColor = new Color(1f, 0.40f, 0.36f);
        [ColorUsage(true, true)] public Color strokeMidColor = new Color(1f, 0.10f, 0.11f);
        [ColorUsage(true, true)] public Color strokeTailColor = new Color(0.50f, 0.02f, 0.04f);

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

        private void OnValidate()
        {
            if (trail != null) trail.maxParticles = Mathf.Clamp(trail.maxParticles, 1, TrailMaxParticlesLimit);
            if (spark != null) spark.maxParticles = Mathf.Clamp(spark.maxParticles, 1, 600);
        }
    }
}
