using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「플레어 봄」 차징 오브 프리셋 공통 베이스.
    /// F0(필드 방식)·F1(레인 방식)이 공유하는 섹션: 셸 형태 / 혀 공통 색 / 후면 레이어.
    /// 파생: FlareOrbPreset(F0, +코어/림/광원/필드 무늬), FlareOrbAltPreset(F1, +레인 획).
    /// 필드명은 기존과 동일하게 유지해 직렬화 값을 보존한다.
    /// </summary>
    public abstract class FlareOrbPresetBase : ScriptableObject
    {
        // ★변종 대응(260730) — 예전에는 에디터가 자산 경로를 상수로 박아, 크림판 프리셋 하나만
        // 존재할 수 있었다(흑염 자산은 프리셋 스코프에서 아예 제외). 접미사를 프리셋이 들고 있게 해서
        // 같은 코드로 변종 프리셋을 만들 수 있게 한다. 빈 값이면 종전과 완전히 동일하게 동작한다.
        [Header("변종 (대상 자산 선택)")]
        [Tooltip("대상 자산 이름의 접미사. 비우면 기본(크림) 세트, \"_Dark\"면 흑염 세트를 대상으로 한다.\n" +
                 "적용·캡처·라이브 프리뷰의 대상이 이 값에 따라 갈린다.\n" +
                 "예) 비움 → FlareOrbCore.mat / \"_Dark\" → FlareOrbCore_Dark.mat")]
        public string variantSuffix = "";

        [Header("셸 형태 (FlareOrbShell — 통통 티어드롭 메시)")]
        [Tooltip("몸통(구) 반경(m). 코어 구체(0.4)를 살짝 감싸는 값.")]
        public float shellRadius = 0.46f;
        [Tooltip("팁 시작 높이(정규화 0~1). 이 높이부터 위 캡이 솟는다.")]
        [Range(0f, 0.95f)] public float shellTipStart = 0.65f;
        [Tooltip("팁 솟음 높이(m). 0이면 구체(타원체).")]
        public float shellTipHeight = 0.22f;
        [Tooltip("팁 곡률 지수. 클수록 옆이 오목하게 파이며 뾰족해진다.")]
        [Range(1f, 4f)] public float shellTipPower = 1.9f;
        [Tooltip("셸 전체 상하 오프셋(m). 메시 정점에 굽는다.")]
        public float shellYOffset = 0f;
        [Tooltip("몸통 세로 비율(타원화). 1=정원, >1=길쭉, <1=납작. 양 극점 간 거리 조절.")]
        [Range(0.4f, 2f)] public float shellBodyHeightRatio = 1f;
        [Tooltip("전면 셸 자전 속도(도/초). +가 Spiral Shear(+)와 같은 방향.")]
        public float shellSpinSpeed = 25f;

        [Header("혀 — 공통 색")]
        [Tooltip("혀/획 기본 색(진한 골드/주황).")]
        [ColorUsage(true, true)] public Color tongueColor = new Color(1.05f, 0.62f, 0.16f);
        [Tooltip("내부 하이라이트 색(크림).")]
        [ColorUsage(true, true)] public Color tongueHighlightColor = new Color(1.45f, 1.25f, 0.80f);
        [Tooltip("발광 배수.")]
        [Range(0, 6)] public float tongueEmission = 1.2f;

        [Header("후면 화염 레이어 (TeardropShellBack)")]
        [Tooltip("후면 발광 배수. 전면 대비 후면 화염의 밝기 비율.")]
        [Range(0, 1)] public float backEmissionMul = 0.55f;
        [Tooltip("후면 불투명도.")]
        [Range(0, 1)] public float backOpacity = 0.8f;
        [Tooltip("후면 셸 자전 속도(도/초). 전면과 반대 부호=역회전.")]
        public float backSpinSpeed = -25f;
    }
}
