using UnityEngine;

namespace JC.Env
{
    /// <summary>
    /// 환경(시간대·날씨) 라이팅 프로파일 — 전역 라이팅 값의 정본.
    ///
    /// ★설계 원칙(260811): 라이팅 값을 씬에 하드코딩하지 않고 이 SO 가 소유한다.
    /// 지금은 맑은 낮(ENV_Day_Clear) 하나지만, 밤·노을·날씨는 프로파일 자산 추가만으로 열린다
    /// (기획: 시간 경과 낮/밤 표현 — 1턴=1일이라 턴 이벤트 연동 예정).
    /// 두 프로파일 사이 보간은 JcEnvironmentController.ApplyBlend 가 담당.
    /// </summary>
    [CreateAssetMenu(menuName = "JC Environment/환경 프로파일", fileName = "ENV_Profile")]
    public class JcEnvironmentProfile : ScriptableObject
    {
        [Header("디렉셔널 라이트 (태양)")]
        [Tooltip("빛 색. 맑은 낮 = 살짝 웜톤이 정석(앰비언트를 쿨하게 보색).")]
        public Color lightColor = new Color(1f, 0.956f, 0.839f, 1f);
        [Tooltip("강도. 톤맵(Neutral) 위에서는 1.0~1.4 대역이 기준.")]
        [Range(0f, 8f)] public float lightIntensity = 1f;
        [Tooltip("회전(오일러). X=고도(50 전후), Y=방위 — 쿼터뷰 그림자 방향.")]
        public Vector3 lightEuler = new Vector3(50f, 30f, 0f);

        [Header("앰비언트 — Gradient 3색 (환경광)")]
        [Tooltip("하늘색(위에서 내려오는 환경광). 그림자 진 면을 물들이는 주역.")]
        [ColorUsage(false, true)] public Color ambientSky = new Color(0.66f, 0.77f, 0.88f);
        [Tooltip("지평선색(수평 방향 환경광).")]
        [ColorUsage(false, true)] public Color ambientEquator = new Color(0.43f, 0.48f, 0.54f);
        [Tooltip("지면색(아래에서 올라오는 반사광).")]
        [ColorUsage(false, true)] public Color ambientGround = new Color(0.29f, 0.27f, 0.24f);

        [Header("스카이박스 (비주얼)")]
        [Tooltip("이 시간대의 스카이박스 재질. ★비우면 현행 유지 — 재질 프로퍼티를 쓰지 않고 통째 교체 방식(에셋 오염 없음).")]
        public Material skyboxMaterial;

        [Header("반사")]
        [Tooltip("환경 리플렉션 강도(RenderSettings.reflectionIntensity). 밤 프로파일에서 낮추는 용도 — 베이크 프로브 시간대 절충값.")]
        [Range(0f, 1f)] public float reflectionIntensity = 1f;

        [Header("안개 틴트 (예약)")]
        [Tooltip("★현재 미적용 — 전장 안개(FOW) 색 틴트 자리. 시간대별 안개 색이 필요해지면 안개 시스템과 연동한다.")]
        public Color fogTint = Color.white;
    }
}
