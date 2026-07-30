using System;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 바람무늬 방사광(안개 토러스) 프리셋 — ★서브이펙트별 프리셋 분리 1호(260729).
    /// JusticeTrailPreset의 인스펙터 비대화를 멈추기 위해, 이후 신규 서브이펙트는
    /// 이렇게 전용 SO를 하나씩 갖는다(FlareOrb 표준: 전 필드 툴팁 + 대상 소유 + 적용).
    ///
    /// 표현 목표(갈리오 궁 장벽 테두리 참조): 날 선 벽면이 아니라 「빛나는 안개 고리」 —
    /// 바닥 발자국과 높이를 동시에 가진 토러스 볼륨, 구름형 fbm 얼룩의 저속 회전·표류,
    /// 고투명 가산 누적이 실루엣 부근에서 두께감을 만든다. 색은 흰↔청.
    /// </summary>
    [CreateAssetMenu(menuName = "JC VFX/Justice Wind Ring Preset (바람무늬 방사광)", fileName = "FX_JusticeWindRing")]
    public class JusticeWindRingPreset : ScriptableObject
    {
        /// <summary>이 프리셋이 다루는 자산. 경로 하드코딩 금지 — 프리셋이 직접 소유한다.</summary>
        [Serializable]
        public class TargetSet
        {
            [Tooltip("바람 고리 프리팹(JcWindRingEffect + 바인더).")]
            public GameObject windPrefab;
            [Tooltip("바람 고리 재질(Testbed/Justice/WindRing). 바닥·벽이 공유하고 렌더러별 차이는 MPB가 만든다.")]
            public Material windMaterial;
        }

        [Header("── 대상 자산 ──")]
        public TargetSet targets = new TargetSet();

        [Header("── 색 (노이즈가 A↔B를 넘나든다) ──")]
        [Tooltip("색 A — 안개의 밝은 부분(기본 흰).")]
        [ColorUsage(true, true)] public Color colorA = Color.white;
        [Tooltip("색 B — 안개의 깊은 부분(청).")]
        [ColorUsage(true, true)] public Color colorB = new Color(0.55f, 0.8f, 1f);
        [Tooltip("발광 배수.")]
        [Range(0f, 8f)] public float emission = 1.5f;
        [Tooltip("색 얼룩의 크기 배율. 작을수록 큰 덩어리로 천천히 넘나든다.")]
        [Range(0.2f, 4f)] public float colorNoiseTile = 1.2f;

        [Header("── 안개 노이즈 ──")]
        [Tooltip("알파 얼룩 밀도(각도 방향). 클수록 잘게 일렁인다.")]
        [Range(0.5f, 12f)] public float noiseScale = 4f;
        [Tooltip("알파 얼룩 밀도(교차 방향 — 바닥은 반경, 벽은 높이).")]
        [Range(0f, 8f)] public float crossScale = 2f;
        [Tooltip("알파 얼룩 강도. 0 = 균일한 안개, 1 = 얼룩이 완전히 뚫린다.")]
        [Range(0f, 1f)] public float noiseAmp = 0.7f;
        [Tooltip("소멸 가장자리(바닥 바깥·벽 상단)가 노이즈로 뜯기는 정도.")]
        [Range(0f, 1f)] public float tearAmount = 0.5f;
        [Tooltip("밴드 경계의 부드러움(정규 0~1). 클수록 어느 경계든 뭉게하게 풀린다.")]
        [Range(0.02f, 0.6f)] public float bandSoft = 0.18f;

        [Header("── 운동 ──")]
        [Tooltip("회전 속도(회전/초). 음수면 반대 방향.")]
        [Range(-1f, 1f)] public float spinSpeed = 0.12f;
        [Tooltip("내벽 회전 배속. 외벽과 다르게(음수 = 역방향) 두면 시차가 두께감을 만든다.")]
        [Range(-2f, 2f)] public float innerSpinMul = -0.6f;
        [Tooltip("노이즈 자체의 표류 속도 — 회전과 별개로 무늬가 흐른다.")]
        [Range(0f, 2f)] public float driftSpeed = 0.25f;

        [Header("── 형상 (m) ──")]
        [Tooltip("바닥 안개 도넛의 안쪽 반경.")]
        [Range(0.1f, 5f)] public float floorInner = 0.9f;
        [Tooltip("바닥 안개 도넛의 바깥 반경.")]
        [Range(0.2f, 6f)] public float floorOuter = 1.8f;
        [Tooltip("벽(외벽) 반경.")]
        [Range(0.2f, 6f)] public float wallRadius = 1.7f;
        [Tooltip("벽 두께 — 내벽은 외벽보다 이만큼 안쪽에 선다.")]
        [Range(0.02f, 1.5f)] public float wallThickness = 0.25f;
        [Tooltip("벽 높이. ※카메라 70° 내려보기에서 수직은 34%로 압축된다 — 화면 인상 기준으로 과장해서 잡는다.")]
        [Range(0.2f, 5f)] public float wallHeight = 1.2f;
        [Tooltip("바닥 도넛을 지면에서 살짝 띄우는 높이(z-파이트 방지).")]
        [Range(0f, 0.2f)] public float groundLift = 0.03f;

        [Header("── 투명도 ──")]
        [Tooltip("최대 불투명도. 고투명 가산이 겹치며 두께감을 만드는 구조라 낮게 유지한다.")]
        [Range(0f, 1f)] public float alphaMax = 0.35f;
        [Tooltip("바닥 도넛의 알파 배율(벽 대비).")]
        [Range(0f, 2f)] public float floorAlphaMul = 0.8f;

        [Header("── 수명 (초) ──")]
        [Tooltip("나타나는 시간.")]
        [Range(0.02f, 1f)] public float fadeIn = 0.12f;
        [Tooltip("완전히 뜬 채 유지되는 시간.")]
        [Range(0f, 3f)] public float hold = 0.45f;
        [Tooltip("사라지는 시간.")]
        [Range(0.05f, 2f)] public float fadeOut = 0.5f;

        [Header("── 배치 ──")]
        [Tooltip("스폰 지점(소켓) 기준 오프셋(m). y를 내려 바닥에 붙인다.")]
        public Vector3 spawnOffset = new Vector3(0f, -0.2f, 0f);

        // ★뷰 벡터 보정(260729) — 매 프레임 현재 카메라 기준으로 재계산되므로
        // 카메라가 이동·회전해도 자동 추종한다. 직하 카메라에서는 보정이 자연 감쇠(퇴화 가드).
        [Header("── 카메라 보정 (매 프레임 자동 추종) ──")]
        [Tooltip("링 평면을 카메라 쪽으로 기울이는 정도. 0 = 완전 바닥, 1 = 화면 평행.\n" +
                 "올릴수록 화면상 타원이 원에 가까워지고 먼 쪽 말림이 줄어든다(절충 0.2~0.4 권장).\n" +
                 "※미학적 선택이라 자동화하지 않는다.")]
        [Range(0f, 1f)] public float cameraTilt = 0.25f;
        [Tooltip("켜면: 링의 가까운/먼 끝을 화면에 투영해 「화면상 시각 중심」이 기하 중심과\n" +
                 "일치하도록 보정량을 매 프레임 자동 역산한다(카메라 피치·거리 무관). 아래 수동값은 무시.")]
        public bool autoCenter = true;
        [Tooltip("수동 중심 보정(m) — 자동이 꺼져 있을 때, 시선의 수평 방향으로 링을 민다.\n" +
                 "+ = 화면 위쪽(먼 쪽). 원근이 시각 중심을 위로 밀어내는 것을 상쇄하는 용도.")]
        [Range(-2f, 2f)] public float centerShift = 0f;

        private void OnValidate()
        {
            if (floorOuter < floorInner + 0.05f) floorOuter = floorInner + 0.05f;
            if (wallThickness > wallRadius - 0.05f) wallThickness = Mathf.Max(0.02f, wallRadius - 0.05f);
        }
    }
}
