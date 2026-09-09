using UnityEngine;
using UnityEngine.Rendering;

namespace JC.Env
{
    /// <summary>
    /// 환경 프로파일 적용기 — 씬에 하나 두고, 프로파일 값을 RenderSettings·스카이박스 재질·디렉셔널 라이트에 반영한다.
    ///
    /// 적용은 명시 호출(ApplyNow / 에디터 버튼)·플레이 시작 시(applyOnStart)만 — 매 프레임 덮어쓰지 않아
    /// 에디터에서 씬 값을 직접 스윕하는 튜닝 흐름과 싸우지 않는다.
    /// 낮/밤 전환은 ApplyBlend(a, b, t) — 턴 이벤트 연동은 추후.
    ///
    /// ★260902 구조 개편: 하늘 파라미터를 프로파일이 소유하고 이 컴포넌트가 재질에 기록한다.
    ///   에디트 모드에서는 재질 에셋을 직접 갱신(=의도된 튜닝),
    ///   플레이 중에는 <b>인스턴스를 만들어</b> 갱신한다(플레이 중 변경이 에셋에 스미는 사고 방지).
    /// </summary>
    [DisallowMultipleComponent]
    public class JcEnvironmentController : MonoBehaviour
    {
        [Tooltip("적용할 환경 프로파일(정본).")]
        [SerializeField] private JcEnvironmentProfile profile;

        [Tooltip("씬의 태양(디렉셔널). 비우면 씬에서 자동 탐색.\n" +
                 "★여기 꽂힌 라이트가 RenderSettings.sun(Sun Source)에도 결선된다 — Procedural 하늘의 태양 원반이 이 방향을 따른다.")]
        [SerializeField] private Light directionalLight;

        [Tooltip("플레이 시작 시 프로파일을 1회 적용.")]
        [SerializeField] private bool applyOnStart = true;

        [Tooltip("★씬별 방위 보정(도, Y축) — 씬마다 카메라 방위가 다를 때(탐사=-Z 시점, 전투=+X 시점 등)\n" +
                 "프로파일의 절대 각도에 이 값을 더해 「화면 기준 라이팅 인상」을 통일한다. 전투씬 ≈ ±90.\n" +
                 "프로파일은 기준 각도 하나만 소유하고, 카메라 방위 차이는 씬의 컨트롤러가 흡수하는 구조.")]
        [SerializeField] private float lightYawOffset;

        /// <summary>플레이 중 하늘 파라미터 편집용 인스턴스 — 에셋 오염 방지.</summary>
        private Material _runtimeSky;

        public JcEnvironmentProfile Profile { get => profile; set => profile = value; }

        // ── 셰이더 프로퍼티 ID (Procedural Skybox 기준. 커스텀 셰이더도 같은 이름을 쓰면 그대로 동작) ──
        private static readonly int ID_SkyTint = Shader.PropertyToID("_SkyTint");
        private static readonly int ID_GroundColor = Shader.PropertyToID("_GroundColor");
        private static readonly int ID_AtmosphereThickness = Shader.PropertyToID("_AtmosphereThickness");
        private static readonly int ID_Exposure = Shader.PropertyToID("_Exposure");
        private static readonly int ID_SunSize = Shader.PropertyToID("_SunSize");
        private static readonly int ID_SunSizeConvergence = Shader.PropertyToID("_SunSizeConvergence");
        /// <summary>Skybox/Cubemap·Panoramic 계열의 하늘 회전(도). Procedural 에는 없다 — HasProperty 가드로 걸러진다.</summary>
        private static readonly int ID_Rotation = Shader.PropertyToID("_Rotation");

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

            var light = ResolveLight();

            // 태양의 최종 회전을 먼저 확정한다 — 하늘 정합이 이 값을 참조하기 때문(적용 순서 의존).
            var sunRot = WithYawOffset(Quaternion.Euler(p.lightEuler));

            // ① 하늘 — 모든 환경광의 원본이므로 가장 먼저
            var sky = ApplySkybox(p.skyboxMaterial);
            if (p.driveSkyboxParams) WriteSkyParams(sky,
                p.skyTint, p.skyGroundColor, p.atmosphereThickness, p.skyExposure, p.sunSize, p.sunSizeConvergence);

            // ①-b 하늘↔태양 방위 정합 — driveSkyboxParams 와 무관하게 동작한다.
            //     (HDRI 를 쓸 때 driveSkyboxParams 는 꺼 두는 것이 정석인데, 정합이 필요한 상황도 바로 그때다)
            if (p.alignSkyToSun) WriteSkyRotation(sky, p.skySunAzimuth, sunRot);

