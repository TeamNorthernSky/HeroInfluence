using UnityEngine;
using UnityEngine.Rendering;

namespace JC.Env
{
    /// <summary>
    /// 환경 프로파일 적용기 — 씬에 하나 두고, 프로파일 값을 RenderSettings·디렉셔널 라이트에 반영한다.
    ///
    /// 적용은 명시 호출(ApplyNow / 에디터 버튼)·플레이 시작 시(applyOnStart)만 — 매 프레임 덮어쓰지 않아
    /// 에디터에서 씬 값을 직접 스윕하는 튜닝 흐름과 싸우지 않는다. 튜닝 확정 → 캡처(씬→프로파일) → 💾.
    /// 낮/밤 전환은 ApplyBlend(a, b, t) — 턴 이벤트 연동은 추후.
    /// </summary>
    [DisallowMultipleComponent]
    public class JcEnvironmentController : MonoBehaviour
    {
        [Tooltip("적용할 환경 프로파일(정본).")]
        [SerializeField] private JcEnvironmentProfile profile;

        [Tooltip("씬의 태양(디렉셔널). 비우면 씬에서 자동 탐색.")]
        [SerializeField] private Light directionalLight;

        [Tooltip("플레이 시작 시 프로파일을 1회 적용.")]
        [SerializeField] private bool applyOnStart = true;

        [Tooltip("★씬별 방위 보정(도, Y축) — 씬마다 카메라 방위가 다를 때(탐사=-Z 시점, 전투=+X 시점 등)\n" +
                 "프로파일의 절대 각도에 이 값을 더해 「화면 기준 라이팅 인상」을 통일한다. 전투씬 ≈ ±90.\n" +
                 "프로파일은 기준 각도 하나만 소유하고, 카메라 방위 차이는 씬의 컨트롤러가 흡수하는 구조.")]
        [SerializeField] private float lightYawOffset;

        public JcEnvironmentProfile Profile { get => profile; set => profile = value; }

        private void Start()
        {
            if (applyOnStart) ApplyNow();
        }

        [ContextMenu("프로파일 적용")]
        public void ApplyNow() => Apply(profile);

        /// <summary>프로파일 하나를 그대로 적용.</summary>
        public void Apply(JcEnvironmentProfile p)
        {
            if (p == null) return;
            ApplyAmbient(p.ambientSky, p.ambientEquator, p.ambientGround);
            RenderSettings.reflectionIntensity = p.reflectionIntensity;
            if (p.skyboxMaterial != null && RenderSettings.skybox != p.skyboxMaterial)
            {
                RenderSettings.skybox = p.skyboxMaterial;
                DynamicGI.UpdateEnvironment();   // 환경 리플렉션·GI에 새 하늘 반영
            }

            var l = ResolveLight();
            if (l != null)
            {
                l.color = p.lightColor;
                l.intensity = p.lightIntensity;
                l.transform.rotation = WithYawOffset(Quaternion.Euler(p.lightEuler));
            }
        }

        /// <summary>
        /// 두 프로파일 사이 보간 적용(t: 0=a, 1=b) — 낮→밤 전환 연출용.
        /// 스카이박스는 보간 불가(재질 통째 교체 방식)라 t 0.5 에서 스왑 — 부드러운 하늘 전환이
        /// 필요해지면 프로시저럴 스카이박스 전환과 함께 다시 설계한다.
        /// </summary>
        public void ApplyBlend(JcEnvironmentProfile a, JcEnvironmentProfile b, float t)
        {
            if (a == null || b == null) { Apply(a != null ? a : b); return; }
            t = Mathf.Clamp01(t);

            ApplyAmbient(
                Color.Lerp(a.ambientSky, b.ambientSky, t),
                Color.Lerp(a.ambientEquator, b.ambientEquator, t),
                Color.Lerp(a.ambientGround, b.ambientGround, t));
            RenderSettings.reflectionIntensity = Mathf.Lerp(a.reflectionIntensity, b.reflectionIntensity, t);

            var sky = t < 0.5f ? a.skyboxMaterial : b.skyboxMaterial;
            if (sky != null && RenderSettings.skybox != sky)
            {
                RenderSettings.skybox = sky;
                DynamicGI.UpdateEnvironment();
            }

            var l = ResolveLight();
            if (l != null)
            {
                l.color = Color.Lerp(a.lightColor, b.lightColor, t);
                l.intensity = Mathf.Lerp(a.lightIntensity, b.lightIntensity, t);
                l.transform.rotation = WithYawOffset(Quaternion.Slerp(
                    Quaternion.Euler(a.lightEuler), Quaternion.Euler(b.lightEuler), t));
            }
        }

        private Quaternion WithYawOffset(Quaternion baseRot)
            => Quaternion.AngleAxis(lightYawOffset, Vector3.up) * baseRot;

        /// <summary>현재 씬 라이팅 상태를 프로파일로 역방향 캡처(메모리만 — 디스크는 💾 버튼).</summary>
        public void CaptureToProfile()
        {
            if (profile == null) return;
            profile.ambientSky = RenderSettings.ambientSkyColor;
            profile.ambientEquator = RenderSettings.ambientEquatorColor;
            profile.ambientGround = RenderSettings.ambientGroundColor;
            profile.reflectionIntensity = RenderSettings.reflectionIntensity;
            // 스카이박스: 현행 재질을 그대로 정본으로 기록
            profile.skyboxMaterial = RenderSettings.skybox;

            var l = ResolveLight();
            if (l != null)
            {
                profile.lightColor = l.color;
                profile.lightIntensity = l.intensity;
                // 씬 회전에서 방위 보정을 빼고 기준 각도로 환산해 담는다(적용↔캡처 왕복 일관).
                profile.lightEuler = (Quaternion.AngleAxis(-lightYawOffset, Vector3.up)
                                      * l.transform.rotation).eulerAngles;
            }
        }

        private static void ApplyAmbient(Color sky, Color equator, Color ground)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;   // Gradient 3색 — 프로파일 체계의 전제
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
        }

        private Light ResolveLight()
        {
            if (directionalLight != null) return directionalLight;
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { directionalLight = l; return l; }
            Debug.LogWarning("[JcEnvironment] 씬에서 디렉셔널 라이트를 찾지 못했습니다.", this);
            return null;
        }
    }
}
