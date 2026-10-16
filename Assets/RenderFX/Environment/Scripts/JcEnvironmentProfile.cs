using UnityEngine;

namespace JC.Env
{
    /// <summary>
    /// 환경(시간대·날씨) 라이팅 프로파일 — 전역 라이팅 값의 정본.
    ///
    /// ★설계 원칙(260811): 라이팅 값을 씬에 하드코딩하지 않고 이 SO 가 소유한다.
    /// 지금은 맑은 낮(ENV_Day_Clear) 하나지만, 밤·노을·날씨는 프로파일 자산 추가만으로 열린다
    /// (기획: 시간 경과 낮/밤 표현 — 1턴=1일이라 턴 이벤트 연동 예정).
    ///
    /// ★260902 구조 개편: <b>하늘 파라미터까지 프로파일이 소유</b>하고 재질은 출력물로 격하했다.
    ///   · 재질을 아무도 직접 만지지 않으므로 「프로파일 vs 재질」 이중 정본이 사라진다
    ///   · 하이어라키(컨트롤러 인스펙터)에서 하늘을 바로 조절할 수 있다
    /// </summary>
    [CreateAssetMenu(menuName = "JC Environment/환경 프로파일", fileName = "ENV_Profile")]
    public class JcEnvironmentProfile : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────
        // 1. 하늘 — 확산광(디퓨즈 IBL)과 반사(스펙큘러 IBL)의 공통 원본
        // ─────────────────────────────────────────────────────────────
        [Header("① 하늘 (Skybox) — 모든 환경광의 원본")]
        [Tooltip("이 시간대의 스카이박스 재질. 아래 파라미터가 이 재질에 기록된다(재질은 결과물 — 직접 열어 만지지 말 것).\n" +
                 "비우면 현행 하늘 유지.")]
        public Material skyboxMaterial;

        [Tooltip("현재 셰이더가 지원하는 하늘 파라미터(노출 등)를 프로파일에서 재질에 적용합니다.\n" +
                 "HDRI에서도 노출 제어에 사용됩니다. 미지원 항목은 건너뛰며, 태양 방위 정합과 구름 제어는 이 토글과 별개입니다.")]
        public bool driveSkyboxParams = true;

        [Header("①-b 하늘↔태양 방위 정합 (HDRI 전용)")]
        [Tooltip("★켜면 태양(③) 방위에 맞춰 하늘을 자동으로 돌린다(_Rotation).\n" +
                 "HDRI 하늘은 태양이 그림 안에 박혀 있어, 라이트만 돌리면 그림자 방향과 하늘의 태양이 어긋난다.\n" +
                 "이 스위치가 「라이트를 돌리면 하늘이 따라 돈다」를 보장한다.\n" +
                 "_Rotation 프로퍼티가 없는 셰이더에서는 자동으로 무시된다.")]
        public bool alignSkyToSun = true;

        [Tooltip("★이 하늘 그림 안에서 태양이 있는 방위각(도) — _Rotation = 0 일 때 기준.\n" +
                 "하늘 재질을 바꾸면 이 값도 다시 재야 한다 — 컨트롤러 인스펙터의 [🔎 태양 자동 탐색] 버튼이 실측해 채운다.\n" +
                 "적용식: _Rotation = 이 값 − 태양이 있는 실제 방위.")]
        [Range(0f, 360f)] public float skySunAzimuth = 36.1f;

        [Tooltip("하늘 그림 속 태양의 고도(도) — 자동 탐색이 함께 기록하는 참고값(적용되지 않는다).\n" +
                 "★고도는 _Rotation 으로 보정할 수 없다. 태양(③)의 X 각도가 이 값과 크게 다르면\n" +
                 "그림자 길이와 하늘의 태양 높이가 어긋나 보이므로, 맞추려면 라이트 X 를 이 값 근처로 내려야 한다.")]
        public float skySunElevation = 27.3f;

        [Header("①-c 구름 레이어 공전")]
        [Range(-2,2), InspectorName("구름 공전 속도 (도/초)"), Tooltip("구름만 천천히 회전합니다. 0이면 정지, 음수이면 반대 방향입니다. 태양과 원경 지형은 고정되며 레이어형 스카이박스에서만 적용됩니다.")]
        public float skyCloudSpeed = .2f;
        [Range(0,360), InspectorName("구름 시작 각도"), Tooltip("구름 레이어의 기준 각도입니다. 속도를 0으로 두고 이 값을 바꾸면 수면에 비치는 위치를 비교할 수 있습니다.")]
        public float skyCloudOffset = 0f;
        [Range(0,1), InspectorName("구름 표시 강도"), Tooltip("0이면 구름을 숨겨 고정 하늘·태양·풍경만 확인합니다. 1이면 원래 구름의 불투명도입니다.")]
        public float skyCloudOpacity = 1f;

        [Tooltip("하늘 색조. 지원 셰이더의 색조를 조절하며 중립은 회색(0.5)입니다.")]
        [ColorUsage(false, true)] public Color skyTint = new Color(0.5f, 0.5f, 0.5f);

        [Tooltip("★하늘 전체 밝기 = IBL 세기에 직결. HDR 에너지의 주 공급원이다(1.0 초과가 나와야 톤맵이 일한다).")]
        [Range(0f, 8f)] public float skyExposure = 1.3f;

        // ─────────────────────────────────────────────────────────────
        // 2. 환경광 (디퓨즈 IBL) — 음지를 물들이는 빛
        // ─────────────────────────────────────────────────────────────
        [Header("② 환경광 (디퓨즈 IBL) — 음지 담당")]
        [Tooltip("환경광 세기 배율. Skybox 소스일 때의 주 노브 — 그림자 속 밝기를 여기서 잡는다.")]
        [Range(0f, 8f)] public float ambientIntensity = 1f;

        // ─────────────────────────────────────────────────────────────
        // 3. 태양 (직접광) — 형태와 그림자 담당
        // ─────────────────────────────────────────────────────────────
        [Header("③ 태양 (디렉셔널) — 형태·그림자 담당")]
        [Tooltip("빛 색. 맑은 낮 = 살짝 웜톤이 정석(환경광을 쿨하게 두어 보색 대비 — 양지/음지가 색으로도 갈린다).")]
        public Color lightColor = new Color(1f, 0.956f, 0.839f, 1f);
        [Tooltip("강도. ★하늘(환경광)로 기저를 먼저 잡은 뒤 워시아웃 직전까지 올리는 것이 순서다.")]
        [Range(0f, 8f)] public float lightIntensity = 1f;
        [Tooltip("회전(오일러). X=고도(50 전후), Y=방위 — 쿼터뷰 그림자 방향.")]
        public Vector3 lightEuler = new Vector3(50f, 30f, 0f);

        // ─────────────────────────────────────────────────────────────
        // 4. 반사 (스펙큘러 IBL)
        // ─────────────────────────────────────────────────────────────
        [Header("④ 반사 (스펙큘러 IBL)")]
        [Tooltip("환경 리플렉션 강도(RenderSettings.reflectionIntensity). 프로브·스카이박스 반사 양쪽에 곱해진다.")]
        [Range(0f, 1f)] public float reflectionIntensity = 1f;

    }
}