            // Sun Source — Procedural 하늘의 태양 원반이 실제 조명 방향을 따르게 한다
            if (light != null) RenderSettings.sun = light;

            // ② 환경광(디퓨즈 IBL)
            ApplyAmbient(p.ambientSource, p.ambientIntensity, p.ambientSky, p.ambientEquator, p.ambientGround);

            // ④ 반사(스펙큘러 IBL)
            RenderSettings.reflectionIntensity = p.reflectionIntensity;

            // ③ 태양(직접광)
            if (light != null)
            {
                light.color = p.lightColor;
                light.intensity = p.lightIntensity;
                light.transform.rotation = sunRot;
            }

            // 하늘·앰비언트가 바뀌었으므로 SH(앰비언트 프로브)와 환경 반사를 재계산
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>
        /// 두 프로파일 사이 보간 적용(t: 0=a, 1=b) — 낮→밤 전환 연출용.
        /// ★260902: 하늘도 <b>파라미터 보간</b>으로 매끄럽게 전환된다(구 방식은 t 0.5 재질 스왑).
        /// 단 두 프로파일의 스카이박스 재질이 다르면 셰이더가 다를 수 있어 t 0.5 에서 스왑한다.
        /// </summary>
        public void ApplyBlend(JcEnvironmentProfile a, JcEnvironmentProfile b, float t)
        {
            if (a == null || b == null) { Apply(a != null ? a : b); return; }
            t = Mathf.Clamp01(t);

            var light = ResolveLight();

            // 태양 회전을 먼저 확정(하늘 정합이 참조).
            var sunRot = WithYawOffset(Quaternion.Slerp(
                Quaternion.Euler(a.lightEuler), Quaternion.Euler(b.lightEuler), t));

            // ① 하늘 — 같은 재질이면 파라미터 보간, 다르면 t 0.5 스왑 후 목표 파라미터
            bool sameMat = a.skyboxMaterial == b.skyboxMaterial;
            var srcMat = sameMat ? a.skyboxMaterial : (t < 0.5f ? a.skyboxMaterial : b.skyboxMaterial);
            var sky = ApplySkybox(srcMat);

            if (a.driveSkyboxParams || b.driveSkyboxParams)
            {
                if (sameMat)
                    WriteSkyParams(sky,
                        Color.Lerp(a.skyTint, b.skyTint, t),
                        Color.Lerp(a.skyGroundColor, b.skyGroundColor, t),
                        Mathf.Lerp(a.atmosphereThickness, b.atmosphereThickness, t),
                        Mathf.Lerp(a.skyExposure, b.skyExposure, t),
                        Mathf.Lerp(a.sunSize, b.sunSize, t),
                        Mathf.Lerp(a.sunSizeConvergence, b.sunSizeConvergence, t));
                else
                {
                    var s = t < 0.5f ? a : b;
                    WriteSkyParams(sky, s.skyTint, s.skyGroundColor, s.atmosphereThickness,
                        s.skyExposure, s.sunSize, s.sunSizeConvergence);
                }
            }

            // ①-b 하늘↔태양 정합 — 하늘 그림이 다르면 태양 방위도 다르므로 기준값을 함께 보간한다
            //     (같은 재질이면 두 값이 같으니 보간은 항등이 된다)
            if (a.alignSkyToSun || b.alignSkyToSun)
            {
                bool align = t < 0.5f ? a.alignSkyToSun : b.alignSkyToSun;
                if (align)
                    WriteSkyRotation(sky, sameMat
                        ? Mathf.LerpAngle(a.skySunAzimuth, b.skySunAzimuth, t)
                        : (t < 0.5f ? a.skySunAzimuth : b.skySunAzimuth), sunRot);
            }

            if (light != null) RenderSettings.sun = light;

            // ② 환경광 — 소스가 다르면 t 0.5 에서 전환(모드는 보간 대상이 아니다)
            var srcMode = t < 0.5f ? a.ambientSource : b.ambientSource;
            ApplyAmbient(srcMode,
                Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, t),
                Color.Lerp(a.ambientSky, b.ambientSky, t),
                Color.Lerp(a.ambientEquator, b.ambientEquator, t),
                Color.Lerp(a.ambientGround, b.ambientGround, t));

            RenderSettings.reflectionIntensity = Mathf.Lerp(a.reflectionIntensity, b.reflectionIntensity, t);

