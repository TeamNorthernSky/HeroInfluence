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
        private readonly JcIndicatorPathMesh pathBuilder = new JcIndicatorPathMesh();
        private MaterialPropertyBlock block;
        private GameObject visualRoot;
        private MeshRenderer pathVisual, pathShadow, markerVisual, markerShadow;
        private Mesh pathMesh, shadowMesh, markerMesh;
        private Material material, shadowMaterial;
        private Renderer originalMarkerRenderer;
        private PathPreviewRenderer boundPath;
        private Transform boundMarker;
        private bool originalMarkerForceRenderingOff;
        private bool markerReachable = true, hasPath, geometryDirty = true;
        private int reachableSegments;
        private float previousWidth = -1, previousSoftness = -1;
        private bool startedInPlay;

        public JcMovementIndicatorProfile Profile { get => profile; set => profile = value; }
        public JcMovementIndicatorSettings Settings { get => settings.Sanitized(); set { settings = value.Sanitized(); geometryDirty = true; } }
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
        private void OnValidate() { settings = settings.Sanitized(); geometryDirty = true; }
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
            boundPath = null; boundMarker = null;
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
            points.Clear();
            for (int i = 0; i < path.Count; i++) points.Add(path[i]);
            reachableSegments = Mathf.Clamp(reachable, 0, Mathf.Max(0, points.Count - 1));
            hasPath = points.Count >= 2; geometryDirty = true;
        }
        public void HidePath()
        {
            hasPath = false;
            if (pathVisual != null) pathVisual.enabled = false;
            if (pathShadow != null) pathShadow.enabled = false;
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
            float lift = s.floatHeight + s.bobAmplitude * Mathf.Sin(clock * s.bobFrequency * Mathf.PI * 2);
            Vector3 shadowOffset = new Vector3(Mathf.Cos(s.shadowAngle * Mathf.Deg2Rad), 0, Mathf.Sin(s.shadowAngle * Mathf.Deg2Rad)) * s.shadowDistance;
            if (geometryDirty || previousWidth != s.lineWidth || previousSoftness != s.shadowSoftness)
            {
                pathBuilder.Build(pathMesh, points, reachableSegments, s.lineWidth * .5f + .006f);
                pathBuilder.Build(shadowMesh, points, reachableSegments, s.lineWidth * .5f + s.shadowSoftness + .006f);
                previousWidth = s.lineWidth; previousSoftness = s.shadowSoftness; geometryDirty = false;
            }
            bool showPath = hasPath && pathRenderer.isActiveAndEnabled;
            pathVisual.enabled = showPath && s.opacity > 0;
            pathShadow.enabled = showPath && s.opacity * s.shadowOpacity > 0;
            pathVisual.transform.position = Vector3.up * lift;
            pathShadow.transform.position = shadowOffset - Vector3.up * .005f;

            bool showMarker = destinationMarker.gameObject.activeInHierarchy;
            markerVisual.enabled = showMarker && s.opacity > 0;
            markerShadow.enabled = showMarker && s.opacity * s.shadowOpacity > 0;
            float size = gridManager.CellSize * s.markerSize;
            Vector3 basePosition = destinationMarker.position;
            markerVisual.transform.position = basePosition + Vector3.up * (lift + .003f);
            markerShadow.transform.position = basePosition + shadowOffset - Vector3.up * .005f;
            markerVisual.transform.localScale = Vector3.one * size;
            // Quad UV extends beyond the square so even maximum blur has enough geometry.
            float extent = .5f + (s.shadowSoftness + .006f) / size;
            markerShadow.transform.localScale = Vector3.one * size;
            if (markerMesh.bounds.extents.x < extent || markerMesh.bounds.extents.x > extent + .001f) BuildMarkerMesh(extent);
            Apply(pathVisual, s, false, false, 1, clock);
            Apply(pathShadow, s, false, true, 1, clock);
            Apply(markerVisual, s, true, false, size, clock);
            Apply(markerShadow, s, true, true, size, clock);
        }
        private void Apply(MeshRenderer renderer, JcMovementIndicatorSettings s, bool marker, bool shadow, float size, float clock)
        {
            block.Clear();
            Color color = marker && !markerReachable ? s.unreachableColor : s.reachableColor;
            block.SetColor("_Color", shadow ? s.shadowColor : color);
            block.SetColor("_UnreachableColor", s.unreachableColor);
            block.SetColor("_HighlightColor", s.highlightColor);
            block.SetVector("_Shape", new Vector4(s.borderWidth, s.cornerRadius, s.ringRadius, s.ringWidth));
            block.SetVector("_Wave", new Vector4(s.waveWidth, s.waveSpeed, s.wavePeriod, s.highlightStrength));
            block.SetVector("_Dash", new Vector4(s.dashLength, s.dashGap, s.lineWidth, s.flowSpeed));
            block.SetFloat("_DotRadius", s.dotRadius);
            block.SetFloat("_Opacity", s.opacity * (shadow ? s.shadowOpacity : 1));
            block.SetFloat("_Mode", marker ? 0 : 1);
            block.SetFloat("_Shadow", shadow ? 1 : 0);
            block.SetFloat("_Softness", shadow ? s.shadowSoftness / size : 0);
            block.SetFloat("_Clock", clock);
            Vector3 center = destinationMarker.position;
            if (shadow) center += new Vector3(Mathf.Cos(s.shadowAngle * Mathf.Deg2Rad), 0, Mathf.Sin(s.shadowAngle * Mathf.Deg2Rad)) * s.shadowDistance;
            block.SetVector("_MarkerClip", new Vector4(center.x, center.z, gridManager.CellSize * s.markerSize, destinationMarker.gameObject.activeInHierarchy ? 1 : 0));
            renderer.SetPropertyBlock(block);
        }
        private void EnsureVisuals()
        {
            if (block == null) block = new MaterialPropertyBlock();
            if (visualRoot != null) return;
            visualRoot = new GameObject("__JC Movement Visuals") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(visualRoot, gameObject.scene);
            material = new Material(indicatorShader) { name = "JC Movement (runtime)", hideFlags = HideFlags.HideAndDontSave };
            shadowMaterial = new Material(material) { renderQueue = 2999 };
            pathMesh = NewMesh("JC Path"); shadowMesh = NewMesh("JC Path Shadow"); markerMesh = NewMesh("JC Marker");
            BuildMarkerMesh(.6f);
            pathVisual = MakeRenderer("Path", pathMesh, material);
            pathShadow = MakeRenderer("Path Shadow", shadowMesh, shadowMaterial);
            markerVisual = MakeRenderer("Destination", markerMesh, material);
            markerShadow = MakeRenderer("Destination Shadow", markerMesh, shadowMaterial);
            geometryDirty = true;
        }
        private static Mesh NewMesh(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave }; mesh.MarkDynamic(); return mesh;
        }
        private MeshRenderer MakeRenderer(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = destinationMarker.gameObject.layer };
            go.transform.SetParent(visualRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat; renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false; renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off; renderer.enabled = false;
            return renderer;
        }
        private void BuildMarkerMesh(float extent)
        {
            float e = Mathf.Max(.506f, extent);
            markerMesh.Clear();
            markerMesh.vertices = new[] { new Vector3(-e, 0, -e), new Vector3(-e, 0, e), new Vector3(e, 0, -e), new Vector3(e, 0, e) };
            markerMesh.uv = new[] { new Vector2(-e, -e), new Vector2(-e, e), new Vector2(e, -e), new Vector2(e, e) };
            markerMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            markerMesh.triangles = new[] { 0, 1, 2, 2, 1, 3 }; markerMesh.RecalculateBounds();
        }
        private void DisposeVisuals()
        {
            DestroyOwned(visualRoot); DestroyOwned(pathMesh); DestroyOwned(shadowMesh); DestroyOwned(markerMesh);
            DestroyOwned(material); DestroyOwned(shadowMaterial);
            visualRoot = null; pathVisual = pathShadow = markerVisual = markerShadow = null;
            pathMesh = shadowMesh = markerMesh = null; material = shadowMaterial = null;
        }
        private static void DestroyOwned(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
