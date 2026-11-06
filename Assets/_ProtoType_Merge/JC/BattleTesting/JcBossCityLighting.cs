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
    public sealed class JcBossCityLighting : MonoBehaviour
    {
        public const int CityLayer = 30;
        public JcDayNightController Clock { get; private set; }
        public Light Sun { get; private set; }
        public int SelectedPeriod { get; private set; } = -1;
        readonly Dictionary<Renderer, (ShadowCastingMode cast, bool receive, Material[] materials)> originals = new();
        readonly Dictionary<GameObject, int> layers = new();
        readonly List<Material> copies = new();
        readonly List<(Light light, bool enabled)> previousSuns = new();
        readonly List<(Light light, int mask, float range, float intensity)> cityLights = new();
        UniversalRenderPipelineAsset pipeline;
        RenderPipelineAsset previousPipeline;
        Light previousSun;
        Transform city;
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
            city = stage.Find("도시 원경 (베이크)") ?? stage.Find("도시 미리보기");
            if (!city) return;
            initialized = true; active.Add(this);
            var oldTime = city.GetComponent<JcBossCityTime>();
            if (oldTime) oldTime.UseWorldLighting();
            foreach (var r in city.GetComponentsInChildren<Renderer>(true))
            {
                if (!layers.ContainsKey(r.gameObject)) layers.Add(r.gameObject, r.gameObject.layer);
                r.gameObject.layer = CityLayer;
                originals.Add(r, (r.shadowCastingMode, r.receiveShadows, r.sharedMaterials));
                // JC | 2026-11-05 | 예전 출력물의 그림자 OFF도 복구합니다. 투명 수면/선은 그림자를 만들지 않습니다.
                var mats = r.sharedMaterials;
                bool opaque = mats.Any(m => m && m.renderQueue < 3000);
                r.shadowCastingMode = opaque && !(r is LineRenderer) ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i] || !mats[i].HasProperty("_ReceiveShadows")) continue;
                    var m = new Material(mats[i]) { hideFlags = HideFlags.DontSave };
                    m.SetFloat("_ReceiveShadows", 1); m.DisableKeyword("_RECEIVE_SHADOWS_OFF");
                    copies.Add(m); mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
            foreach (var l in city.GetComponentsInChildren<Light>(true))
            {
                // JC | 2026-11-05 | 원경 축소율에 맞춰 광원 도달 거리와 역제곱 광량을 함께 맞춥니다.
                cityLights.Add((l, l.cullingMask, l.range, l.intensity)); l.cullingMask = 1 << CityLayer;
                float scale = Mathf.Abs(city.lossyScale.x); l.range *= scale; l.intensity *= scale * scale;
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
            var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (source)
            {
                pipeline = Instantiate(source); pipeline.hideFlags = HideFlags.DontSave;
                pipeline.name = source.name + " (보스 원경)";
                pipeline.shadowDistance = Mathf.Max(source.shadowDistance, settings.cityShadowDistance);
                QualitySettings.renderPipeline = pipeline;
            }
            Clock = JcDayNightController.EnsureCity(gameObject.scene, hour, settings.cityTimeProfile);
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
            if (!city) return;
            foreach (var lamp in city.GetComponentsInChildren<DayNightLightController>(true)) lamp.RefreshLight();
        }
        void LateUpdate()
        {
            if (!initialized || !Application.isPlaying) return;
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
            var font = panel.GetComponentInChildren<TMP_Text>(true)?.font;
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
            foreach (var p in originals) if (p.Key) { p.Key.shadowCastingMode = p.Value.cast; p.Key.receiveShadows = p.Value.receive; p.Key.sharedMaterials = p.Value.materials; }
            foreach (var m in copies) Release(m);
            Release(toolbar); Release(pipeline);
        }
        static void Release(Object value) { if (!value) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