            if (light != null)
            {
                light.color = Color.Lerp(a.lightColor, b.lightColor, t);
                light.intensity = Mathf.Lerp(a.lightIntensity, b.lightIntensity, t);
                light.transform.rotation = sunRot;
            }

            DynamicGI.UpdateEnvironment();
        }

        /// <summary>현재 씬 라이팅 상태를 프로파일로 역방향 캡처(메모리만 — 디스크는 💾 버튼).</summary>
        public void CaptureToProfile() => CaptureInto(profile);

        /// <summary>
        /// 현재 씬 라이팅 상태를 <paramref name="target"/> 에 담는다.
        /// ★260904: 에디터가 <b>초안(draft) 사본</b>에 먼저 담고 그 다음 프로파일로 옮기기 위해 대상을 분리했다
        /// (인라인 편집이 프로파일 원본을 곧바로 건드리지 않게 하려는 것 — 캡처 전까지는 프로파일 무변경).
        /// </summary>
        public void CaptureInto(JcEnvironmentProfile target)
        {
            if (target == null) return;

            // ① 하늘 — 현행 재질을 정본으로 기록하고, 파라미터도 함께 읽어온다
            target.skyboxMaterial = RenderSettings.skybox;
            var m = RenderSettings.skybox;
            if (m != null && target.driveSkyboxParams)
            {
                if (m.HasProperty(ID_SkyTint)) target.skyTint = m.GetColor(ID_SkyTint);
                if (m.HasProperty(ID_GroundColor)) target.skyGroundColor = m.GetColor(ID_GroundColor);
                if (m.HasProperty(ID_AtmosphereThickness)) target.atmosphereThickness = m.GetFloat(ID_AtmosphereThickness);
                if (m.HasProperty(ID_Exposure)) target.skyExposure = m.GetFloat(ID_Exposure);
                if (m.HasProperty(ID_SunSize)) target.sunSize = m.GetFloat(ID_SunSize);
                if (m.HasProperty(ID_SunSizeConvergence)) target.sunSizeConvergence = m.GetFloat(ID_SunSizeConvergence);
            }

            // ② 환경광
            target.ambientSource = FromUnity(RenderSettings.ambientMode);
            target.ambientIntensity = RenderSettings.ambientIntensity;
            target.ambientSky = RenderSettings.ambientSkyColor;
            target.ambientEquator = RenderSettings.ambientEquatorColor;
            target.ambientGround = RenderSettings.ambientGroundColor;

            // ④ 반사
            target.reflectionIntensity = RenderSettings.reflectionIntensity;

            // ③ 태양
            var l = ResolveLight();
            if (l != null)
            {
                target.lightColor = l.color;
                target.lightIntensity = l.intensity;
                // 씬 회전에서 방위 보정을 빼고 기준 각도로 환산해 담는다(적용↔캡처 왕복 일관).
                target.lightEuler = (Quaternion.AngleAxis(-lightYawOffset, Vector3.up)
                                     * l.transform.rotation).eulerAngles;

                // ①-b 정합 기준 역산 — 지금 씬이 「맞춰진 상태」라고 보고 하늘의 태양 방위를 되돌려 담는다.
                //     캡처→적용 왕복이 같은 _Rotation 을 재현하게 만드는 항등식이다.
                if (target.alignSkyToSun && m != null && m.HasProperty(ID_Rotation))
                    target.skySunAzimuth = Mathf.Repeat(
                        m.GetFloat(ID_Rotation) + SunAzimuthOf(l.transform.rotation), 360f);
            }
        }

        // ── 내부 ─────────────────────────────────────────────────────

        /// <summary>스카이박스를 지정하고, 실제로 파라미터를 쓸 대상 재질을 돌려준다.</summary>
        private Material ApplySkybox(Material src)
        {
            if (src == null) return RenderSettings.skybox;

            // 플레이 중에는 인스턴스로 — 에셋에 실험값이 스미지 않게(에디트 모드는 에셋 직접 = 의도된 튜닝)
            Material target = src;
            if (Application.isPlaying)
            {
                if (_runtimeSky == null || _runtimeSky.shader != src.shader)
                    _runtimeSky = new Material(src);
                target = _runtimeSky;
            }

            if (RenderSettings.skybox != target) RenderSettings.skybox = target;
            return target;
        }

