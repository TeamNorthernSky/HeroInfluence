using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JC.BattleTesting
{
    // JC | 2026-11-05 | 은행에서만 실외 조명·낮밤 적용을 중지합니다. 공용 적용기와 보스 환경 원본은 수정하지 않습니다.
    [DisallowMultipleComponent]
    public sealed class JcBankEnvironment : MonoBehaviour
    {
        public JcBankEnvironmentProfile Source { get; private set; }
        public JcBankEnvironmentProfile Draft { get; private set; }
        public Transform Stage { get; private set; }
        public ReflectionProbe IndoorProbe { get; private set; }
        readonly Dictionary<Behaviour, bool> external = new Dictionary<Behaviour, bool>();
        readonly List<Light> lights = new List<Light>();
        readonly Dictionary<Light, GameObject> fixtures = new Dictionary<Light, GameObject>();
        readonly Dictionary<Light, GameObject> fixtureSources = new Dictionary<Light, GameObject>();
        Transform lightRoot;
        Cubemap blackReflection;
        Renderer haloRenderer;
        Material haloOriginal, haloPreview;
        State baseline;
        bool initialized, applied, reflectionPending;
        float reflectionDue;
        int reflectionRender = -1;

        // JC | 2026-11-06 | URP 14.0.12의 SSAO 설정 형식만 직렬화하여 접근합니다. 내부 클래스 반사나 렌더러 교체 없이 기존 기능을 조절합니다.
        [System.Serializable] sealed class SsaoValues
        {
            public float Intensity, Radius, DirectLightingStrength;
            public bool Downsample;
        }
        [System.Serializable] sealed class SsaoPayload { public SsaoValues m_Settings; }
        UnityEngine.Rendering.Universal.ScriptableRendererFeature ssaoFeature;
        string ssaoBaseline;
        bool ssaoWasActive;
        public bool SsaoAvailable => Source && Source.ssaoFeature &&
            Source.ssaoFeature.GetType().FullName == "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion";

        void ApplySsao()
        {
            if (!SsaoAvailable) return;
            if (!ssaoFeature)
            {
                ssaoFeature = Source.ssaoFeature; ssaoWasActive = ssaoFeature.isActive;
                ssaoBaseline = JsonUtility.ToJson(JsonUtility.FromJson<SsaoPayload>(JsonUtility.ToJson(ssaoFeature)));
            }
            // JC | 2026-11-06 | 초안의 네 값만 덮어써 DepthNormals·샘플·블러 등 기존 품질 설정을 보존합니다.
            var payload = new SsaoPayload { m_Settings = new SsaoValues {
                Intensity = Mathf.Clamp(Draft.ssaoIntensity, 0, 3), Radius = Mathf.Clamp(Draft.ssaoRadius, .02f, 2),
                DirectLightingStrength = Mathf.Clamp01(Draft.ssaoDirectLightingStrength), Downsample = Draft.ssaoDownsample
            } };
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(payload), ssaoFeature);
            ssaoFeature.SetActive(Draft.ssaoEnabled);
        }

        void RestoreSsao()
        {
            // JC | 2026-11-06 | 씬 이탈·비활성화·플레이 종료 시 진입 전 값을 복원합니다. 저장 버튼은 은행 프로필만 저장합니다.
            if (ssaoFeature)
            {
                JsonUtility.FromJsonOverwrite(ssaoBaseline, ssaoFeature);
                ssaoFeature.SetActive(ssaoWasActive);
            }
            ssaoFeature = null; ssaoBaseline = null;
        }

        // JC | 2026-11-05 | 일시 중지·씬 종료 때 되돌릴 전역 상태를 보관하며 다음 씬에는 이전 값을 쓰지 않습니다.
        sealed class State
        {
            public Material skybox;
            public Light sun;
            public AmbientMode mode;
            public Color ambient, sky, equator, ground;
            public float intensity, reflection;
            public DefaultReflectionMode reflectionMode;
            public Texture reflectionTexture;
            public SphericalHarmonicsL2 probe;
            public bool fog;
            public Vector4 dayNight, windows, readability;
            // JC | 2026-11-06 | 은행 조명이 소유하는 플레이어 전용 보조광도 씬 이탈 때 복원합니다.
            public Vector4 playerFillDirection, playerFillColor;
            public static State Capture() => new State {
                skybox = RenderSettings.skybox, sun = RenderSettings.sun, mode = RenderSettings.ambientMode,
                ambient = RenderSettings.ambientLight, sky = RenderSettings.ambientSkyColor,
                equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor,
                intensity = RenderSettings.ambientIntensity, reflection = RenderSettings.reflectionIntensity,
                reflectionMode = RenderSettings.defaultReflectionMode, reflectionTexture = RenderSettings.customReflectionTexture,
                probe = RenderSettings.ambientProbe, fog = RenderSettings.fog,
                dayNight = Shader.GetGlobalVector("_JcDayNight"), windows = Shader.GetGlobalVector("_JcWindowLight"),
                readability = Shader.GetGlobalVector("_JcNightReadability"),
                playerFillDirection = Shader.GetGlobalVector("_JcBankFillDirection"), playerFillColor = Shader.GetGlobalVector("_JcBankFillColor")
            };
            public void Restore()
            {
                RenderSettings.skybox = skybox; RenderSettings.sun = sun; RenderSettings.ambientMode = mode;
                RenderSettings.ambientLight = ambient; RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientIntensity = intensity; RenderSettings.reflectionIntensity = reflection;
                RenderSettings.defaultReflectionMode = reflectionMode; RenderSettings.customReflectionTexture = reflectionTexture;
                RenderSettings.ambientProbe = probe; RenderSettings.fog = fog;
                Shader.SetGlobalVector("_JcDayNight", dayNight); Shader.SetGlobalVector("_JcWindowLight", windows);
                Shader.SetGlobalVector("_JcNightReadability", readability);
                Shader.SetGlobalVector("_JcBankFillDirection", playerFillDirection); Shader.SetGlobalVector("_JcBankFillColor", playerFillColor);
            }
        }

        public void Initialize(JcBankEnvironmentProfile source, Transform stage)
        {
            if (initialized || !source || !stage || stage.gameObject.scene != gameObject.scene) return;
            Source = source; Stage = stage;
            Draft = Instantiate(source); Draft.name = source.name + " (조절 중)"; Draft.hideFlags = HideFlags.DontSave;
            var root = new GameObject("실내 조명 · 개별 오브젝트에서 조절") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false); lightRoot = root.transform;
            var reflection = new GameObject("실내 반사") { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
            reflection.transform.SetParent(lightRoot, false);
            IndoorProbe = reflection.AddComponent<ReflectionProbe>();
            IndoorProbe.mode = ReflectionProbeMode.Realtime;
            IndoorProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            IndoorProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            IndoorProbe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            IndoorProbe.backgroundColor = Color.black;
            IndoorProbe.boxProjection = true; IndoorProbe.resolution = 128; IndoorProbe.hdr = true;
            IndoorProbe.nearClipPlane = .1f; IndoorProbe.farClipPlane = 100; IndoorProbe.importance = 100;
            // JC | 2026-11-05 | 실내 캡처 전·영역 밖에도 이전 하늘 반사가 유입되지 않도록 검정 기본 반사를 사용합니다.
            blackReflection = new Cubemap(1, TextureFormat.RGBAHalf, false) { name = "은행 외부 반사 차단", hideFlags = HideFlags.DontSave };
            for (int face = 0; face < 6; face++) blackReflection.SetPixel((CubemapFace)face, 0, 0, Color.black);
            blackReflection.Apply();
            // JC | 2026-11-05 | 공유 후광 재질은 복제해 조절하고 원본 에셋에는 임시 값을 쓰지 않습니다.
            var halo = Stage.Find("JC_BankGlyphHalo");
            if (halo && halo.TryGetComponent(out haloRenderer))
            {
                haloOriginal = haloRenderer.sharedMaterial;
                if (haloOriginal) haloPreview = new Material(haloOriginal) { hideFlags = HideFlags.DontSave, name = "은행 문양 후광 (조절 중)" };
            }
            initialized = true;
            Acquire();
        }

        void Acquire()
        {
            if (!initialized || applied || !isActiveAndEnabled || SceneManager.GetActiveScene() != gameObject.scene) return;
            if (baseline == null) baseline = State.Capture();
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                // JC | 2026-11-05 | 이미 배치된 환경 조명만 차단합니다. 이후 생성되는 전투 스킬의 효과광은 유지합니다.
                foreach (var component in root.GetComponentsInChildren<Behaviour>(true))
                {
                    // JC | 2026-11-05 | 조명과 무관한 빈 스크립트 슬롯은 대상에서 제외합니다.
                    if (!component || component.transform.IsChildOf(transform)) continue;
                    if (component is Light || component is ReflectionProbe ||
                        component is JC.Env.JcEnvironmentController || component is JC.Environment.JcDayNightController)
                    {
                        external[component] = component.enabled; component.enabled = false;
                    }
                }
            }
            applied = true; ApplyDraft();
        }

        public void ApplyDraft()
        {
            if (!applied || !Draft || !Stage || !isActiveAndEnabled || SceneManager.GetActiveScene() != gameObject.scene) return;
            // JC | 2026-11-09 | 하위 조명 편집/저장값 복원도 공통 명암 선택을 즉시 반영합니다.
            GetComponentInParent<JcIndoorEnvironment>()?.ApplySharedSettings();
            // JC | 2026-11-06 | 기존 환경 초안 적용과 같은 타이밍에 SSAO를 미리보기합니다.
            ApplySsao();
            RenderSettings.skybox = null; RenderSettings.sun = null; RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientIntensity = 1;
            var ambient = Draft.ambientColor * Mathf.Max(0, Draft.ambientIntensity);
            RenderSettings.ambientLight = ambient;
            var sh = new SphericalHarmonicsL2(); sh.AddAmbientLight(ambient.linear); RenderSettings.ambientProbe = sh;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = blackReflection;
            RenderSettings.reflectionIntensity = 1;
            Shader.SetGlobalVector("_JcDayNight", Vector4.zero);
            Shader.SetGlobalVector("_JcWindowLight", Vector4.zero);
            Shader.SetGlobalVector("_JcNightReadability", Vector4.zero);
            // JC | 2026-11-06 | 목록에서 전용 보조광을 끄거나 제거해도 이전 광량이 남지 않습니다.
            Shader.SetGlobalVector("_JcBankFillDirection", Vector4.zero); Shader.SetGlobalVector("_JcBankFillColor", Vector4.zero);
            int count = Draft.lights == null ? 0 : Draft.lights.Count;
            while (lights.Count > count)
            {
                int last = lights.Count - 1; fixtures.Remove(lights[last]); fixtureSources.Remove(lights[last]); lights[last].enabled = false; Release(lights[last].gameObject); lights.RemoveAt(last);
            }
            while (lights.Count < count)
            {
                var go = new GameObject("은행 조명") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(lightRoot, false);
                var light = go.AddComponent<Light>(); light.hideFlags = HideFlags.HideInInspector;
                // JC | 2026-11-09 | 은행과 보스에서 동일한 하위 광원 편집기를 사용합니다.
                lights.Add(light); go.AddComponent<JcIndoorLightControl>().Bind(this, lights.Count - 1);
            }
            for (int i = 0; i < count; i++)
            {
                var spec = Draft.lights[i]; var light = lights[i]; light.enabled = spec != null && spec.enabled;
                if (spec == null) continue;
                light.name = spec.label;
                // JC | 2026-11-05 | 월마운트 외형은 광원과 함께 움직입니다. 프로필에 연결된 프리팹을 한 번만 생성합니다.
                if (fixtureSources.TryGetValue(light, out var oldFixture) && oldFixture != spec.fixturePrefab) {
                    if (fixtures.TryGetValue(light, out var oldVisual)) { oldVisual.SetActive(false); Release(oldVisual); }
                    fixtures.Remove(light); fixtureSources.Remove(light);
                }
                if (spec.fixturePrefab && !fixtures.ContainsKey(light)) {
                    var fixture = Instantiate(spec.fixturePrefab, light.transform, false);
                    fixture.name = "월마운트 램프 외형"; fixtures.Add(light, fixture); fixtureSources.Add(light, spec.fixturePrefab);
                }
                // JC | 2026-11-09 | 은행 저장 번호를 의미로 변환한 뒤 보스와 동일한 광원 적용기를 사용합니다.
                light.cullingMask = -1;
                JcIndoorLightSettings.Apply(light, Stage, spec.label, spec.enabled, JcIndoorLightSettings.Resolve(spec.type),
                    spec.position, spec.rotation, spec.color, spec.intensity, Mathf.Max(1,spec.range), Mathf.Min(170,spec.spotAngle),
                    spec.innerSpotRatio, spec.shadows, spec.shadowStrength, spec.shadowBias, spec.shadowNormalBias);
                if (spec.playerBackFill) { JcIndoorLightSettings.ApplyPlayerFill(light,spec.enabled); continue; }
                if (light.type == LightType.Directional && light.enabled && !RenderSettings.sun) RenderSettings.sun = light;
            }
            IndoorProbe.enabled = true; IndoorProbe.transform.position = Stage.TransformPoint(Draft.reflectionPosition);
            IndoorProbe.size = new Vector3(Mathf.Max(1, Draft.reflectionSize.x), Mathf.Max(1, Draft.reflectionSize.y), Mathf.Max(1, Draft.reflectionSize.z));
            IndoorProbe.intensity = Mathf.Max(0, Draft.reflectionIntensity); IndoorProbe.blendDistance = 1;
            if (haloRenderer && haloPreview)
            {
                haloPreview.SetColor("_Tint", Draft.glyphHaloColor);
                haloPreview.SetFloat("_Intensity", Mathf.Max(0, Draft.glyphHaloIntensity));
                haloPreview.SetFloat("_Width", Mathf.Clamp(Draft.glyphHaloWidth, .03f, .5f));
                haloRenderer.sharedMaterial = haloPreview;
            }
            RequestReflection();
        }

        // JC | 2026-11-05 | 슬라이더 변경을 묶어 실내만 다시 캡처합니다. 매 프레임 반사를 재계산하지 않습니다.
        public void RequestReflection() { reflectionPending = true; reflectionDue = Time.realtimeSinceStartup + .2f; }
        public void RefreshReflectionNow()
        {
            if (!applied || !IndoorProbe || !IndoorProbe.enabled || SceneManager.GetActiveScene() != gameObject.scene) return;
            if (reflectionRender >= 0 && !IndoorProbe.IsFinishedRendering(reflectionRender)) return;
            reflectionRender = IndoorProbe.RenderProbe(); reflectionPending = false;
        }
        void LateUpdate()
        {
            if (reflectionPending && Time.realtimeSinceStartup >= reflectionDue) RefreshReflectionNow();
        }
        public void ReloadProfile()
        {
            if (!Source || !Draft) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(Source), Draft); ApplyDraft();
        }

        void Suspend()
        {
            if (!applied) return;
            // JC | 2026-11-06 | 공용 렌더러에 임시 SSAO 조절값이 남지 않도록 환경 복원과 함께 해제합니다.
            RestoreSsao();
            foreach (var light in lights) if (light) light.enabled = false;
            if (IndoorProbe) IndoorProbe.enabled = false;
            if (haloRenderer && haloRenderer.sharedMaterial == haloPreview) haloRenderer.sharedMaterial = haloOriginal;
            bool ownScene = SceneManager.GetActiveScene() == gameObject.scene;
            // JC | 2026-11-05 | 다른 활성 씬에서 해제해도 공용 적용기의 OnEnable이 새 환경을 오염시키지 않게 보호합니다.
            var current = ownScene ? baseline : State.Capture();
            foreach (var entry in external) if (entry.Key) entry.Key.enabled = entry.Value;
            external.Clear(); current.Restore(); applied = false; reflectionPending = false;
            // JC | 2026-11-05 | 중지된 캡처의 완료를 재활성화 후 계속 기다리지 않고 새 캡처를 시작합니다.
            reflectionRender = -1;
        }
        void ActiveSceneChanged(Scene previous, Scene next)
        {
            if (next == gameObject.scene) Acquire(); else Suspend();
        }
        void OnEnable() { SceneManager.activeSceneChanged += ActiveSceneChanged; Acquire(); }
        void OnDisable() { SceneManager.activeSceneChanged -= ActiveSceneChanged; Suspend(); }
        void OnDestroy()
        {
            Suspend(); Release(Draft); Release(haloPreview); Release(blackReflection); if (lightRoot) Release(lightRoot.gameObject);
        }
        // JC | 2026-11-06 | 에디트 구도 도구가 만든 환경을 명시 해제하여 공유 SSAO/전역 광원 설정을 복원합니다.
        public void ReleaseEditorPreview() => OnDestroy();
        static void Release(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
