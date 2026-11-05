using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JC.Indicators
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    // JC | 2026-11-04 | 탐사 영역의 채움과 테두리를 부모 조절기에서 함께 관리해 하위 표시 설정이 저장값을 덮지 않게 합니다.
    public sealed class JcTriggerAreaIndicator : MonoBehaviour, IInteractionCellOverlayVisual
    {
        [SerializeField, Tooltip("현재 씬의 기존 셀 수집·색상·겹침·호버 판정을 제공하는 컨트롤러입니다.")]
        private InteractionCellOverlayController source;
        [SerializeField, Tooltip("메시와 렌더러를 재사용하는 기존 전투 테두리 프리팹입니다. 원본 프리팹과 프로필은 수정하지 않습니다.")]
        private BattleCellBorderVisual borderPrefab;
        [SerializeField, Tooltip("빌드에 직접 포함할 기존 전투 테두리 셰이더입니다.")] private Shader borderShader;
        [SerializeField, Tooltip("탐사 전용 저장 프로필입니다. 같은 프로필을 참조하는 탐사씬에 저장값이 적용됩니다.")]
        private JcTriggerAreaProfile profile;
        [SerializeField, Tooltip("현재 실시간 조절용 초안입니다. 저장 버튼 전에는 프로필을 덮어쓰지 않습니다.")]
        private JcTriggerAreaSettings settings = new JcTriggerAreaSettings();

        private readonly Dictionary<RegionKey, View> views = new Dictionary<RegionKey, View>();
        private readonly Dictionary<RegionKey, List<InteractionOverlayCell>> groups = new Dictionary<RegionKey, List<InteractionOverlayCell>>();
        private readonly List<RegionKey> removed = new List<RegionKey>();
        private Material material, depthMaterial, fillMaterial, outlineMaterial;
        private InteractionCellOverlayController bound;
        private bool geometryDirty = true;
        // JC | 2026-11-04 | 실제 도로 메시로 영역 전체 높이를 고정하고 가림 표면 분류를 재사용합니다.
        private readonly JcTriggerAreaSurfaces surfaces = new JcTriggerAreaSurfaces();

        public JcTriggerAreaProfile Profile => profile;
        public JcTriggerAreaSettings Settings => settings;
        public int RegionCount => views.Count;
        public bool IsReady => source && source.gameObject.scene == gameObject.scene && borderPrefab && borderShader && profile;
        public void Configure(InteractionCellOverlayController overlay, BattleCellBorderVisual prefab, Shader shader, JcTriggerAreaProfile preset)
        { Unbind(); source = overlay; borderPrefab = prefab; borderShader = shader; profile = preset; LoadProfile(); Bind(); }
        public void LoadProfile()
        {
            if (profile) settings = profile.settings.Copy();
            geometryDirty = true;
            if (source) source.RequestRefresh();
        }
        private void Awake() => LoadProfile();
        private void OnEnable() => Bind();
        private void OnValidate() { geometryDirty = true; if (source) source.RequestRefresh(); }
        private void Bind()
        {
            if (!IsReady || !isActiveAndEnabled) return;
            if (source.VisualOverride != null && source.VisualOverride != this) return;
            if (!material) material = new Material(borderShader) { name = "탐사 트리거 테두리 (임시)", hideFlags = HideFlags.HideAndDontSave, renderQueue = JcIndicatorRenderOrder.TriggerMain };
            material.SetFloat("_ZWrite", 0);
            // JC | 2026-11-04 | 색상 패스에서 장면 깊이와 종류별 마스크를 비교해 계단·건물·인도를 다르게 합성합니다.
            material.SetFloat("_ZTest", (float)CompareFunction.Always);
            if (!outlineMaterial)
            {
                outlineMaterial = new Material(material) { name = "탐사 트리거 보조선 (임시)", hideFlags = HideFlags.HideAndDontSave, renderQueue = JcIndicatorRenderOrder.TriggerOutline };
            }
            if (!fillMaterial)
            {
                fillMaterial = new Material(borderShader) { name = "탐사 트리거 채움 (임시)", hideFlags = HideFlags.HideAndDontSave };
                fillMaterial.SetFloat("_ZWrite", 0);
                // JC | 2026-11-04 | 탐사 채움도 전용 가림 비교로 인도에만 약하게 투과합니다.
                fillMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
                fillMaterial.renderQueue = JcIndicatorRenderOrder.TriggerFill;
            }
            if (!depthMaterial)
            {
                depthMaterial = new Material(borderShader) { name = "탐사 테두리 내부 깊이 (임시)", hideFlags = HideFlags.HideAndDontSave };
            }
            source.VisualOverride = this; bound = source;
            JcTriggerAreaDepth.Register(this);
        }
        private void Unbind()
        {
            JcTriggerAreaDepth.Unregister(this);
            if (bound && bound.VisualOverride == this) bound.VisualOverride = null;
            bound = null; ClearCells();
        }
        private void OnDisable() { Unbind(); Dispose(material); Dispose(depthMaterial); Dispose(fillMaterial); Dispose(outlineMaterial); material = depthMaterial = fillMaterial = outlineMaterial = null; }
        // JC | 2026-11-05 | 이동 코루틴의 위치·상태 갱신 이후에 숨김을 적용해 이동 시작 프레임에도 윤곽선이 남지 않게 합니다.
        private void LateUpdate()
        {
            if (bound != source || !bound || bound.VisualOverride != this)
            { Unbind(); Bind(); }
            if (!bound) return;
            // The existing visual rebuilds only when its shape changes; movement only changes the transform.
            foreach (var view in views.Values)
            {
                if (view.arnold && view.arnold.IsMoving) view.hideBordersUntilRefresh = true;
                else if (view.hideBordersUntilRefresh) source.RequestRefresh();
                // 정지 직후에는 셀 갱신이 끝날 때까지 숨김을 유지해 이전 위치의 선이 잠깐 나타나는 것을 막습니다.
                Paint(view);
            }
        }
        public void RenderCells(IReadOnlyList<InteractionOverlayCell> cells, GridManager grid)
        {
            surfaces.Refresh(gameObject.scene, grid);
            foreach (var list in groups.Values) list.Clear();
            foreach (var cell in cells)
            {
                var key = new RegionKey(cell.Owner, cell.Color);
                if (!groups.TryGetValue(key, out var list)) groups.Add(key, list = new List<InteractionOverlayCell>());
                list.Add(cell);
            }
            removed.Clear();
            foreach (var pair in views)
                if (!groups.TryGetValue(pair.Key, out var list) || list.Count == 0) removed.Add(pair.Key);
            foreach (var key in removed) { Dispose(views[key].visual.gameObject); views.Remove(key); }
            foreach (var pair in groups)
            {
                if (pair.Value.Count == 0) continue;
                if (!views.TryGetValue(pair.Key, out var view))
                {
                    var visual = Instantiate(borderPrefab, transform);
                    visual.gameObject.name = "영역 테두리 · " + (pair.Key.owner as UnityEngine.Object)?.name;
                    visual.gameObject.hideFlags = HideFlags.DontSave;
                    visual.profile = null; visual.SetDriven(true);
                    visual.transform.localScale = Vector3.one;
                    visual.transform.rotation = Quaternion.identity;
                    foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = false; renderer.allowOcclusionWhenDynamic = false;
                    }
                    if (visual.interior) visual.interior.sharedMaterial = fillMaterial;
                    if (visual.outerBorder) visual.outerBorder.sharedMaterial = outlineMaterial;
                    if (visual.innerOutline) visual.innerOutline.sharedMaterial = outlineMaterial;
                    view = new View { visual = visual, color = pair.Key.color };
                    // JC | 2026-11-05 | 기존 이동 효과음과 같은 아놀드 그룹 식별자를 사용하며 다른 이동형 적의 표시는 구분합니다.
                    var enemy = pair.Key.owner as EnemyGridMover;
                    var identity = enemy ? enemy.GetComponent<EnemyIdentity>() : null;
                    if (identity && (identity.EnemyGroupKey == "BE161" || identity.EnemyGroupKey == "BE261")) view.arnold = enemy;
                    int ownerId = pair.Key.owner is UnityEngine.Object owner ? owner.GetInstanceID() : pair.Key.GetHashCode();
                    view.borders = new[] {
                        new JcTriggerAreaDepth.Border(visual.innerBorder, depthMaterial, ownerId, 0),
                        new JcTriggerAreaDepth.Border(visual.outerBorder, depthMaterial, ownerId, 1),
                        new JcTriggerAreaDepth.Border(visual.innerOutline, depthMaterial, ownerId, 2)
                    };
                    views.Add(pair.Key, view);
                }
                // JC | 2026-11-04 | 인도 셀 높이 차이를 제거하고 도로에 접한 모든 셀을 같은 평면에 둡니다.
                float ground = surfaces.GroundHeight(pair.Value);
                bool changed = view.ground != ground || geometryDirty || view.region == null || view.region.CellSize != grid.CellSize || view.region.Cells.Count != pair.Value.Count;
                if (!changed)
                    foreach (var cell in pair.Value)
                        if (!view.region.Cells.TryGetValue(cell.Grid - view.anchor, out float y) || y != ground) { changed = true; break; }
                if (changed)
                {
                    view.ground = ground;
                    view.anchor = pair.Value[0].Grid;
                    view.position = grid.GridToWorldCenter(view.anchor);
                    view.position.y = 0;
                    view.region = new BattleCellBorderMesh.Region { CellSize = grid.CellSize };
                    foreach (var cell in pair.Value) view.region.Cells[cell.Grid - view.anchor] = ground;
                    view.region.Trace(); view.visual.SetRegion(view.region);
                }
                // JC | 2026-11-05 | 최종 셀로 메시를 갱신한 뒤에만 정지한 아놀드의 윤곽선을 다시 표시합니다.
                view.hideBordersUntilRefresh = view.arnold && view.arnold.IsMoving;
                foreach (var border in view.borders) border.priority = pair.Value[0].Priority;
                Paint(view);
            }
            geometryDirty = false;
            // Remove old owner keys as events despawn rather than retaining them for the scene lifetime.
            removed.Clear();
            foreach (var pair in groups) if (pair.Value.Count == 0) removed.Add(pair.Key);
            foreach (var key in removed) groups.Remove(key);
        }
        private void Paint(View view)
        {
            var config = view.config;
            var style = config.currentTurn;
            var shape = settings.shape;
            float cell = view.region.CellSize;
            shape.size = Vector2.one * cell; // Width budget is based on a single cell, never on the region bounds.
            shape.padding = Mathf.Clamp(shape.padding, 0, cell * .1f);
            float budget = cell * .2f;
            float total = Mathf.Max(.001f, shape.mainWidth) + (settings.showOuter ? Mathf.Max(.001f, shape.outerWidth) : 0) + (settings.showInner ? Mathf.Max(.001f, shape.innerWidth) : 0);
            float scale = Mathf.Min(1, budget / total);
            shape.mainWidth = Mathf.Max(.001f, shape.mainWidth) * scale;
            shape.outerWidth = Mathf.Max(.001f, shape.outerWidth) * scale;
            shape.innerWidth = Mathf.Max(.001f, shape.innerWidth) * scale;
            float amplitude = Mathf.Max(0, settings.bobAmplitude);
            shape.floatHeight = Mathf.Max(.001f, settings.minimumHeight) + amplitude;
            style.shape = shape; style.showOuter = settings.showOuter; style.showInner = settings.showInner;
            style.mainColor = Tint(view.color, settings.intensity, settings.opacity);
            style.allowSweep = false;
            style.useFill = settings.useFill;
            style.fillColor = Tint(settings.fillUsesTriggerColor ? view.color : settings.fillColor, settings.fillIntensity, 1);
            style.fillOpacity = Mathf.Clamp01(settings.fillOpacity); style.fillTexture = settings.fillTexture;
            config.bobAmplitude = amplitude; config.bobPeriod = Mathf.Max(.1f, settings.bobPeriod); config.useSweep = false;
            float lift = Mathf.Max(settings.minimumHeight, view.motion.Evaluate(BattleCellVisualState.CurrentTurn, config, Time.unscaledTimeAsDouble));
            view.visual.transform.position = view.position + Vector3.up * lift;
            view.visual.RenderRegion(config, Time.unscaledTime,
                Tint(view.color, settings.outerIntensity, settings.outerOpacity),
                Tint(view.color, settings.innerIntensity, settings.innerOpacity),
                settings.buildingOccludedOpacity, settings.sidewalkOccludedOpacity, settings.fillSidewalkOccludedOpacity);
            // JC | 2026-11-05 | 이동 중에는 윤곽선과 채움을 함께 숨기고, 정지 후 새 셀 갱신이 끝나면 같은 프레임에 복원합니다.
            // 채움의 enabled 설정은 프로필이 관리하고, 이동 중 임시 숨김만 별도로 적용합니다.
            foreach (var border in view.borders)
                if (border.renderer) border.renderer.forceRenderingOff = view.hideBordersUntilRefresh;
            if (view.visual.interior) view.visual.interior.forceRenderingOff = view.hideBordersUntilRefresh;
        }
        internal void CollectBorders(List<JcTriggerAreaDepth.Border> borders, Camera camera)
        {
            if (!isActiveAndEnabled || !bound || !depthMaterial) return;
            if (camera.scene.IsValid() && camera.scene != gameObject.scene) return;
            // JC | 2026-11-05 | 실제로 표시하는 선만 공용 겹침 판정에 포함해 숨겨진 선이 다른 영역을 가리지 않게 합니다.
            foreach (var view in views.Values)
            {
                foreach (var border in view.borders)
                {
                    var renderer = border.renderer;
                    if (renderer && renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff &&
                        (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0) borders.Add(border);
                }
            }
        }
        // JC | 2026-11-04 | 별도 버퍼에만 가림 표면을 그려 이동 표시·말풍선의 장면 깊이는 보존합니다.
        internal void DrawOccluders(CommandBuffer command, Camera camera, MaterialPropertyBlock properties)
        {
            if (!bound || !depthMaterial || (camera.scene.IsValid() && camera.scene != gameObject.scene)) return;
            surfaces.Draw(command, camera, depthMaterial, properties);
        }
        private static Color Tint(Color color, float intensity, float opacity)
        { color *= Mathf.Max(0, intensity); color.a = Mathf.Clamp01(opacity); return color; }
        public void ClearCells()
        {
            foreach (var view in views.Values) if (view.visual) Dispose(view.visual.gameObject);
            views.Clear(); groups.Clear();
        }
        private static void Dispose(UnityEngine.Object value)
        { if (!value) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private readonly struct RegionKey : IEquatable<RegionKey>
        {
            public readonly object owner; public readonly Color color;
            public RegionKey(object source, Color tint) { owner = source; color = tint; }
            public bool Equals(RegionKey other) => ReferenceEquals(owner, other.owner) && color.Equals(other.color);
            public override bool Equals(object other) => other is RegionKey key && Equals(key);
            public override int GetHashCode() => (owner != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(owner) : 0) * 397 ^ color.GetHashCode();
        }
        private sealed class View
        {
            internal BattleCellBorderVisual visual;
            internal BattleCellBorderMesh.Region region;
            internal Vector2Int anchor;
            internal Vector3 position;
            internal float ground;
            internal Color color;
            // JC | 2026-11-05 | 이동 종료 후 셀 갱신까지의 숨김과 각 윤곽선의 고정 정렬 정보를 보관합니다.
            internal EnemyGridMover arnold;
            internal bool hideBordersUntilRefresh;
            internal JcTriggerAreaDepth.Border[] borders;
            internal readonly BattleCellBorderConfiguration config = new BattleCellBorderConfiguration();
            internal readonly BattleCellBorderMotion motion = new BattleCellBorderMotion();
        }
    }
}
