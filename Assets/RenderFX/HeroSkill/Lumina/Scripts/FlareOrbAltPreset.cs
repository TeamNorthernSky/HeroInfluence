using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「플레어 봄」 차징 오브 F1(레인 방식) 프리셋.
    /// 공통 섹션(셸/혀 색/후면)은 FlareOrbPresetBase 참조.
    /// F1 전담: 레인 획(FlareAuraLane) — 코어/림/광원은 겹침 구성에서 F0 담당이라 없다.
    /// FlareOrbAlt 프리팹 = 셸 전/후면 2장뿐.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Flare Orb Alt Preset (F1 Lane)", fileName = "F1_FlareOrbAltPreset")]
    public class FlareOrbAltPreset : FlareOrbPresetBase
    {
        [Header("레인 획 — 레인/폭")]
        [Tooltip("둘레 레인(획) 개수.")]
        [Range(3, 24)] public int laneCount = 9;
        [Tooltip("획 절반 폭(레인 셀 기준 0~0.5).")]
        [Range(0.02f, 0.5f)] public float laneWidth = 0.22f;
        [Tooltip("레인별 폭 지터. 획마다 굵기가 달라진다.")]
        [Range(0, 1)] public float laneWidthJitter = 0.35f;
        [Tooltip("레인별 생성 위치 지터(셀 내 중심 오프셋). 균등 격자 간격을 흐트러뜨린다.")]
        [Range(0, 0.8f)] public float lanePosJitter = 0.35f;
        [Tooltip("레인별 기울기 지터. 위 방향은 유지한 채 획이 좌우 사선으로 랜덤하게 기운다.")]
        [Range(0, 2f)] public float laneTiltJitter = 0.5f;
        [Tooltip("폭 물결 스케일. 획 굵기가 길이 방향으로 출렁이는 주기.")]
        [Range(0.5f, 12f)] public float widthNoiseScale = 3.5f;
        [Tooltip("폭 물결 폭. 0=일정한 굵기.")]
        [Range(0, 1)] public float widthNoiseAmount = 0.4f;

        [Header("레인 획 — 존재 구간")]
        [Tooltip("획 시작 높이(정규화). 생성 범위는 여기서 이동거리만큼 아래로 확장된다(하단 유량 정상화).")]
        [Range(0, 1)] public float laneVStart = 0.08f;
        [Tooltip("획 종료 높이(정규화). 상단 극점 도달 전에 소멸한다.")]
        [Range(0, 1)] public float laneVEnd = 0.82f;
        [Tooltip("생성 높이 산포. 0=모든 획이 시작 높이에서 생성, 0.5=전체 범위 랜덤. 사이클마다 새로 뽑힌다.")]
        [Range(0, 0.5f)] public float laneVJitter = 0.25f;
        [Tooltip("하단 생성 가중. 1=균등, 클수록 아래쪽에서 더 자주 생성된다.")]
        [Range(1f, 4f)] public float birthBottomBias = 1.8f;
        [Tooltip("레인별 길이 지터. 획마다 길이가 최대 이 비율만큼 랜덤으로 짧아진다.")]
        [Range(0, 1)] public float laneLengthJitter = 0.35f;
        [Tooltip("획 최대 길이(v 단위). 0.25 = 극-극 아크의 1/4 = 대원 둘레의 1/8.")]
        [Range(0.08f, 1f)] public float laneMaxLength = 0.25f;
        [Tooltip("상부 생성 축소. 적도면(v=0.5) 위에서 태어난 획일수록 길이·두께가 이 강도로 줄어든다.")]
        [Range(0, 1)] public float laneUpperShrink = 0.6f;

        [Header("레인 획 — 생명주기")]
        [Tooltip("획 이동 거리(v). 태어난 위치에서 이만큼 위로 타고 오르며 소멸한다.")]
        [Range(0.02f, 0.6f)] public float laneTravelDist = 0.22f;
        [Tooltip("획 상승 속도(흐름 대비 비율). flowSpeed와 곱해져 실제 이동 속도가 된다. 0=제자리 생멸.")]
        [Range(0, 1)] public float laneTravelSpeed = 0.3f;
        [Tooltip("생명주기 페이드 피크(최대 알파 도달 시점). 0=시작부터 최대→끝에서 0, 0.5=중간 피크(양끝 0), 1=페이드 없음. 획 전체 균일 알파.")]
        [Range(0, 1)] public float laneLifeFadePeak = 0.4f;

        [Header("레인 획 — 실루엣")]
        [Tooltip("동공 프로파일 지수. 클수록 획이 전체적으로 날씬해지고 양끝이 예리해진다.")]
        [Range(0.5f, 6f)] public float taperSharp = 1.4f;
        [Tooltip("내부 하이라이트 폭 비율(획 폭 대비).")]
        [Range(0, 1)] public float highlightRatio = 0.45f;
        [Tooltip("에지 소프트 비율. 0=하드컷(카툰).")]
        [Range(0, 1)] public float edgeSoftRatio = 0.25f;

        [Header("레인 획 — 운동")]
        [Tooltip("상승 속도. 폭 물결·상승 이동이 이 흐름을 탄다.")]
        [Range(0, 6)] public float flowSpeed = 1.2f;
        [Tooltip("나선 시어(전역, 격자 굽힘 — 무제한 감기). 0=순수 상승.")]
        [Range(-2f, 2f)] public float spiralShear = 0f;
        [Tooltip("획별 S커브 진폭(라디안). 획마다 위상·방향이 랜덤인 S 궤도. 셀 폭 초과분은 자동 클램프.")]
        [Range(0, 1.2f)] public float sCurveAmount = 0.45f;
        [Tooltip("S커브 굴곡 수. 획 길이는 S 1주기(=1/이 값)를 넘지 않는다.")]
        [Range(0.2f, 4f)] public float sCurveFreq = 1.2f;
        [Tooltip("S커브 상단 감쇠(하단 급격·상단 완만).")]
        [Range(0, 1)] public float sCurveUpperRatio = 0.4f;

        [Header("레인 획 — 팁 소멸")]
        [Tooltip("팁 핀치 시작 높이(정규화).")]
        [Range(0, 1)] public float tipErodeStart = 0.68f;
        [Tooltip("팁 핀치 강도. 폭이 줄어들며 바늘끝으로 소멸.")]
        [Range(0, 1.5f)] public float tipErodeStrength = 0.9f;

        [Header("디버그")]
        [Tooltip("티어드롭 실루엣 아웃라인 표시 강도(전면 셸만). 튜닝용 — 완성 시 0.")]
        [Range(0, 1)] public float debugOutline = 0.15f;
    }
}
