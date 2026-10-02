using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JC.Indicators
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("JC Indicators/이동 인디케이터 설정")]
    public sealed class JcMovementIndicatorController : MonoBehaviour
    {
        [SerializeField, Tooltip("저장된 기본값입니다. 초안 조절은 이 에셋을 바꾸지 않으며 캡처·저장 버튼으로 확정합니다.")]
        private JcMovementIndicatorProfile profile;
        [SerializeField, Tooltip("이 씬에서 스타일을 적용할 경로 렌더러입니다. 다른 씬에는 적용하지 않습니다.")]
        private PathPreviewRenderer pathRenderer;
        [SerializeField, Tooltip("기존 클릭 판정을 유지할 도착 표식입니다. 원본 메시만 숨기고 콜라이더는 유지합니다.")]
        private Transform destinationMarker;
        [SerializeField, Tooltip("타일 크기와 높이를 제공하는 현재 씬의 그리드입니다.")]
        private GridManager gridManager;
        [SerializeField, Tooltip("절차적 도형 셰이더입니다. 빌드에서도 포함되도록 직접 참조합니다.")]
        private Shader indicatorShader;
        [SerializeField, Tooltip("실시간 조절용 초안입니다. 프로파일에 캡처하기 전에는 저장된 기본값을 덮어쓰지 않습니다.")]
        private JcMovementIndicatorSettings settings = JcMovementIndicatorSettings.Default;

        private static readonly List<JcMovementIndicatorController> Controllers = new List<JcMovementIndicatorController>();
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<Vector3> rawPoints = new List<Vector3>(), terrainPoints = new List<Vector3>();
        private readonly JcIndicatorTerrainPath terrainPath = new JcIndicatorTerrainPath();
        private readonly JcIndicatorGroundShadow pathProjection = new JcIndicatorGroundShadow(), markerProjection = new JcIndicatorGroundShadow();
        private int rawReachable;
        private Vector3 previousShadowOffset;
        private readonly JcIndicatorPathMesh pathBuilder = new JcIndicatorPathMesh();
        private readonly JcIndicatorMarkerMesh markerBuilder = new JcIndicatorMarkerMesh();
        private JcMovementIndicatorSettings previousMarkerSettings;
        private bool markerGeometryBuilt;
        private MaterialPropertyBlock block;
        private GameObject visualRoot;
        private MeshRenderer pathVisual, pathShadow, pathGlow, markerVisual, markerShadow;
        private Mesh pathMesh, shadowMesh, glowMesh, markerMesh, markerShadowMesh;
        private Material material, shadowMaterial, markerMaterial;
        private Renderer originalMarkerRenderer;
        private PathPreviewRenderer boundPath;
        private Transform boundMarker;
        private bool originalMarkerForceRenderingOff;
        private bool markerReachable = true, hasPath, geometryDirty = true;
        private bool showingGuidance;
        private JcMovementIndicatorController guidanceLayer;
        private Vector3 guidanceDestination;
        private static readonly Color GuidanceColor = new Color(1f, .76f, .025f, 1f);
        private bool ShowDestination => showingGuidance || destinationMarker.gameObject.activeInHierarchy;
        private Vector3 DestinationPosition => showingGuidance ? guidanceDestination : destinationMarker.position;

        // 안내는 선택된 목적지가 아니므로 기존 마커 콜라이더를 켜지 않는다.
        public void SetGuidanceDestination(Vector3 position)
        {
            showingGuidance = true;
            guidanceDestination = position;
        }

        // 같은 메시/지형/셰이더 구현을 재사용하되 별도 PathPreviewRenderer에 연결한다.
        // 기본 이동 미리보기의 Hide/RenderPath 호출이 노란 안내를 지우지 않는다.
        public void RenderGuidance(IReadOnlyList<Vector3> path)
        {
            if (path == null || path.Count < 2) { HideGuidance(); return; }
            if (guidanceLayer == null)
            {
                var host = new GameObject("Zone Guidance Indicator (Generated)") { hideFlags = HideFlags.DontSave };
                host.transform.SetParent(transform, false);
                var guidePath = host.AddComponent<PathPreviewRenderer>();
                var markerHost = new GameObject("Guidance Destination") { hideFlags = HideFlags.DontSave, layer = destinationMarker.gameObject.layer };
                markerHost.transform.SetParent(host.transform, false);
                markerHost.SetActive(false);
                guidanceLayer = host.AddComponent<JcMovementIndicatorController>();
                guidanceLayer.Configure(guidePath, markerHost.transform, gridManager, indicatorShader, null);
            }
            guidanceLayer.Settings = Settings;
            guidanceLayer.RenderPath(path, path.Count - 1);
            guidanceLayer.SetGuidanceDestination(path[path.Count - 1]);
        }

        public void HideGuidance()
        {
            if (guidanceLayer != null) guidanceLayer.HidePath();
            if (showingGuidance) HidePath();
        }
        private int reachableSegments;
        private float previousWidth = -1, previousSoftness = -1;
        private bool startedInPlay;
        private float pathLength, flickerFrontRemaining, flickerPreviousClock;
        private int flickerDirection;
        private bool flickerInitialized;
        private JcMovementIndicatorSettings previousPathSettings;
        private float previousMarkerSize = -1, previousVisualClock = float.NaN;
        private float previousPulseFront = float.NaN;
        private bool previousMarkerActive;
        private Vector3 previousMarkerPosition;

        public JcMovementIndicatorProfile Profile { get => profile; set => profile = value; }
        public JcMovementIndicatorSettings ResolveSettings(JcMovementIndicatorSettings value) => value.ForCellSize(gridManager != null ? gridManager.CellSize : 1);
        public JcMovementIndicatorSettings Settings { get => ResolveSettings(settings); set { settings = ResolveSettings(value); geometryDirty = true; } }
        public bool IsReady => isActiveAndEnabled && indicatorShader != null && pathRenderer != null
            && destinationMarker != null && gridManager != null
            && pathRenderer.gameObject.scene == gameObject.scene && destinationMarker.gameObject.scene == gameObject.scene;

        public void Configure(PathPreviewRenderer path, Transform marker, GridManager grid, Shader shader, JcMovementIndicatorProfile preset)
        {
            Unbind();
            pathRenderer = path; destinationMarker = marker; gridManager = grid; indicatorShader = shader; profile = preset;
            LoadProfile(); Bind();
        }
        public void LoadProfile() { if (profile != null) Settings = profile.settings; }
        private void OnEnable() { if (!Controllers.Contains(this)) Controllers.Add(this); Bind(); }
        private void OnValidate() { settings = gridManager != null ? settings.ForCellSize(gridManager.CellSize) : settings.Sanitized(); geometryDirty = true; }
        private void OnDisable() { Controllers.Remove(this); Unbind(); DisposeVisuals(); startedInPlay = false; }
        private void OnDestroy() { Controllers.Remove(this); Unbind(); DisposeVisuals(); }

        private void Bind()
        {
            if (boundPath != pathRenderer || boundMarker != destinationMarker) Unbind();
            if (!IsReady) return;
            if (pathRenderer.VisualOverride != null && pathRenderer.VisualOverride != this) return;
            pathRenderer.VisualOverride = this;
            boundPath = pathRenderer;
            boundMarker = destinationMarker;
        }
        private void Unbind()
        {
            if (boundPath != null && boundPath.VisualOverride == this) boundPath.VisualOverride = null;
            if (originalMarkerRenderer != null) originalMarkerRenderer.forceRenderingOff = originalMarkerForceRenderingOff;
            originalMarkerRenderer = null;
            boundPath = null; boundMarker = null; flickerInitialized = false;
        }
        public static bool TryGetForMarker(Transform marker, out JcMovementIndicatorController controller)
        {
            for (int i = Controllers.Count - 1; i >= 0; i--)
            {
                var candidate = Controllers[i];
                if (candidate == null) { Controllers.RemoveAt(i); continue; }
                if (candidate.IsReady && candidate.destinationMarker == marker && candidate.pathRenderer.VisualOverride == candidate)
                { controller = candidate; return true; }
            }
            controller = null; return false;
        }
        public void SetDestinationState(bool reachable) => markerReachable = reachable;
        public void RenderPath(IReadOnlyList<Vector3> path, int reachable)
        {
            showingGuidance = false;
            int newReachable = Mathf.Clamp(reachable, 0, Mathf.Max(0, path.Count - 1));
            bool changed = rawPoints.Count != path.Count || rawReachable != newReachable;
            if (!changed) for (int i = 0; i < path.Count; i++) if (rawPoints[i] != path[i]) { changed = true; break; }
            bool sameDestination = hasPath && path.Count >= 2 && rawPoints.Count >= 2
                && (path[path.Count - 1] - rawPoints[rawPoints.Count - 1]).sqrMagnitude < .000001f;
            if (!sameDestination) flickerInitialized = false;
            rawPoints.Clear();
            for (int i = 0; i < path.Count; i++)
            {
                rawPoints.Add(path[i]);
            }
            rawReachable = newReachable;
            hasPath = rawPoints.Count >= 2; geometryDirty |= changed;
        }
        public void HidePath()
        {
            showingGuidance = false;
            hasPath = false; flickerInitialized = false;
            if (pathVisual != null) pathVisual.enabled = false;
            if (pathShadow != null) pathShadow.enabled = false;
            if (pathGlow != null) pathGlow.enabled = false;
        }
        private void LateUpdate()
        {
            if (!IsReady) { Unbind(); DisposeVisuals(); return; }
            Bind();
            if (pathRenderer.VisualOverride != this) return;
            if (Application.IsPlaying(gameObject) && !startedInPlay) { LoadProfile(); startedInPlay = true; }
            if (!Application.IsPlaying(gameObject)) startedInPlay = false;
            UpdateVisuals(Time.time);
        }

        // 에디터 비플레이 렌더 검증에도 사용한다. 게임 입력이나 턴 진행은 변경하지 않는다.
        public void UpdateVisuals(float clock)
        {
            if (!IsReady) return;
            EnsureVisuals();
            if (originalMarkerRenderer == null)
            {
                originalMarkerRenderer = destinationMarker.GetComponent<Renderer>();
                if (originalMarkerRenderer != null) originalMarkerForceRenderingOff = originalMarkerRenderer.forceRenderingOff;
            }
            if (originalMarkerRenderer != null) originalMarkerRenderer.forceRenderingOff = true;
            var s = Settings;
            // 등록된 셀 표면만 사용하므로 건물이나 캐릭터를 높이로 잘못 인식하지 않는다.
            if (hasPath)
            {
                float radius = s.lineWidth + (s.dashGlowStrength > 0 ? 2 * s.dashGlowWidth : 0);
                pathLength = terrainPath.Build(rawPoints, rawReachable, gridManager, s.terrainRampLength,
                    radius, terrainPoints, out int terrainReachable);
                bool changed = points.Count != terrainPoints.Count || reachableSegments != terrainReachable;
                if (!changed) for (int i = 0; i < points.Count; i++)
                    if (points[i] != terrainPoints[i]) { changed = true; break; }
                if (changed) { points.Clear(); points.AddRange(terrainPoints); geometryDirty = true; }
                reachableSegments = terrainReachable;
            }
            float size = gridManager.CellSize * s.markerSize;
            if (!markerGeometryBuilt || !SameMarkerGeometry(s, previousMarkerSettings) || previousMarkerSize != size)
            {
                markerBuilder.Build(markerMesh, s, size);
                previousMarkerSettings = s; markerGeometryBuilt = true;
            }
            float lift = s.floatHeight + s.bobAmplitude * Mathf.Sin(clock * s.bobFrequency * Mathf.PI * 2);
            Vector3 shadowOffset = new Vector3(Mathf.Cos(s.shadowAngle * Mathf.Deg2Rad), 0, Mathf.Sin(s.shadowAngle * Mathf.Deg2Rad)) * s.shadowDistance;
            bool showPath = hasPath && pathLength > .00001f && pathRenderer.isActiveAndEnabled;
            UpdateFlicker(s, clock, showPath);
            var pulse = new JcIndicatorPulse(s, flickerFrontRemaining, flickerDirection, flickerInitialized && showPath);
            bool showMarker = ShowDestination;
            Vector3 basePosition = DestinationPosition;
            basePosition.y = JcIndicatorTerrainPath.SurfaceHeight(gridManager, basePosition, size * .5f - .0001f)
                + JcIndicatorTerrainPath.Clearance;
            bool rebuildShadow = geometryDirty || previousWidth != s.lineWidth || previousSoftness != s.shadowSoftness
                || previousShadowOffset != shadowOffset || pathProjection.TerrainChanged(gridManager);
            bool rebuildMarkerShadow = previousSoftness != s.shadowSoftness || previousShadowOffset != shadowOffset
                || size != previousMarkerSize || basePosition != previousMarkerPosition || !markerGeometryBuilt
                || markerShadowMesh.vertexCount == 0 || markerProjection.TerrainChanged(gridManager);
            pathShadow.transform.position = shadowOffset;
            markerShadow.transform.position = basePosition + shadowOffset;
            markerShadow.transform.localScale = Vector3.one * size;
            if (rebuildShadow)
            {
                pathBuilder.Build(shadowMesh, points, reachableSegments, s.lineWidth * .5f + s.shadowSoftness + .006f);
                pathProjection.Project(shadowMesh, pathShadow.transform.localToWorldMatrix, gridManager);
                previousWidth = s.lineWidth; previousSoftness = s.shadowSoftness;
            }
            bool moving = clock != previousVisualClock && s.flowSpeed > 0;
            if (geometryDirty || moving || !SamePathGeometry(s, previousPathSettings)
                || showMarker != previousMarkerActive || basePosition != previousMarkerPosition || size != previousMarkerSize)
                pathBuilder.BuildSolid(pathMesh, glowMesh, points, reachableSegments, s, clock, pulse, basePosition, size, showMarker);
            else if (s.flickerLiftHeight > 0 && previousPulseFront != flickerFrontRemaining)
                pathBuilder.UpdateLift(pathMesh, glowMesh, pulse, s.flickerLiftHeight);
            previousPathSettings = s; previousVisualClock = clock; previousPulseFront = flickerFrontRemaining;
            previousMarkerActive = showMarker; previousMarkerPosition = basePosition; previousMarkerSize = size;
            previousShadowOffset = shadowOffset;
            geometryDirty = false;
            pathVisual.enabled = showPath && s.opacity > 0;
            pathShadow.enabled = showPath && s.opacity * s.shadowOpacity > 0;
            pathGlow.enabled = showPath && s.opacity > 0 && s.dashGlowStrength > 0 && s.dashGlowWidth > 0;
            pathVisual.transform.position = Vector3.up * lift;
            pathGlow.transform.position = Vector3.up * lift;

            markerVisual.enabled = showMarker && s.opacity > 0;
            markerShadow.enabled = showMarker && s.opacity * s.shadowOpacity > 0;
            markerVisual.transform.position = basePosition + Vector3.up * (Mathf.Max(0, lift + s.markerHeightOffset) + .003f
                + s.flickerLiftHeight * pulse.Evaluate(0));
            markerVisual.transform.localScale = Vector3.one * size;
            // Quad UV extends beyond the square so even maximum blur has enough geometry.
            float extent = .5f + (s.shadowSoftness + .006f) / size;
            if (rebuildMarkerShadow)
            {
                BuildMarkerShadowMesh(extent);
                markerProjection.Project(markerShadowMesh, markerShadow.transform.localToWorldMatrix, gridManager);
            }
            Apply(pathVisual, s, false, false, 1, clock);
            Apply(pathShadow, s, false, true, 1, clock);
            Apply(pathGlow, s, false, false, 1, clock);
            Apply(markerVisual, s, true, false, size, clock);
            Apply(markerShadow, s, true, true, size, clock);
        }
        private static float FlickerUnit(JcMovementIndicatorSettings s) => Mathf.Max(.015f, s.dashLength + s.dashGap) / .37f;
        private void UpdateFlicker(JcMovementIndicatorSettings s, float clock, bool showPath)
        {
            int direction = s.dashFlickerDirection == JcDashFlickerDirection.TowardStart ? -1 : 1;
            if (!showPath) { flickerInitialized = false; flickerPreviousClock = clock; return; }
            if (!flickerInitialized || direction != flickerDirection || clock < flickerPreviousClock)
            {
                flickerFrontRemaining = direction > 0 ? pathLength : 0;
                flickerPreviousClock = clock; flickerDirection = direction; flickerInitialized = true;
            }
            float unit = FlickerUnit(s);
            float distance = Mathf.Max(0, clock - flickerPreviousClock) * s.dashFlickerSpeed * unit;
            flickerPreviousClock = clock;
            flickerFrontRemaining -= direction * distance;
            // 전환과 복귀가 각 1구간. 마지막 위치의 복귀가 끝난 뒤에만 새 흐름을 시작한다.
            float tail = 2 * unit, cycle = pathLength + tail;
            if (direction > 0 && flickerFrontRemaining <= -tail)
                flickerFrontRemaining = pathLength - Mathf.Repeat(-tail - flickerFrontRemaining, cycle);
            else if (direction < 0 && flickerFrontRemaining >= pathLength + tail)
                flickerFrontRemaining = Mathf.Repeat(flickerFrontRemaining - pathLength - tail, cycle);
        }

        private static bool SameMarkerGeometry(JcMovementIndicatorSettings a, JcMovementIndicatorSettings b) =>
            a.commonThickness == b.commonThickness && a.bevelWidth == b.bevelWidth && a.curveSegments == b.curveSegments
            && a.borderWidth == b.borderWidth && a.cornerRadius == b.cornerRadius && a.ringRadius == b.ringRadius
            && a.ringWidth == b.ringWidth && a.dotRadius == b.dotRadius;

        private static bool SamePathGeometry(JcMovementIndicatorSettings a, JcMovementIndicatorSettings b) =>
            a.commonThickness == b.commonThickness && a.lineWidth == b.lineWidth && a.dashLength == b.dashLength
            && a.dashGap == b.dashGap && a.curveSegments == b.curveSegments && a.cornerRadius == b.cornerRadius
            && a.flowSpeed == b.flowSpeed && a.dashGlowWidth == b.dashGlowWidth && a.dashGlowStrength == b.dashGlowStrength
            && a.flickerLiftHeight == b.flickerLiftHeight && a.dashFlickerRiseSpeed == b.dashFlickerRiseSpeed
            && a.dashFlickerFallSpeed == b.dashFlickerFallSpeed && a.dashFlickerDirection == b.dashFlickerDirection;

        private void Apply(MeshRenderer renderer, JcMovementIndicatorSettings s, bool marker, bool shadow, float size, float clock)
        {
            block.Clear();
            Color color = showingGuidance ? GuidanceColor : (marker && !markerReachable ? s.unreachableColor : s.reachableColor);
            block.SetColor("_Color", shadow ? s.shadowColor : color);
            block.SetColor("_UnreachableColor", showingGuidance ? GuidanceColor : s.unreachableColor);
            block.SetColor("_HighlightColor", showingGuidance ? GuidanceColor : s.highlightColor);
            block.SetFloat("_MarkerColorCycle", s.highlightColorCyclePeriod);
            block.SetVector("_DashGlow", new Vector4(s.dashGlowWidth, s.dashGlowStrength, 0, 0));
            block.SetVector("_FlickerPulse", new Vector4(flickerFrontRemaining, FlickerUnit(s), 0, flickerInitialized ? 1 : 0));
            block.SetVector("_DashFlickerRates", new Vector4(s.dashFlickerRiseSpeed, s.dashFlickerFallSpeed, 0, 0));
            block.SetVector("_DashFlicker", new Vector4(s.dashFlickerStrength, s.dashFlickerSpeed,
                s.dashFlickerDirection == JcDashFlickerDirection.TowardStart ? -1 : 1, 0));
            block.SetFloat("_SideBrightness", s.sideBrightness);
            block.SetVector("_Shape", new Vector4(s.borderWidth, s.cornerRadius, s.ringRadius, s.ringWidth));
            block.SetVector("_Wave", new Vector4(s.waveWidth, s.waveSpeed, s.wavePeriod, s.highlightStrength));
            block.SetVector("_Dash", new Vector4(s.dashLength, s.dashGap, s.lineWidth, s.flowSpeed));
            block.SetFloat("_DotRadius", s.dotRadius);
            block.SetFloat("_Opacity", s.opacity * (shadow ? s.shadowOpacity : 1));
            block.SetFloat("_Mode", marker ? (shadow ? 0 : 2) : (shadow ? 1 : (renderer == pathGlow ? 3 : 4)));
            block.SetFloat("_Shadow", shadow ? 1 : 0);
            block.SetFloat("_Softness", shadow ? s.shadowSoftness / size : 0);
            block.SetFloat("_Clock", clock);
            Vector3 center = DestinationPosition;
            if (shadow) center += new Vector3(Mathf.Cos(s.shadowAngle * Mathf.Deg2Rad), 0, Mathf.Sin(s.shadowAngle * Mathf.Deg2Rad)) * s.shadowDistance;
            block.SetVector("_MarkerClip", new Vector4(center.x, center.z, gridManager.CellSize * s.markerSize, ShowDestination ? 1 : 0));
            renderer.SetPropertyBlock(block);
        }
        private void EnsureVisuals()
        {
            if (block == null) block = new MaterialPropertyBlock();
            if (visualRoot != null) return;
            visualRoot = new GameObject("JC Movement Visuals (Generated)") { hideFlags = HideFlags.DontSave };
            SceneManager.MoveGameObjectToScene(visualRoot, gameObject.scene);
            material = new Material(indicatorShader) { name = "JC Movement Glow (runtime)", hideFlags = HideFlags.DontSave };
            shadowMaterial = new Material(material) { name = "JC Movement Shadow (runtime)", renderQueue = 2999, hideFlags = HideFlags.DontSave };
            markerMaterial = new Material(material) { name = "JC Solid Indicator (runtime)", hideFlags = HideFlags.DontSave };
            markerMaterial.SetFloat("_Cull", (float)CullMode.Back);
            pathMesh = NewMesh("JC Path"); shadowMesh = NewMesh("JC Path Shadow"); markerMesh = NewMesh("JC Marker");
            glowMesh = NewMesh("JC Path Glow");
            markerShadowMesh = NewMesh("JC Marker Shadow");
            BuildMarkerShadowMesh(.6f);
            markerGeometryBuilt = false;
            pathVisual = MakeRenderer("Path", pathMesh, markerMaterial);
            pathShadow = MakeRenderer("Path Shadow", shadowMesh, shadowMaterial);
            pathGlow = MakeRenderer("Path Glow", glowMesh, material);
            markerVisual = MakeRenderer("Destination", markerMesh, markerMaterial);
            markerShadow = MakeRenderer("Destination Shadow", markerShadowMesh, shadowMaterial);
            geometryDirty = true;
        }
        private static Mesh NewMesh(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave }; mesh.MarkDynamic(); return mesh;
        }
        private MeshRenderer MakeRenderer(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = destinationMarker.gameObject.layer };
            go.transform.SetParent(visualRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat; renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false; renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off; renderer.enabled = false;
            return renderer;
        }
        private void BuildMarkerShadowMesh(float extent)
        {
            float e = Mathf.Max(.506f, extent);
            markerShadowMesh.Clear();
            markerShadowMesh.vertices = new[] { new Vector3(-e, 0, -e), new Vector3(-e, 0, e), new Vector3(e, 0, -e), new Vector3(e, 0, e) };
            markerShadowMesh.uv = new[] { new Vector2(-e, -e), new Vector2(-e, e), new Vector2(e, -e), new Vector2(e, e) };
            markerShadowMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            markerShadowMesh.triangles = new[] { 0, 1, 2, 2, 1, 3 }; markerShadowMesh.RecalculateBounds();
        }
        private void DisposeVisuals()
        {
            if (guidanceLayer != null) DestroyOwned(guidanceLayer.gameObject);
            guidanceLayer = null;
            if (showingGuidance) hasPath = false;
            showingGuidance = false;
            DestroyOwned(visualRoot); DestroyOwned(pathMesh); DestroyOwned(shadowMesh); DestroyOwned(glowMesh); DestroyOwned(markerMesh); DestroyOwned(markerShadowMesh);
            DestroyOwned(material); DestroyOwned(shadowMaterial); DestroyOwned(markerMaterial);
            visualRoot = null; pathVisual = pathShadow = pathGlow = markerVisual = markerShadow = null;
            pathMesh = shadowMesh = glowMesh = markerMesh = markerShadowMesh = null; material = shadowMaterial = markerMaterial = null; markerGeometryBuilt = false; flickerInitialized = false;
            previousVisualClock = previousPulseFront = float.NaN; previousMarkerSize = -1;
        }
        private static void DestroyOwned(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
