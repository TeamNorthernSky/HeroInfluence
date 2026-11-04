using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// JC | 2026-11-04 | 기존 이벤트 등록부의 메시를 재사용하고 가려진 부분만 별도 합성합니다. 건물 투명화와 판정은 변경하지 않습니다.
[ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("JC Environment/NPC·이벤트 가림 표시")]
public sealed class JcEventOcclusionController : MonoBehaviour
{
    [SerializeField, Tooltip("일반·튜토리얼 탐사가 공유하는 가림 표시 저장 프로필입니다.")] private JcEventOcclusionProfile profile;
    [SerializeField, Range(0, 1), Tooltip("실시간 조절용 초안입니다. 현재 값 저장 전에는 프로필을 변경하지 않습니다.")] private float opacity = .25f;
    [SerializeField, HideInInspector] private Shader compositeShader, surfaceShader;
    private static readonly List<JcEventOcclusionController> controllers = new List<JcEventOcclusionController>();
    private readonly List<Renderer> targets = new List<Renderer>();
    private readonly HashSet<Renderer> seen = new HashSet<Renderer>();
    internal readonly JC.Indicators.JcTriggerAreaSurfaces surfaces = new JC.Indicators.JcTriggerAreaSurfaces();
    private readonly List<Renderer> sceneRenderers = new List<Renderer>();
    internal IReadOnlyList<Renderer> SceneRenderers => sceneRenderers;
    private GridManager grid;
    private float nextCollect;
    private GameObject composite;
    private Mesh quad;
    private Material material, surfaceMaterial;
    private bool preview;
    public JcEventOcclusionProfile Profile => profile;
    public float Opacity { get => Mathf.Clamp01(opacity); set => opacity = Mathf.Clamp01(value); }
    internal Shader CompositeShader => compositeShader;
    internal Material SurfaceMaterial => surfaceMaterial;
    internal IReadOnlyList<Renderer> Targets => targets;
    public void LoadProfile() { if (profile) opacity = Mathf.Clamp01(profile.opacity); }
    public void Configure(JcEventOcclusionProfile preset, Shader shader, Shader surfacesShader)
    { profile = preset; compositeShader = shader; surfaceShader = surfacesShader; LoadProfile(); RequestRefresh(); }
    public void RequestRefresh() => nextCollect = 0;
    private void OnEnable()
    {
        // JC | 2026-11-04 | 다른 렌더러/미리보기 카메라에는 직전 카메라의 합성 텍스처가 새지 않게 초기화합니다.
        if (controllers.Count == 0) RenderPipelineManager.beginCameraRendering += ResetCamera;
        if (!controllers.Contains(this)) controllers.Add(this);
        if (Application.IsPlaying(gameObject)) { LoadProfile(); EnsureComposite(); }
    }
    private void OnDisable()
    {
        controllers.Remove(this);
        if (controllers.Count == 0) { RenderPipelineManager.beginCameraRendering -= ResetCamera; Shader.SetGlobalFloat("_JcEventGhostActive", 0); }
        targets.Clear(); sceneRenderers.Clear();
        DestroyOwned(composite); DestroyOwned(quad); DestroyOwned(material); DestroyOwned(surfaceMaterial);
        composite = null; quad = null; material = surfaceMaterial = null;
    }
    private static void ResetCamera(ScriptableRenderContext context, Camera camera) => Shader.SetGlobalFloat("_JcEventGhostActive", 0);
    private void OnValidate() => opacity = Mathf.Clamp01(opacity);
    // JC | 2026-11-04 | 일반 편집 씬에는 임시 표시물을 만들지 않으며 비플레이 검증에서만 명시적으로 켭니다.
    internal void EnablePreview() { preview = true; EnsureComposite(); }
    internal static bool TryResolve(Camera camera, out JcEventOcclusionController result)
    {
        Scene scene = camera.cameraType == CameraType.SceneView ? SceneManager.GetActiveScene() :
            camera.scene.IsValid() ? camera.scene : camera.gameObject.scene;
        for (int i = controllers.Count - 1; i >= 0; i--)
        {
            var c = controllers[i];
            if (!c) { controllers.RemoveAt(i); continue; }
            if (!c.isActiveAndEnabled || c.gameObject.scene != scene || (!Application.IsPlaying(c.gameObject) && !c.preview) ||
                !c.profile || !c.compositeShader || !c.surfaceShader || c.Opacity <= 0) continue;
            c.EnsureComposite(); c.Collect();
            if (c.grid && c.targets.Count > 0) { c.surfaces.Refresh(scene, c.grid); result = c; return true; }
        }
        result = null; return false;
    }
    private void EnsureComposite()
    {
        if (!compositeShader || !surfaceShader) return;
        if (!material) material = new Material(compositeShader) { name = "NPCイベント 가림 합성 (임시)", hideFlags = HideFlags.HideAndDontSave,
            renderQueue = JC.Indicators.JcIndicatorRenderOrder.EventOcclusion };
        if (!surfaceMaterial) surfaceMaterial = new Material(surfaceShader) { hideFlags = HideFlags.HideAndDontSave };
        material.SetFloat("_Opacity", Opacity);
        if (composite) return;
        composite = new GameObject("__EventOcclusionComposite") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        composite.transform.SetParent(transform, false);
        quad = new Mesh { name = "NPC event fullscreen triangle", hideFlags = HideFlags.HideAndDontSave };
        quad.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 3, 0), new Vector3(3, -1, 0) };
        quad.triangles = new[] { 0, 1, 2 }; quad.bounds = new Bounds(Vector3.zero, Vector3.one * 1000000);
        composite.AddComponent<MeshFilter>().sharedMesh = quad;
        var renderer = composite.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.allowOcclusionWhenDynamic = false;
    }
    private void Collect()
    {
        if (Time.realtimeSinceStartup < nextCollect) return;
        nextCollect = Time.realtimeSinceStartup + .25f;
        targets.Clear(); seen.Clear(); sceneRenderers.Clear();
        // JC | 2026-11-04 | 기존 이벤트 등록부를 입력으로 사용하며 NPC 카탈로그나 오브젝트 복제본을 만들지 않습니다.
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            sceneRenderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
            if (!grid) grid = root.GetComponentInChildren<GridManager>(true);
            foreach (var registry in root.GetComponentsInChildren<MainEventRegistry>(true)) foreach (var entry in registry.MainEvents) Add(entry);
            foreach (var registry in root.GetComponentsInChildren<SubEventRegistry>(true)) foreach (var entry in registry.SubEvents) Add(entry);
            foreach (var registry in root.GetComponentsInChildren<MapEventRegistry>(true)) foreach (var entry in registry.MapEvents) Add(entry);
            foreach (var registry in root.GetComponentsInChildren<WorldEventRegistry>(true)) foreach (var entry in registry.WorldEvents) Add(entry);
            foreach (var encounter in root.GetComponentsInChildren<EnemyEventEncounterBinding>(true)) if (encounter.HasEventEncounter) Add(encounter);
        }
    }
    private void Add(Component owner)
    {
        if (!owner || owner.gameObject.scene != gameObject.scene || !owner.gameObject.activeInHierarchy || owner is Behaviour b && !b.isActiveAndEnabled) return;
        foreach (var renderer in owner.GetComponentsInChildren<Renderer>(false))
            if ((renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && !renderer.GetComponentInParent<Canvas>() && seen.Add(renderer)) targets.Add(renderer);
    }
    internal static void DestroyOwned(Object value)
    { if (!value) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
}
