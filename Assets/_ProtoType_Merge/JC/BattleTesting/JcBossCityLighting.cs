using System.Collections.Generic;
using System.Linq;
using JC.Environment;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JC.BattleTesting
{
    // JC | 2026-11-05 | 원경은 월드 시간대, 실내는 창문/추가광만 사용합니다. 변경값은 이 보스 무대 수명에 한정합니다.
    [ExecuteAlways, DefaultExecutionOrder(200)]
    public sealed partial class JcBossCityLighting : MonoBehaviour
    {
        public const int CityLayer = 30;
        public JcDayNightController Clock { get; private set; }
        public Light Sun { get; private set; }
        public int SelectedPeriod { get; private set; } = -1;
        readonly Dictionary<Renderer, (ShadowCastingMode cast, bool receive, Material[] materials)> originals = new();
        readonly Dictionary<GameObject, int> layers = new();
        readonly List<Material> copies = new();
        // JC | 2026-11-17 | 동일 원본의 그림자 설정 사본을 공유하여 수천 개의 중복 재질 생성을 방지합니다.
        readonly Dictionary<Material, Material> sharedCopies = new();
        // JC | 2026-11-17 | 에디트 중 복제한 원경도 임시 재질을 공유하므로 해제 전에 원본 에셋으로 되돌립니다.
        readonly Dictionary<Material, Material> copySources = new();
        readonly List<(Light light, bool enabled)> previousSuns = new();
        readonly List<(Light light, int mask, float range, float intensity)> cityLights = new();
        UniversalRenderPipelineAsset pipeline;
        RenderPipelineAsset previousPipeline;
        Light previousSun;
        // JC | 2026-11-16 | 한 무대의 모든 도시를 초기화 때만 수집합니다. 시간·태양·렌더 품질은 무대당 하나를 공유합니다.
        Transform[] cities;
        // JC | 2026-11-17 | 편집 중 도시 루트 자체를 복제해도 종료 시 새 루트의 임시 재질까지 복원합니다.
        Transform stageRoot;
        GameObject toolbar;
        readonly List<Image> buttons = new();
        bool initialized;
        static readonly HashSet<JcBossCityLighting> active = new();
        // JC | 2026-11-05 | 도메인 재로드를 생략한 플레이에서도 이전 전투 등록이 남지 않게 합니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => active.Clear();
        public static bool IsActive(Scene scene) => active.Any(c => c && c.isActiveAndEnabled && c.gameObject.scene == scene);

        public void Initialize(Transform stage, JcBossBattleBackground settings, double hour)
        {
            if (initialized || !stage || !settings || !settings.citySun || !settings.cityTimeProfile) return;
            cities = JcBossCityTime.Roots(stage);
            if (cities.Length == 0) return;
            optimization = settings;
            stageRoot = stage; // JC | 2026-11-17 | 시작 시 목록과 별개로 현재 무대 하위 도시를 다시 조회할 기준입니다.
            initialized = true; active.Add(this);
            foreach (var city in cities)
            {
                var oldTime = city.GetComponent<JcBossCityTime>();
                if (oldTime) oldTime.UseWorldLighting();
                BuildCityLods(city); // JC | 2026-11-17 | 표시용 LOD에도 같은 시간대/재질 보정을 적용합니다.
                foreach (var r in city.GetComponentsInChildren<Renderer>(true))
                {
                    if (!layers.ContainsKey(r.gameObject)) layers.Add(r.gameObject, r.gameObject.layer);
                    r.gameObject.layer = CityLayer;
                    originals.Add(r, (r.shadowCastingMode, r.receiveShadows, r.sharedMaterials));
                    // JC | 2026-11-05 | 예전 출력물의 그림자 OFF도 복구합니다. 투명 수면/선은 그림자를 만들지 않습니다.
                    var mats = r.sharedMaterials;
                    bool opaque = mats.Any(m => m && m.renderQueue < 3000);
                    r.shadowCastingMode = opaque && !(r is LineRenderer) && r.bounds.size.y >= settings.citySmallShadowHeight ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    r.receiveShadows = true;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (!mats[i] || !mats[i].HasProperty("_ReceiveShadows")) continue;
                        // JC | 2026-11-17 | 원본별 색상/텍스처는 보존하고 동일한 보정만 공유합니다.
                        if (!sharedCopies.TryGetValue(mats[i], out var m)) {
                            m = new Material(mats[i]) { hideFlags = HideFlags.DontSave };
                            m.SetFloat("_ReceiveShadows", 1); m.DisableKeyword("_RECEIVE_SHADOWS_OFF");
                            sharedCopies.Add(mats[i], m); copySources.Add(m, mats[i]); copies.Add(m);
                        }
                        mats[i] = m; // JC | 2026-11-17 | 복제 재질의 원본을 명시적으로 보관합니다.
                    }
                    r.sharedMaterials = mats;
                }
                foreach (var l in city.GetComponentsInChildren<Light>(true))
                {
                    // JC | 2026-11-17 | 복제된 원경도 직렬화된 원본 광량에서 한 번만 배율 보정합니다.
                    var baseline = city.GetComponent<JcBossCityTime>().GetLampDefaults(l);
                    cityLights.Add((l, baseline.mask, baseline.range, baseline.intensity));
                    ConfigureLamp(city, baseline);

                }
            }
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                    if (l.type == LightType.Directional) { previousSuns.Add((l, l.enabled)); l.enabled = false; }
            previousSun = RenderSettings.sun;
            var go = Instantiate(settings.citySun.gameObject, transform);
            go.name = "원경 태양·달 (월드 공통)";
            Sun = go.GetComponent<Light>(); Sun.enabled = true; Sun.cullingMask = 1 << CityLayer;
            RenderSettings.sun = Sun;
            // JC | 2026-11-05 | 먼 원경까지 그림자를 계산하되 공용 품질 에셋 자체는 변경하지 않습니다.
            previousPipeline = QualitySettings.renderPipeline;
            // JC | 2026-11-09 | 다중 창문광·천장광은 보스 전용 Forward+를 쓰고 종료 시 기존 품질로 복원합니다.
            var source = settings.interiorPipeline ? settings.interiorPipeline : GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (source)
            {
                pipeline = Instantiate(source); pipeline.hideFlags = HideFlags.DontSave;
                pipeline.name = source.name + " (보스 원경)";
                pipeline.shadowDistance = Mathf.Max(source.shadowDistance, settings.cityShadowDistance);
                QualitySettings.renderPipeline = pipeline;
            }
            // JC | 2026-11-17 | 하늘 표시 프로필만 전달하여 기존 보스 조명·반사 계산을 유지합니다.
            Clock = JcDayNightController.EnsureCity(gameObject.scene, hour, settings.cityTimeProfile, settings.citySkyProfile);
            Clock.Settings.SegmentPair(hour, out int segment, out _, out _); SelectedPeriod = segment;
            RefreshLamps();
        }

        // JC | 2026-11-05 | 프로필의 실제 구간 시각을 읽습니다. GameManager/턴/저장 데이터에는 쓰지 않습니다.
        public void SelectPeriod(int index)
        {
            if (!Clock || !Clock.Settings || index < 0 || index > 3) return;
            Clock.Settings.EnsureSegments(); SelectedPeriod = index;
            Clock.previewHour = Clock.Settings.segments[index].hour;
            Clock.ApplyHour(Clock.previewHour); RefreshLamps(); RefreshButtons();
        }
        void RefreshLamps()
        {
            // JC | 2026-11-17 | 450개 개별 Update 대신 보스 적용기가 시간대와 가시 광원을 함께 갱신합니다.
            UpdateCityLamps(true);
        }
        void LateUpdate()
        {
            if (!initialized) return;
            UpdateCityLamps(false);
            if (!Application.isPlaying) return;
            // JC | 2026-11-05 | 백쿼트 5회 콘솔의 실제 상단에 붙이고 테스트 전투에서만 제공합니다.
            var context = CombatContext.Instance;
            bool test = context && context.IsSimulation && context.SimulationPartyId == "JC_BOSS_PREVIEW";
            if (!test) { if (toolbar) toolbar.SetActive(false); return; }
            if (!toolbar && DebugPanelManager.Instance)
            {
                var panel = DebugPanelManager.Instance.GetComponentInChildren<DebugPanelController>(true);
                if (panel) BuildToolbar(panel);
            }
            if (toolbar) toolbar.SetActive(DebugPanelManager.IsDebugPanelActive);
        }
        void BuildToolbar(DebugPanelController panel)
        {
            toolbar = new GameObject("보스 원경 시간대", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            var rect = (RectTransform)toolbar.transform; rect.SetParent(panel.transform, false);
            // JC | 2026-11-05 | 화면 위로 밀려나지 않도록 콘솔 내부의 로그 탭 오른쪽 빈 줄에 배치합니다.
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1); rect.offsetMin = new Vector2(70, -54); rect.offsetMax = new Vector2(-10, -30);
            toolbar.GetComponent<Image>().color = new Color(.08f, .09f, .12f, .98f);
            var layout = toolbar.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 3;
            layout.padding = new RectOffset(2, 2, 2, 2); layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
            layout.childControlWidth = true; layout.childControlHeight = true;
            // JC | 2026-11-09 | 콘솔에 추가하는 시간대 버튼도 개발자 Noto 및 자동 대체 정책을 사용합니다.
            var font = DebugPanelManager.ResolveDeveloperFont();
            string[] names = { "아침", "오후", "저녁", "새벽" };
            for (int i = 0; i < 4; i++)
            {
                int period = i;
                var go = new GameObject(names[i], typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(rect, false); buttons.Add(go.GetComponent<Image>());
                go.GetComponent<Button>().onClick.AddListener(() => SelectPeriod(period));
                var label = new GameObject("표시", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(go.transform, false);
                var lr = (RectTransform)label.transform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
                var text = label.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = 12; text.enableAutoSizing = true; text.fontSizeMin = 8; text.fontSizeMax = 12; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
                float h = Clock.Settings.segments[i].hour;
                text.text = $"{names[i]} {Mathf.FloorToInt(h):00}:{Mathf.RoundToInt((h % 1) * 60):00}";
            }
            RefreshButtons();
        }
        void RefreshButtons()
        {
            for (int i = 0; i < buttons.Count; i++) buttons[i].color = i == SelectedPeriod ? new Color(.18f, .48f, .72f) : new Color(.22f, .24f, .29f);
        }
        void OnDestroy()
        {
            active.Remove(this);
            if (!initialized) return;
            initialized = false;
            // JC | 2026-11-05 | 전투 종료/비플레이 검증 후 품질·메시·광원 설정을 원래 상태로 돌립니다.
            if (Clock) Clock.SendMessage("OnDisable", SendMessageOptions.DontRequireReceiver);
            if (QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = previousPipeline;
            if (RenderSettings.sun == Sun) RenderSettings.sun = previousSun;
            foreach (var p in previousSuns) if (p.light) p.light.enabled = p.enabled;
            foreach (var p in cityLights) if (p.light) { p.light.cullingMask = p.mask; p.light.range = p.range; p.light.intensity = p.intensity; }
            foreach (var p in layers) if (p.Key) p.Key.layer = p.Value;
            // JC | 2026-11-17 | 초기 등록 후 사용자가 복제한 렌더러도 저장 직전에 복원합니다.
            // 이름 추측이나 대체 재질 없이 이 적용기가 만든 임시 재질만 정확한 원본으로 치환합니다.
            // JC | 2026-11-17 | 최초 도시 배열에는 이후 복붙한 형제 루트가 없으므로 현재 무대에서 수집합니다.
            foreach (var city in JcBossCityTime.Roots(stageRoot)) if (city)
            foreach (var renderer in city.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials; bool changed = false;
                for (int slot = 0; slot < materials.Length; slot++)
                    if (materials[slot] && copySources.TryGetValue(materials[slot], out var source))
                    { materials[slot] = source; changed = true; }
                if (changed) renderer.sharedMaterials = materials;
            }
            // JC | 2026-11-17 | 미리보기 중 새로 복제한 도시의 조명도 원본값으로 복구합니다.
            foreach (var city in JcBossCityTime.Roots(stageRoot)) if(city) city.GetComponent<JcBossCityTime>().RestoreLampDefaults();
            lamps.Clear(); sharedCopies.Clear(); copySources.Clear();
            foreach (var p in originals) if (p.Key) { p.Key.shadowCastingMode = p.Value.cast; p.Key.receiveShadows = p.Value.receive; p.Key.sharedMaterials = p.Value.materials; }
            ReleaseCityLods(); // JC | 2026-11-17 | 저장 전 원본 렌더러와 배치를 복원합니다.
            foreach (var m in copies) Release(m);
            Release(toolbar); Release(pipeline);
        }
        static void Release(Object value) { if (!value) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