        /// <summary>하늘 파라미터를 재질에 기록. 셰이더에 해당 프로퍼티가 없으면 조용히 건너뛴다.</summary>
        private static void WriteSkyParams(Material m, Color tint, Color ground,
            float thickness, float exposure, float sunSize, float sunConvergence)
        {
            if (m == null) return;
            if (m.HasProperty(ID_SkyTint)) m.SetColor(ID_SkyTint, tint);
            if (m.HasProperty(ID_GroundColor)) m.SetColor(ID_GroundColor, ground);
            if (m.HasProperty(ID_AtmosphereThickness)) m.SetFloat(ID_AtmosphereThickness, thickness);
            if (m.HasProperty(ID_Exposure)) m.SetFloat(ID_Exposure, exposure);
            if (m.HasProperty(ID_SunSize)) m.SetFloat(ID_SunSize, sunSize);
            if (m.HasProperty(ID_SunSizeConvergence)) m.SetFloat(ID_SunSizeConvergence, sunConvergence);
        }

        /// <summary>
        /// 하늘을 태양 방위에 맞춰 돌린다.
        ///
        /// ★관계식(260904 실측): 하늘 그림 속 태양의 방위 = <c>skySunAzimuth − _Rotation</c> — 기울기가 정확히 −1이다
        /// (_Rotation 0~330 을 30° 간격으로 훑어 12점 전부 일치 확인).
        /// 따라서 실제 태양이 있어야 할 방위 <c>sunAz</c> 에 하늘을 맞추려면 <c>_Rotation = skySunAzimuth − sunAz</c>.
        ///
        /// ★고도는 이 식으로 보정할 수 없다 — _Rotation 은 Y축 회전 하나뿐이기 때문이다.
        /// 하늘의 태양 고도와 라이트 고도를 맞추려면 라이트의 X 각도를 움직여야 한다(에디터가 경고로 알린다).
        /// </summary>
        private static void WriteSkyRotation(Material m, float skySunAzimuth, Quaternion sunRot)
        {
            if (m == null || !m.HasProperty(ID_Rotation)) return;
            m.SetFloat(ID_Rotation, Mathf.Repeat(skySunAzimuth - SunAzimuthOf(sunRot), 360f));
        }

        /// <summary>
        /// 라이트 회전 → 「태양이 떠 있는 방위각」(도, +Z=0 기준 시계방향).
        /// 라이트는 빛이 <b>나아가는</b> 방향(forward)을 보므로 태양의 위치는 그 반대편(−forward)이다.
        /// </summary>
        public static float SunAzimuthOf(Quaternion lightRot)
        {
            Vector3 s = -(lightRot * Vector3.forward);
            return Mathf.Repeat(Mathf.Atan2(s.x, s.z) * Mathf.Rad2Deg, 360f);
        }

        /// <summary>라이트 회전 → 태양 고도(도). 하늘 태양 고도와 비교해 경고를 띄우는 용도.</summary>
        public static float SunElevationOf(Quaternion lightRot)
            => Mathf.Asin(Mathf.Clamp(-(lightRot * Vector3.forward).y, -1f, 1f)) * Mathf.Rad2Deg;

        /// <summary>프로파일의 각도 + 씬 방위 보정 = 실제로 씬에 적용될 태양 회전.</summary>
        public Quaternion ResolveSunRotation(JcEnvironmentProfile p)
            => WithYawOffset(Quaternion.Euler(p != null ? p.lightEuler : Vector3.zero));

        /// <summary>씬의 태양(디렉셔널). 에디터 도구용 — 없으면 null.</summary>
        public Light SunLight => ResolveLight();

        private static void ApplyAmbient(JcAmbientSource source, float intensity,
            Color sky, Color equator, Color ground)
        {
            RenderSettings.ambientMode = ToUnity(source);
            RenderSettings.ambientIntensity = intensity;
            // Gradient/Flat 에서만 실제로 쓰이지만, 왕복 일관을 위해 항상 기록해 둔다
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
        }

        private static AmbientMode ToUnity(JcAmbientSource s) => s switch
        {
            JcAmbientSource.Gradient => AmbientMode.Trilight,
            JcAmbientSource.Flat => AmbientMode.Flat,
            _ => AmbientMode.Skybox,
        };

        private static JcAmbientSource FromUnity(AmbientMode m) => m switch
        {
            AmbientMode.Trilight => JcAmbientSource.Gradient,
            AmbientMode.Flat => JcAmbientSource.Flat,
            _ => JcAmbientSource.Skybox,
        };

        private Quaternion WithYawOffset(Quaternion baseRot)
            => Quaternion.AngleAxis(lightYawOffset, Vector3.up) * baseRot;

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
