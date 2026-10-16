using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JC.Env
{
    /// <summary>
    /// 환경 프로파일 적용기 — 씬에 하나 두고, 프로파일 값을 RenderSettings·스카이박스 재질·디렉셔널 라이트에 반영한다.
    ///
    /// 적용은 명시 호출·플레이 시작·소속 씬의 활성 전환 시만 — 매 프레임 덮어쓰지 않아
    /// 에디터에서 씬 값을 직접 스윕하는 튜닝 흐름과 싸우지 않는다.
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
                 "지정한 라이트는 RenderSettings.sun(Sun Source)에도 연결됩니다.")]
        [SerializeField] private Light directionalLight;

        [Tooltip("플레이 시작 시 프로파일을 적용합니다. 추가 로드된 씬은 활성 씬이 될 때까지 기다립니다.\n" +
                 "다른 씬에서 돌아오거나 컴포넌트를 다시 켜면 마지막 적용 상태를 복원합니다. OFF이면 명시 적용 전까지 자동 적용하지 않습니다.")]
        [SerializeField] private bool applyOnStart = true;

        [Tooltip("★씬별 방위 보정(도, Y축) — 씬마다 카메라 방위가 다를 때(탐사=-Z 시점, 전투=+X 시점 등)\n" +
                 "프로파일의 절대 각도에 이 값을 더해 「화면 기준 라이팅 인상」을 통일한다. 전투씬 ≈ ±90.\n" +
                 "프로파일은 기준 각도 하나만 소유하고, 카메라 방위 차이는 씬의 컨트롤러가 흡수하는 구조.")]
        [SerializeField] private float lightYawOffset;

        /// <summary>플레이 중 하늘 파라미터 편집용 인스턴스 — 에셋 오염 방지.</summary>
        private Material _runtimeSky;
        private Material _runtimeSkySource;
        private bool _started;
        private JcEnvironmentProfile _requestedProfile;

        public JcEnvironmentProfile Profile { get => profile; set => profile = value; }

        // ── 셰이더 프로퍼티 ID (셰이더가 지원하는 이름에만 적용) ──
        private static readonly int ID_SkyTint = Shader.PropertyToID("_Tint");
        private static readonly int ID_Exposure = Shader.PropertyToID("_Exposure");
        /// <summary>Skybox/Cubemap·Panoramic 계열의 하늘 회전(도). 미지원 셰이더는 HasProperty 가드로 걸러진다.</summary>
        private static readonly int ID_Rotation = Shader.PropertyToID("_Rotation");

        private void Update()
        {
            if (CanApply) Shader.SetGlobalFloat("_JcSkyCloudTime", Time.time);
        }

        private static void WriteCloudParams(Material sky, float speed, float offset, float opacity)
        {
            if (sky == null || !sky.HasProperty("_CloudSpeed")) return;
            sky.SetFloat("_CloudSpeed", speed);
            sky.SetFloat("_CloudOffset", offset);
            sky.SetFloat("_CloudOpacity", opacity);
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            if (_started) RestoreRequestedEnvironment();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnDestroy()
        {
            ReleaseRuntimeSky();
        }

        private void Start()
        {
            _started = true;
            RestoreRequestedEnvironment();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            if (_started && next == gameObject.scene) RestoreRequestedEnvironment();
        }

        private void RestoreRequestedEnvironment()
        {
            // 명시 적용 프로파일을 additive 씬 왕복 후에도 복원합니다.
            if (_requestedProfile != null) Apply(_requestedProfile);
            else if (applyOnStart) ApplyNow();
        }

        // Additive 로드의 Start는 SetActiveScene보다 먼저 실행될 수 있다.
        // 그때 RenderSettings를 쓰면 현재 로딩씬의 환경을 바꾸므로 요청만 기억해 둔다.
        private bool CanApply => !Application.isPlaying ||
            (isActiveAndEnabled && gameObject.scene == SceneManager.GetActiveScene());

        [ContextMenu("프로파일 적용")]
        public void ApplyNow() => Apply(profile);

        /// <summary>프로파일 하나를 그대로 적용.</summary>
        public void Apply(JcEnvironmentProfile p)
        {
            if (p == null) return;
            _requestedProfile = p;
            if (!CanApply) return;

            var light = ResolveLight();

            // 태양의 최종 회전을 먼저 확정한다 — 하늘 정합이 이 값을 참조하기 때문(적용 순서 의존).
            var sunRot = WithYawOffset(Quaternion.Euler(p.lightEuler));

            // ① 하늘 — 모든 환경광의 원본이므로 가장 먼저
            var sky = ApplySkybox(p.skyboxMaterial);
            WriteCloudParams(sky, p.skyCloudSpeed, p.skyCloudOffset, p.skyCloudOpacity);
            if (p.driveSkyboxParams) WriteSkyParams(sky,
                p.skyTint, p.skyExposure);

            // ①-b 하늘↔태양 방위 정합 — driveSkyboxParams 와 무관하게 동작한다.
            if (p.alignSkyToSun) WriteSkyRotation(sky, p.skySunAzimuth, sunRot);

            // 전역 Sun Source에 씬의 디렉셔널 라이트를 연결합니다.
            RenderSettings.sun = light;

            // ② 환경광(디퓨즈 IBL)
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = p.ambientIntensity;

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

        /// <summary>현재 씬 라이팅 상태를 프로파일로 역방향 캡처(메모리만 — 디스크는 💾 버튼).</summary>
        public void CaptureToProfile() => CaptureInto(profile);

        /// <summary>
        /// 현재 씬 라이팅 상태를 <paramref name="target"/> 에 담는다.
        /// ★260904: 에디터가 <b>초안(draft) 사본</b>에 먼저 담고 그 다음 프로파일로 옮기기 위해 대상을 분리했다
        /// (인라인 편집이 프로파일 원본을 곧바로 건드리지 않게 하려는 것 — 캡처 전까지는 프로파일 무변경).
        /// </summary>
        public void CaptureInto(JcEnvironmentProfile target)
        {
            if (target == null || !CanApply) return;

            // ① 하늘 — 현행 재질을 정본으로 기록하고, 파라미터도 함께 읽어온다
            var m = RenderSettings.skybox;
            // 저장할 프로파일에 플레이 전용 재질 참조가 들어가지 않게 원본을 기록한다.
            target.skyboxMaterial = m != null && m == _runtimeSky ? _runtimeSkySource : m;
            if (m != null && target.driveSkyboxParams)
            {
                if (m.HasProperty(ID_SkyTint)) target.skyTint = m.GetColor(ID_SkyTint);
                if (m.HasProperty(ID_Exposure)) target.skyExposure = m.GetFloat(ID_Exposure);
            }

            if (m != null && m.HasProperty("_CloudSpeed"))
            {
                target.skyCloudSpeed = m.GetFloat("_CloudSpeed");
                target.skyCloudOffset = m.GetFloat("_CloudOffset");
                target.skyCloudOpacity = m.GetFloat("_CloudOpacity");
            }

            // ② 환경광
            target.ambientIntensity = RenderSettings.ambientIntensity;

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
            if (src == null) src = RenderSettings.skybox;
            if (src != null && src == _runtimeSky) src = _runtimeSkySource;
            if (src == null) return null;

            // 플레이 중에는 인스턴스로 — 에셋에 실험값이 스미지 않게(에디트 모드는 에셋 직접 = 의도된 튜닝)
            Material target = src;
            if (Application.isPlaying)
            {
                if (_runtimeSky == null || _runtimeSky.shader != src.shader)
                {
                    ReleaseRuntimeSky();
                    _runtimeSky = new Material(src)
                    {
                        name = src.name + " (JC Environment Runtime)",
                        hideFlags = HideFlags.DontSave
                    };
                }
                // 같은 셰이더라도 다른 HDRI 텍스처/키워드/회전일 수 있다.
                // 옵션을 끌 때도 이전 프로파일의 덮어쓴 값 대신 원본 값으로 돌아간다.
                _runtimeSky.CopyPropertiesFromMaterial(src);
                _runtimeSkySource = src;
                target = _runtimeSky;
            }

            if (RenderSettings.skybox != target) RenderSettings.skybox = target;
            return target;
        }

        private void ReleaseRuntimeSky()
        {
            if (_runtimeSky == null) return;
            // 이미 다른 씬이 하늘을 소유하면 그 씬의 RenderSettings를 건드리지 않는다.
            if (RenderSettings.skybox == _runtimeSky) RenderSettings.skybox = _runtimeSkySource;
            if (Application.isPlaying) Destroy(_runtimeSky);
            else DestroyImmediate(_runtimeSky);
            _runtimeSky = null;
            _runtimeSkySource = null;
        }

        /// <summary>하늘 파라미터를 재질에 기록. 셰이더에 해당 프로퍼티가 없으면 조용히 건너뛴다.</summary>
        private static void WriteSkyParams(Material m, Color tint, float exposure)
        {
            if (m == null) return;
            if (m.HasProperty(ID_SkyTint)) m.SetColor(ID_SkyTint, tint);
            if (m.HasProperty(ID_Exposure)) m.SetFloat(ID_Exposure, exposure);
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

        private Quaternion WithYawOffset(Quaternion baseRot)
            => Quaternion.AngleAxis(lightYawOffset, Vector3.up) * baseRot;

        private Light ResolveLight()
        {
            if (IsLocalSun(directionalLight)) return directionalLight;
            if (IsLocalSun(RenderSettings.sun)) return directionalLight = RenderSettings.sun;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var l in root.GetComponentsInChildren<Light>())
                    if (IsLocalSun(l)) return directionalLight = l;
            Debug.LogWarning("[JcEnvironment] 소속 씬에서 활성 디렉셔널 라이트를 찾지 못했습니다.", this);
            return null;
        }

        private bool IsLocalSun(Light light) => light != null && light.isActiveAndEnabled &&
            light.type == LightType.Directional && light.gameObject.scene == gameObject.scene;
    }
}
