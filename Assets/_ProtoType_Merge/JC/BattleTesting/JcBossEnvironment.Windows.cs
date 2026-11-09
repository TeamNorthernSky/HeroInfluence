using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace JC.BattleTesting
{
    public sealed partial class JcBossEnvironment
    {
        // JC | 2026-11-05 | URP가 이미 전달하는 조명 마스크로 보스 조명을 식별합니다. 공용 URP 에셋/물리 레이어 설정은 변경하지 않습니다.
        public const uint LocalLightMask = 128;
        const int CityVisualLayer = 30;
        public JcBossBackgroundMaterials SurfaceController { get; set; }
        public bool Isolated => isActiveAndEnabled && Draft && Draft.isolateInterior;
        static readonly HashSet<JcBossEnvironment> environments = new HashSet<JcBossEnvironment>();
        readonly List<Light> windowLights = new List<Light>();
        readonly List<Texture2D> windowCookies = new List<Texture2D>();
        readonly Dictionary<GameObject, int> cityLayers = new Dictionary<GameObject, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEnvironments() => environments.Clear();
        void RegisterLocalLighting() => environments.Add(this);
        public static bool UsesLocalLighting(Renderer renderer)
        {
            foreach (var environment in environments)
                if (environment && environment.Isolated && renderer && environment.gameObject.scene == renderer.gameObject.scene) return true;
            return false;
        }
        internal bool IsInterior(Renderer renderer) => renderer && renderer.transform.IsChildOf(Stage) &&
            !renderer.transform.IsChildOf(Stage.Find("도시 원경 (베이크)"));

        void ConfigureLocalLight(Light light, int mask)
        {
            light.GetUniversalAdditionalLightData().renderingLayers = LocalLightMask;
            // JC | 2026-11-05 | 원경의 표시 오브젝트만 분리해 사용자가 광원 범위를 늘려도 도시가 실내 조명에 물들지 않게 합니다.
            // 게임 유닛/충돌 레이어는 변경하지 않으며 원경 레이어도 종료 시 복원합니다.
            light.cullingMask = mask & ~(1 << CityVisualLayer);
        }
        void ApplyCityLayers()
        {
            var city = Stage.Find("도시 원경 (베이크)");
            if (!city) return;
            foreach (var renderer in city.GetComponentsInChildren<Renderer>(true))
            {
                var go = renderer.gameObject;
                if (!cityLayers.ContainsKey(go)) cityLayers.Add(go, go.layer);
                go.layer = CityVisualLayer;
            }
        }
        void RestoreCityLayers()
        {
            foreach (var pair in cityLayers) if (pair.Key) pair.Key.layer = pair.Value;
            cityLayers.Clear();
        }

        void ApplyWindows()
        {
            ApplyCityLayers();
            foreach (var light in windowLights) if (light) { light.enabled = false; Release(light.gameObject); }
            foreach (var cookie in windowCookies) Release(cookie);
            windowLights.Clear(); windowCookies.Clear();
            if (Draft.windows == null) return;
            foreach (var spec in Draft.windows)
            {
                if (spec == null || !spec.enabled) continue;
                var pane = Stage.Find(spec.rendererPath);
                var filter = pane ? pane.GetComponent<MeshFilter>() : null;
                if (!filter || !filter.sharedMesh) continue;
                var bounds = filter.sharedMesh.bounds;
                int thin = bounds.size.x < bounds.size.z ? 0 : 2;
                var localNormal = thin == 0 ? Vector3.right : Vector3.forward;
                var normal = pane.TransformDirection(localNormal).normalized;
                var center = pane.TransformPoint(bounds.center);
                if (Vector3.Dot(normal, Stage.position - center) < 0) normal = -normal;
                var rotation = Quaternion.LookRotation(normal, Stage.up) * Quaternion.Euler(spec.pitch, spec.yaw, 0);
                var go = new GameObject(spec.label) { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
                go.transform.SetParent(lightRoot, false);
                go.transform.SetPositionAndRotation(center - rotation * Vector3.forward * Mathf.Max(1, spec.standOff), rotation);
                var light = go.AddComponent<Light>(); windowLights.Add(light);
                light.type = LightType.Spot; light.color = spec.color; light.intensity = Mathf.Max(0, spec.intensity);
                light.range = Mathf.Max(1, spec.range); light.shadows = spec.shadows; light.shadowStrength = spec.shadowStrength;
                light.shadowBias = .015f; light.shadowNormalBias = .05f;
                ConfigureLocalLight(light, -1);
                // JC | 2026-11-05 | 실제 창문 네 모서리를 광원 시점으로 투영하여 비스듬한 창문에도 마스크가 맞도록 합니다.
                var corners = new Vector2[4]; float tangent = .01f;
                int wide = thin == 0 ? 2 : 0;
                for (int i = 0; i < 4; i++)
                {
                    var local = bounds.center;
                    local[wide] += bounds.extents[wide] * (i == 0 || i == 3 ? -1 : 1);
                    local.y += bounds.extents.y * (i < 2 ? -1 : 1);
                    var ray = go.transform.InverseTransformPoint(pane.TransformPoint(local));
                    corners[i] = new Vector2(ray.x, ray.y) / Mathf.Max(.1f, ray.z);
                    tangent = Mathf.Max(tangent, corners[i].magnitude);
                }
                tangent *= 1.04f; light.spotAngle = Mathf.Atan(tangent) * 2 * Mathf.Rad2Deg; light.innerSpotAngle = light.spotAngle;
                for (int i = 0; i < 4; i++) corners[i] = corners[i] / (2 * tangent) + Vector2.one * .5f;
                var cookie = WindowCookie(corners, spec.softness); windowCookies.Add(cookie); light.cookie = cookie;
            }
        }
        static Texture2D WindowCookie(Vector2[] corners, float softness)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true) {
                name = "창문 투사 마스크", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var center = (corners[0] + corners[1] + corners[2] + corners[3]) * .25f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var point = new Vector2((x + .5f) / size, (y + .5f) / size); float value = 1;
                for (int edge = 0; edge < 4; edge++)
                {
                    var a = corners[edge]; var v = corners[(edge + 1) % 4] - a;
                    var n = new Vector2(-v.y, v.x).normalized;
                    if (Vector2.Dot(center - a, n) < 0) n = -n;
                    value = Mathf.Min(value, Mathf.SmoothStep(0, 1, Mathf.Clamp01(Vector2.Dot(point - a, n) / Mathf.Max(.001f, softness))));
                }
                byte b = (byte)Mathf.RoundToInt(value * 255); pixels[y * size + x] = new Color32(b,b,b,b);
            }
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }
        void UnregisterLocalLighting()
        {
            environments.Remove(this); RestoreCityLayers();
            foreach (var cookie in windowCookies) Release(cookie);
            windowCookies.Clear();
        }
    }
}
