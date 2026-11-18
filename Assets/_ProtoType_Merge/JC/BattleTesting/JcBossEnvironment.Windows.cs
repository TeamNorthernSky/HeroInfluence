using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace JC.BattleTesting
{
    public sealed partial class JcBossEnvironment
    {
        // JC | 2026-11-05 | URP가 이미 전달하는 조명 마스크로 보스 조명을 식별합니다. 공용 URP 에셋/물리 레이어 설정은 변경하지 않습니다.
        public const uint LocalLightMask = 128;
        // JC | 2026-11-18 | 기존 실내 그룹에 식별 비트만 더해 조준 스팟을 GPU에서 구별합니다. 그림자 그룹은 128로 고정 보존합니다.
        public const uint BossSpotMarker = 64;
        // JC | 2026-11-18 | 조명 식별(128/64)과 별개인 그림자 투사 그룹입니다. 보스 전용 파이프라인에서만 사용합니다.
        const uint InteriorCasterMask = 32;
        readonly Dictionary<Renderer, uint> originalShadowLayers = new Dictionary<Renderer, uint>();
        readonly HashSet<Renderer> exteriorShadowRenderers = new HashSet<Renderer>();
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
        // JC | 2026-11-16 | 모든 도시 복사본을 실내 IBL/창문광 대상에서 제외합니다. 이름 검색으로 첫 도시만 제외하지 않습니다.
        internal bool IsInterior(Renderer renderer) => renderer && renderer.transform.IsChildOf(Stage) &&
            !renderer.GetComponentInParent<JcBossCityTime>();

        // JC | 2026-11-18 | 실제 홀의 외곽 계층만 구분합니다. 책상/바닥 및 원경은 이름 부분 일치로 제외하지 않습니다.
        void ApplyShadowLayers()
        {
            exteriorShadowRenderers.Clear();
            foreach (var path in new[] { "FinalStage/Pillar", "보스 홀 · 측후면 연속 창호", "JC_WindowFrames" })
            {
                var root = Stage.Find(path);
                if (root) foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) exteriorShadowRenderers.Add(renderer);
            }
            foreach (var renderer in Stage.GetComponentsInChildren<Renderer>(true))
                if (IsInterior(renderer)) SetShadowLayers(renderer);
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var actor in root.GetComponentsInChildren<BattleCharactor>(true)) RegisterActorShadowLayers(actor);
        }

        // JC | 2026-11-18 | 전투 참가자 등록 때 뒤늦게 생성된 율리아/증폭기/아군 메시도 같은 그룹에 포함합니다.
        void RegisterActorShadowLayers(BattleCharactor actor)
        {
            if (!actor || actor.gameObject.scene != gameObject.scene) return;
            foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true)) SetShadowLayers(renderer);
        }

        void SetShadowLayers(Renderer renderer)
        {
            if (!originalShadowLayers.TryGetValue(renderer, out uint original))
                originalShadowLayers.Add(renderer, original = renderer.renderingLayerMask);
            // JC | 2026-11-18 | 원래 수광 비트는 보존합니다. 외곽도 일반 실내광의 그림자(128)는 계속 만들고 전용 주광(32)에서만 제외됩니다.
            renderer.renderingLayerMask = (original | LocalLightMask | InteriorCasterMask) &
                (exteriorShadowRenderers.Contains(renderer) ? ~InteriorCasterMask : uint.MaxValue);
        }

        void RestoreShadowLayers()
        {
            // JC | 2026-11-18 | 편집 미리보기 종료/씬 저장 준비/전투 종료 시 원본 렌더 레이어를 복구합니다.
            foreach (var pair in originalShadowLayers) if (pair.Key) pair.Key.renderingLayerMask = pair.Value;
            originalShadowLayers.Clear(); exteriorShadowRenderers.Clear();
        }

        void ConfigureLocalLight(Light light, int mask)
        {
            var data = light.GetUniversalAdditionalLightData();
            data.renderingLayers = LocalLightMask;
            // JC | 2026-11-17 | 파이프라인의 공통 바이어스 대신 인스펙터/프로필에 지정한 그림자 오차 보정을 사용합니다.
            data.usePipelineSettings = false;
            // JC | 2026-11-05 | 원경의 표시 오브젝트만 분리해 사용자가 광원 범위를 늘려도 도시가 실내 조명에 물들지 않게 합니다.
            // 게임 유닛/충돌 레이어는 변경하지 않으며 원경 레이어도 종료 시 복원합니다.
            light.cullingMask = mask & ~(1 << CityVisualLayer);
        }
        void ApplyCityLayers()
        {
            // JC | 2026-11-16 | 도시별 편집 결과와 복사본 모두 동일한 실내광 격리 레이어를 사용합니다.
            foreach (var city in JcBossCityTime.Roots(Stage))
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
                // JC | 2026-11-17 | 창 경계 바깥으로 번지는 빛이 스폿 원뿔/텍스처 끝에서 잘리지 않도록 여백을 확보합니다.
                float softness = Mathf.Clamp(spec.softness, .001f, .2f);
                float padding = 1 + 4 * softness;
                tangent *= 1.04f * padding; light.spotAngle = Mathf.Atan(tangent) * 2 * Mathf.Rad2Deg; light.innerSpotAngle = light.spotAngle;
                for (int i = 0; i < 4; i++) corners[i] = corners[i] / (2 * tangent) + Vector2.one * .5f;
                var cookie = WindowCookie(corners, softness / padding); windowCookies.Add(cookie); light.cookie = cookie;
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
                    // JC | 2026-11-17 | 안쪽만 깎던 마스크를 경계 양쪽으로 풀어 창 사이의 어두운 띠를 완화합니다.
                    // 네 변을 곱해 모서리의 딱딱한 대각선 연결도 부드럽게 연결합니다.
                    float distance = Vector2.Dot(point - a, n);
                    value *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-softness, softness, distance));
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
