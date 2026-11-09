using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JC.BattleTesting
{
    // JC | 2026-11-05 | 공용 낮밤 적용 이후의 환경을 기준으로 보스 전용 보정만 얹습니다.
    // 실행 중 초안·조명·재질은 이 전투의 수명에 묶고, 원본 프로필/무대/공용 파이프라인을 수정하지 않습니다.
    [DisallowMultipleComponent]
    public sealed partial class JcBossEnvironment : MonoBehaviour
    {
        public JcBossEnvironmentProfile Source { get; private set; }
        public JcBossEnvironmentProfile Draft { get; private set; }
        public Transform Stage { get; private set; }
        public string ValidationMessage { get; private set; }
        readonly List<LightState> baseLights = new List<LightState>();
        readonly List<Light> localLights = new List<Light>();
        readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
        readonly List<Material> materialCopies = new List<Material>();
        Transform lightRoot;
        EnvironmentState baseline;
        bool initialized, applied;

        sealed class LightState
        {
            public Light light;
            public Color color;
            public float intensity, shadow;
        }

        struct EnvironmentState
        {
            public Color sky, equator, ground, ambient;
            public float intensity, reflection;
            public SphericalHarmonicsL2 probe;
            public static EnvironmentState Capture() => new EnvironmentState {
                sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor,
                ground = RenderSettings.ambientGroundColor, ambient = RenderSettings.ambientLight,
                intensity = RenderSettings.ambientIntensity, reflection = RenderSettings.reflectionIntensity,
                probe = RenderSettings.ambientProbe
            };
            public void Restore()
            {
                RenderSettings.ambientLight = ambient;
                RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground; RenderSettings.ambientIntensity = intensity;
                RenderSettings.reflectionIntensity = reflection; RenderSettings.ambientProbe = probe;
            }
        }

        public void Initialize(JcBossEnvironmentProfile source, Transform stage)
        {
            if (initialized || source == null || stage == null || stage.gameObject.scene != gameObject.scene) return;
            Source = source; Stage = stage;
            Draft = Instantiate(source); Draft.name = source.name + " (조절 중)"; Draft.hideFlags = HideFlags.DontSave;
            baseline = EnvironmentState.Capture();
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                    if (light.type == LightType.Directional)
                        baseLights.Add(new LightState { light = light, color = light.color, intensity = light.intensity, shadow = light.shadowStrength });
            // JC | 2026-11-05 | 생성된 하위 조명을 직접 편집해 프로필과 값이 갈라지지 않도록 읽기 전용으로 표시합니다.
            var go = new GameObject("추가 라이트") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false); lightRoot = go.transform;
            initialized = true;
            RegisterLocalLighting();
            ApplyDraft();
        }

        // JC | 2026-11-05 | 매번 최초 기준값에서 계산하여 반복 편집 시 밝기·그림자가 누적되지 않게 합니다.
        public void ApplyDraft()
        {
            if (!initialized || !isActiveAndEnabled || Draft == null || Stage == null || SceneManager.GetActiveScene() != gameObject.scene) return;
            // JC | 2026-11-09 | 저장값 다시 적용 시 공통 주변 음영과 캐릭터 모드도 함께 복원합니다.
            GetComponentInParent<JcIndoorEnvironment>()?.ApplySharedSettings();
            // JC | 2026-11-05 | 도시의 밝기는 공통 시간대가 소유합니다. 실내 프로필 변경으로 원경을 덮어쓰지 않습니다.
            if(!JcBossCityLighting.IsActive(gameObject.scene))
            {
            foreach (var state in baseLights)
                if (state.light)
                {
                    state.light.color = state.color * Draft.directionalTint;
                    state.light.intensity = state.intensity * Mathf.Max(0, Draft.directionalIntensity);
                    state.light.shadowStrength = Mathf.Clamp01(state.shadow * Mathf.Max(0, Draft.shadowStrength));
                }
            var tint = Draft.ambientTint * Mathf.Max(0, Draft.ambientIntensity);
            RenderSettings.ambientLight = baseline.ambient * tint;
            RenderSettings.ambientSkyColor = baseline.sky * tint;
            RenderSettings.ambientEquatorColor = baseline.equator * tint;
            RenderSettings.ambientGroundColor = baseline.ground * tint;
            RenderSettings.ambientIntensity = baseline.intensity * Mathf.Max(0, Draft.ambientIntensity);
            var probe = baseline.probe;
            for (int channel = 0; channel < 3; channel++)
                for (int coefficient = 0; coefficient < 9; coefficient++) probe[channel, coefficient] *= tint[channel];
            RenderSettings.ambientProbe = probe;
            RenderSettings.reflectionIntensity = baseline.reflection * Mathf.Max(0, Draft.reflectionIntensity);
            }
            // JC | 2026-11-05 | 창문광과 추가광은 동일한 수광 그룹에 묶고 표면 재생성 뒤에도 분리를 유지합니다.
            ApplyLights(); ApplyWindows(); ApplyMaterials();
            if (SurfaceController) SurfaceController.ApplySurfaces();
            applied = true;
        }

        void ApplyLights()
        {
            // JC | 2026-11-09 | 전용 보조광 제거/비활성 시 이전 빛이 남지 않도록 초기화합니다.
            Shader.SetGlobalVector("_JcBankFillDirection", Vector4.zero); Shader.SetGlobalVector("_JcBankFillColor", Vector4.zero);
            int count = Draft.lights == null ? 0 : Draft.lights.Count;
            while (localLights.Count > count)
            {
                int last = localLights.Count - 1;
                localLights[last].enabled = false; Release(localLights[last].gameObject); localLights.RemoveAt(last);
            }
            while (localLights.Count < count)
            {
                // JC | 2026-11-09 | 조명별 편집도 공통 초안으로 연결하고 원본 Light 인스펙터는 숨깁니다.
                var go = new GameObject("추가 조명") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(lightRoot, false);
                var light = go.AddComponent<Light>(); light.hideFlags = HideFlags.HideInInspector;
                localLights.Add(light); go.AddComponent<JcIndoorLightControl>().Bind(this,localLights.Count-1);
            }
            for (int i = 0; i < count; i++)
            {
                var spec = Draft.lights[i]; var light = localLights[i];
                light.enabled = spec != null && spec.enabled;
                if (spec == null) continue;
                // JC | 2026-11-09 | 기존 Point=0/Spot=1 값을 유지하면서 공통 광원 적용기로 전달합니다.
                JcIndoorLightSettings.Apply(light, Stage, spec.label, spec.enabled, JcIndoorLightSettings.Resolve(spec.type),
                    spec.position, spec.rotation, spec.color, spec.intensity, spec.range, spec.spotAngle, spec.innerSpotRatio,
                    spec.shadows, spec.shadowStrength, spec.shadowBias, spec.shadowNormalBias);
                ConfigureLocalLight(light, spec.cullingMask);
                if(spec.playerBackFill) JcIndoorLightSettings.ApplyPlayerFill(light,spec.enabled);
            }
        }

        // JC | 2026-11-05 | 기존 유리 조절기 등이 설정한 현재 재질도 기준값으로 보관하여 해제 시 되돌립니다.
        void ApplyMaterials()
        {
            RestoreMaterials(); ValidationMessage = string.Empty;
            if (Draft.materials == null) return;
            foreach (var spec in Draft.materials)
            {
                if (spec == null || !spec.enabled) continue;
                var node = string.IsNullOrEmpty(spec.rendererPath) ? Stage : Stage.Find(spec.rendererPath);
                var target = node ? node.GetComponent<Renderer>() : null;
                if (target == null || spec.material == null || spec.slot < 0 || spec.slot >= target.sharedMaterials.Length)
                {
                    ValidationMessage += "재질 대상/슬롯/재질을 확인하세요: " + spec.rendererPath + " [" + spec.slot + "]\n";
                    continue;
                }
                if (!originalMaterials.ContainsKey(target)) originalMaterials.Add(target, target.sharedMaterials);
                var materials = target.sharedMaterials;
                var copy = new Material(spec.material) { hideFlags = HideFlags.DontSave, name = spec.material.name + " (보스전 전용)" };
                materialCopies.Add(copy); materials[spec.slot] = copy; target.sharedMaterials = materials;
            }
        }

        void RestoreMaterials()
        {
            foreach (var entry in originalMaterials) if (entry.Key) entry.Key.sharedMaterials = entry.Value;
            originalMaterials.Clear();
            foreach (var copy in materialCopies) Release(copy);
            materialCopies.Clear();
        }

        // JC | 2026-11-05 | 기본 표면을 재생성할 때 사용자 지정 셰이딩의 참조도 새 복제본으로 다시 연결합니다.
        internal void SuspendMaterialOverrides() => RestoreMaterials();
        internal void ResumeMaterialOverrides() { if (isActiveAndEnabled && Draft) ApplyMaterials(); }

        public void ReloadProfile()
        {
            if (Source == null || Draft == null) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(Source), Draft); ApplyDraft();
        }

        // JC | 2026-11-05 | 비활성화 시 로컬 변경을 복원하며, 다음 씬의 전역 환경을 이전 값으로 덮어쓰지 않습니다.
        void OnDisable()
        {
            if (!initialized) return;
            // JC | 2026-11-09 | 전투 구독과 진행 중 색 변화를 해제해 재진입/다른 씬으로 연출이 남지 않게 합니다.
            StopBattleLighting();
            foreach (var light in localLights) if (light) light.enabled = false;
            foreach (var light in windowLights) if (light) light.enabled = false;
            RestoreCityLayers();
            RestoreMaterials();
            // JC | 2026-11-05 | 도시의 밝기는 공통 시간대가 소유합니다. 실내 프로필 변경으로 원경을 덮어쓰지 않습니다.
            if(!JcBossCityLighting.IsActive(gameObject.scene))
            {
            foreach (var state in baseLights)
                if (state.light) { state.light.color = state.color; state.light.intensity = state.intensity; state.light.shadowStrength = state.shadow; }
            if (applied && !JcBossCityLighting.IsActive(gameObject.scene) && SceneManager.GetActiveScene() == gameObject.scene) baseline.Restore();
            }
            applied = false;
            // JC | 2026-11-05 | 컴포넌트를 끄면 실내 표면의 전용 수광 상태도 해제하여 기존 환경으로 복원합니다.
            if (SurfaceController) SurfaceController.ApplySurfaces();
        }
        void OnEnable() { if (initialized) ApplyDraft(); }
        void OnDestroy() { UnregisterLocalLighting(); Release(Draft); if (lightRoot) Release(lightRoot.gameObject); }
        static void Release(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
