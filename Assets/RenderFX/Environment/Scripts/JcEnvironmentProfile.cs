using UnityEngine;

namespace JC.Env
{
    /// <summary>
    /// 환경광(디퓨즈 IBL) 소스 — Unity <see cref="UnityEngine.Rendering.AmbientMode"/> 의 우리 용어 래핑.
    /// ★260901 멘토링 방향 전환: 기본값은 <b>Skybox</b>. 하늘 하나가 확산광·반사의 공통 원본이 되는 구조가 정상이다.
    /// Gradient(3색)는 방위 정보와 HDR 에너지를 잃으므로 실험·폴백 용도로만 남긴다.
    /// </summary>
    public enum JcAmbientSource
    {
        [InspectorName("하늘 (Skybox) — 디퓨즈 IBL ★권장")] Skybox = 0,
        [InspectorName("그라디언트 3색 — 폴백")] Gradient = 1,
        [InspectorName("단색 — 평평")] Flat = 2,
    }

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
    ///   · 낮/밤 전환이 <b>재질 스왑이 아니라 파라미터 Lerp</b> 로 매끄러워진다(ApplyBlend)
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

        [Tooltip("아래 하늘 파라미터를 재질에 기록할지 여부.\n" +
                 "★HDRI 큐브맵 스카이박스를 쓸 때는 꺼 둘 것(해당 프로퍼티가 없어 무의미하다).")]
        public bool driveSkyboxParams = true;

        [Header("①-b 하늘↔태양 방위 정합 (HDRI 전용)")]
        [Tooltip("★켜면 태양(③) 방위에 맞춰 하늘을 자동으로 돌린다(_Rotation).\n" +
                 "HDRI 하늘은 태양이 그림 안에 박혀 있어, 라이트만 돌리면 그림자 방향과 하늘의 태양이 어긋난다.\n" +
                 "이 스위치가 「라이트를 돌리면 하늘이 따라 돈다」를 보장한다.\n" +
                 "_Rotation 프로퍼티가 없는 셰이더(Procedural 등)에서는 자동으로 무시된다.")]
        public bool alignSkyToSun = true;

        [Tooltip("★이 하늘 그림 안에서 태양이 있는 방위각(도) — _Rotation = 0 일 때 기준.\n" +
                 "하늘 재질을 바꾸면 이 값도 다시 재야 한다 — 컨트롤러 인스펙터의 [🔎 태양 자동 탐색] 버튼이 실측해 채운다.\n" +
                 "적용식: _Rotation = 이 값 − 태양이 있는 실제 방위.")]
        [Range(0f, 360f)] public float skySunAzimuth = 36.1f;

        [Tooltip("하늘 그림 속 태양의 고도(도) — 자동 탐색이 함께 기록하는 참고값(적용되지 않는다).\n" +
                 "★고도는 _Rotation 으로 보정할 수 없다. 태양(③)의 X 각도가 이 값과 크게 다르면\n" +
                 "그림자 길이와 하늘의 태양 높이가 어긋나 보이므로, 맞추려면 라이트 X 를 이 값 근처로 내려야 한다.")]
        public float skySunElevation = 27.3f;

        [Tooltip("하늘 색조. Procedural 기준 중립은 회색(0.5). 카툰 룩은 채도를 살짝 올리는 방향.")]
        [ColorUsage(false, true)] public Color skyTint = new Color(0.5f, 0.5f, 0.5f);

        [Tooltip("아래쪽 반구 색 — 물체 밑면에 도는 빛에 직결된다(지면 반사광의 대역).")]
        [ColorUsage(false, true)] public Color skyGroundColor = new Color(0.369f, 0.349f, 0.341f);

        [Tooltip("대기 두께. 낮추면 맑고 진한 하늘 / 높이면 뿌옇고 노을기가 돈다.")]
        [Range(0f, 5f)] public float atmosphereThickness = 1f;

        [Tooltip("★하늘 전체 밝기 = IBL 세기에 직결. HDR 에너지의 주 공급원이다(1.0 초과가 나와야 톤맵이 일한다).")]
        [Range(0f, 8f)] public float skyExposure = 1.3f;

        [Tooltip("태양 원반 크기(하늘 그림 전용 — 조명 계산과 무관).")]
        [Range(0f, 1f)] public float sunSize = 0.04f;

        [Tooltip("태양 원반 가장자리 수렴도. 클수록 또렷하다.")]
        [Range(1f, 10f)] public float sunSizeConvergence = 5f;

        // ─────────────────────────────────────────────────────────────
        // 2. 환경광 (디퓨즈 IBL) — 음지를 물들이는 빛
        // ─────────────────────────────────────────────────────────────
        [Header("② 환경광 (디퓨즈 IBL) — 음지 담당")]
        [Tooltip("환경광을 어디서 만들지. ★Skybox 가 정상 — 위 하늘에서 SH 9계수를 자동 생성한다.\n" +
                 "Gradient 는 수평 방위 정보(SH 9칸 중 5칸)를 잃으므로 폴백 전용.")]
        public JcAmbientSource ambientSource = JcAmbientSource.Skybox;

        [Tooltip("환경광 세기 배율. Skybox 소스일 때의 주 노브 — 그림자 속 밝기를 여기서 잡는다.")]
        [Range(0f, 8f)] public float ambientIntensity = 1f;

        [Header("②-b 그라디언트 3색 — Gradient 선택 시에만 사용")]
        [Tooltip("하늘색(위에서 내려오는 환경광).")]
        [ColorUsage(false, true)] public Color ambientSky = new Color(0.66f, 0.77f, 0.88f);
        [Tooltip("지평선색(수평 방향 환경광).")]
        [ColorUsage(false, true)] public Color ambientEquator = new Color(0.43f, 0.48f, 0.54f);
        [Tooltip("지면색(아래에서 올라오는 반사광). Flat 선택 시에는 이 색 하나만 쓰인다.")]
        [ColorUsage(false, true)] public Color ambientGround = new Color(0.29f, 0.27f, 0.24f);

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

        // ─────────────────────────────────────────────────────────────
        // 5. 예약
        // ─────────────────────────────────────────────────────────────
        [Header("⑤ 안개 틴트 (예약)")]
        [Tooltip("★현재 미적용 — 전장 안개(FOW) 색 틴트 자리. 시간대별 안개 색이 필요해지면 안개 시스템과 연동한다.")]
        public Color fogTint = Color.white;
    }
}
