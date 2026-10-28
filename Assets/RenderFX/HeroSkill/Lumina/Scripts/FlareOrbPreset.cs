using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「플레어 봄」 차징 오브 F0(필드 방식) 프리셋.
    /// 공통 섹션(셸/혀 색/후면)은 FlareOrbPresetBase 참조.
    /// F0 전담: 코어 구체 + 코어 림 스프라이트 + 광원 + 필드 방식 오라 무늬(FlareAuraTongue).
    /// 겹침 구성(FlareBombOrbFull)에서 코어·림·광원은 F0이 담당한다.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Flare Orb Preset (F0 Field)", fileName = "F0_FlareOrbPreset")]
    public class FlareOrbPreset : FlareOrbPresetBase
    {
        [Header("코어 구체 — 색 (FlareOrbCore.mat)")]
        [Tooltip("코어 저역 색. 노이즈 어두운 곳의 바탕색.")]
        [ColorUsage(true, true)] public Color coreColor = new Color(1.45f, 1.28f, 0.95f);
        [Tooltip("중간 램프 색. 스월 무늬가 이 색으로 드러난다. 코어색과 대비가 커야 무늬가 보인다.")]
        [ColorUsage(true, true)] public Color coreMidColor = new Color(1.05f, 0.66f, 0.22f);
        [Tooltip("림 색. 코어 뒤 2D 빌보드 림 스프라이트(CoreRim)와 코어 노이즈 피크에 쓰인다.")]
        [ColorUsage(true, true)] public Color coreRimColor = new Color(1.8f, 1.55f, 1.0f);

        [Header("코어 구체 — 강도/형태")]
        [Tooltip("전체 발광 배수. 과하면 순백으로 포화되어 스월이 사라진다. 1.2~1.6 권장.")]
        [Range(0, 8)] public float coreEmission = 1.35f;
        [Tooltip("바탕 불투명도. 낮을수록 배경이 비친다.")]
        [Range(0, 1)] public float coreBaseAlpha = 0.85f;
        [Tooltip("(레거시) 코어 셰이더 프레넬 지수. 림이 CoreRim 스프라이트로 분리되어 현재 미사용에 가깝다.")]
        [Range(0.5f, 8)] public float coreRimPower = 2.6f;
        [Tooltip("림 강도. CoreRim 스프라이트의 발광 배수.")]
        [Range(0, 3)] public float coreRimStrength = 0.9f;
        [Tooltip("스월 노이즈 스케일. 클수록 무늬가 잘아진다.")]
        [Range(0.2f, 8)] public float coreNoiseScale = 2.6f;
        [Tooltip("노이즈 상승 스크롤 속도.")]
        [Range(0, 3)] public float coreNoiseSpeed = 0.5f;
        [Tooltip("스월(Y축 회전) 속도.")]
        [Range(0, 3)] public float coreSwirlSpeed = 0.4f;

        [Header("코어 림 스프라이트 (CoreRim — 2D 빌보드)")]
        [Tooltip("링 반경(uv). 코어 실루엣(≈0.57)에 맞추면 딱 붙는 림이 된다.")]
        [Range(0.1f, 1f)] public float rimRadius = 0.6f;
        [Tooltip("링 밴드 폭.")]
        [Range(0.01f, 0.6f)] public float rimWidth = 0.15f;
        [Tooltip("링 안쪽 헤일로 강도.")]
        [Range(0, 1)] public float rimHalo = 0.35f;

        [Header("필드 오라 — 무늬 (FlareAuraTongue.mat)")]
        [Tooltip("무늬 스케일. 클수록 획이 잘게 쪼개진다.")]
        [Range(0.5f, 8f)] public float patternScale = 3.2f;
        [Tooltip("세로 스트레치. 낮을수록 획이 세로로 길어진다.")]
        [Range(0.1f, 1f)] public float patternVStretch = 0.25f;
        [Tooltip("디테일 옥타브 가중. 0=아주 청키, 1=자글자글.")]
        [Range(0, 1)] public float patternDetail = 0.45f;
        [Tooltip("샤프 믹스. 0=둥근 블롭, 1=봉우리 능선 기반의 끝 뾰족한 획(고리 없음).")]
        [Range(0, 1)] public float ridgeMix = 0.8f;
        [Tooltip("획 끝 테이퍼 샤프니스. 클수록 단면이 좁아져 획이 얇고 끝이 날카롭다.")]
        [Range(1f, 8f)] public float taperSharp = 2.6f;
        [Tooltip("획 분절 스케일. 클수록 짧은 토막으로 끊어진다.")]
        [Range(0.3f, 6f)] public float breakScale = 1.6f;
        [Tooltip("획 분절 강도. 0=등고선망 그대로 연결, 1=개별 획으로 완전 분리.")]
        [Range(0, 1)] public float breakAmount = 0.8f;
        [Tooltip("커버리지 임계값. 높을수록 획이 성기다.")]
        [Range(0, 1)] public float patternThreshold = 0.30f;
        [Tooltip("에지 소프트 비율. 0=하드컷(카툰), 1=소프트 발광 에지.")]
        [Range(0, 1)] public float edgeSoftRatio = 0.25f;
        [Tooltip("획 내부 하이라이트 오프셋. 클수록 하이라이트가 획 중심으로 좁아진다.")]
        [Range(0, 0.4f)] public float highlightShift = 0.12f;

        [Header("필드 오라 — 운동")]
        [Tooltip("상승 속도.")]
        [Range(0, 6)] public float flowSpeed = 1.2f;
        [Tooltip("일렁임(licking) 폭.")]
        [Range(0, 0.6f)] public float waver = 0.05f;
        [Tooltip("일렁임 속도.")]
        [Range(0, 6)] public float waverSpeed = 1.4f;
        [Tooltip("나선 시어. 0=순수 상승(기본), +면 상승하며 감아 돈다.")]
        [Range(-2f, 2f)] public float spiralShear = 0f;
        [Tooltip("S커브 진폭(라디안). 획의 진행 경로가 좌우로 굽는 정도. 0=직선 상승.")]
        [Range(0, 1.2f)] public float sCurveAmount = 0.35f;
        [Tooltip("S커브 굴곡 수. 높이 전체에 걸친 S자 꺾임 횟수.")]
        [Range(0.2f, 4f)] public float sCurveFreq = 1.2f;
        [Tooltip("S커브 흐름 추종 비율. 1=굽이가 전체 흐름(flowSpeed)과 함께 이동, 0=굽이 고정.")]
        [Range(0, 2f)] public float sCurveFollow = 1.0f;
        [Tooltip("S커브 상단 감쇠. 중간 지점 위에서 진폭이 이 비율로 줄어든다(하단 급격·상단 완만).")]
        [Range(0, 1)] public float sCurveUpperRatio = 0.4f;

        [Header("필드 오라 — 팁 파편")]
        [Tooltip("팁 침식 시작 높이(정규화). 이 위에서 획이 끊어지기 시작.")]
        [Range(0, 1)] public float tipErodeStart = 0.68f;
        [Tooltip("팁 침식 강도.")]
        [Range(0, 1.5f)] public float tipErodeStrength = 0.7f;
        [Tooltip("방울 파편 양(획 경계의 점들).")]
        [Range(0, 1)] public float speckAmount = 0.5f;
        [Tooltip("방울 파편 크기 스케일. 클수록 점이 잘아진다.")]
        [Range(2f, 24f)] public float speckScale = 10f;

        [Header("광원 (FireLight)")]
        [Tooltip("광원 색.")]
        public Color lightColor = new Color(1f, 0.85f, 0.6f);
        [Tooltip("기준 밝기(FireLightFlicker.baseIntensity).")]
        public float lightIntensity = 4f;
        [Tooltip("깜빡임 폭(FireLightFlicker.intensityAmplitude).")]
        public float lightFlickerAmplitude = 1.0f;
        [Tooltip("광원 범위(m).")]
        public float lightRange = 6f;
    }
}
