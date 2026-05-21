using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class DHFogProgressController : MonoBehaviour
{
    public static DHFogProgressController Instance { get; private set; }

    private static readonly int FogVisibilityTexId = Shader.PropertyToID("_FogVisibilityTex");
    private static readonly int FogGridMinId = Shader.PropertyToID("_FogGridMin");
    private static readonly int FogGridMaxId = Shader.PropertyToID("_FogGridMax");
    private static readonly int FogGridWorldMinId = Shader.PropertyToID("_FogGridWorldMin");
    private static readonly int FogGridWorldSizeId = Shader.PropertyToID("_FogGridWorldSize");
    private static readonly int FogCellSizeId = Shader.PropertyToID("_FogCellSize");

    private readonly List<FeatureBinding> featureBindings = new List<FeatureBinding>();
    private Texture2D clearFogTexture;
    private Coroutine refreshCoroutine;

    private sealed class FeatureBinding
    {
        public DHFogOfWarFeature Feature;
        public Material OriginalMaterial;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureClearFogTexture();
        RefreshFogRenderState();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        QueueRefresh();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
            refreshCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (clearFogTexture != null)
            Destroy(clearFogTexture);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        QueueRefresh();
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        QueueRefresh();
    }

    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        QueueRefresh();
    }

    private void QueueRefresh()
    {
        if (!isActiveAndEnabled)
            return;

        if (refreshCoroutine != null)
            StopCoroutine(refreshCoroutine);

        refreshCoroutine = StartCoroutine(RefreshNextFrame());
    }

    private IEnumerator RefreshNextFrame()
    {
        yield return null;
        refreshCoroutine = null;
        RefreshFogRenderState();
    }

    private void RefreshFogRenderState()
    {
        CacheDHFogFeatures();

        bool shouldRenderFog = HasLoadedFogRenderManager();
        for (int i = 0; i < featureBindings.Count; i++)
        {
            FeatureBinding binding = featureBindings[i];
            if (binding == null || binding.Feature == null || binding.Feature.settings == null)
                continue;

            binding.Feature.settings.fogMaterial = shouldRenderFog ? binding.OriginalMaterial : null;
        }

        if (!shouldRenderFog)
            ClearFogShaderGlobals();
    }

    private void CacheDHFogFeatures()
    {
        UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null)
            return;

        FieldInfo rendererListField = typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
        if (rendererListField == null)
            return;

        ScriptableRendererData[] rendererList = rendererListField.GetValue(pipeline) as ScriptableRendererData[];
        if (rendererList == null)
            return;

        for (int i = 0; i < rendererList.Length; i++)
        {
            ScriptableRendererData rendererData = rendererList[i];
            if (rendererData == null)
                continue;

            List<ScriptableRendererFeature> features = rendererData.rendererFeatures;
            for (int j = 0; j < features.Count; j++)
            {
                if (features[j] is DHFogOfWarFeature fogFeature)
                    AddFeatureBinding(fogFeature);
            }
        }
    }

    private void AddFeatureBinding(DHFogOfWarFeature feature)
    {
        if (feature == null)
            return;

        for (int i = 0; i < featureBindings.Count; i++)
        {
            if (featureBindings[i].Feature == feature)
            {
                if (featureBindings[i].OriginalMaterial == null && feature.settings != null && feature.settings.fogMaterial != null)
                    featureBindings[i].OriginalMaterial = feature.settings.fogMaterial;

                return;
            }
        }

        featureBindings.Add(new FeatureBinding
        {
            Feature = feature,
            OriginalMaterial = feature.settings != null ? feature.settings.fogMaterial : null
        });
    }

    private static bool HasLoadedFogRenderManager()
    {
        FogRenderManager[] managers = FindObjectsByType<FogRenderManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            FogRenderManager manager = managers[i];
            if (manager == null)
                continue;

            Scene scene = manager.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
                return true;
        }

        return false;
    }

    private void EnsureClearFogTexture()
    {
        if (clearFogTexture != null)
            return;

        clearFogTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
        {
            name = "DHFogClearTexture",
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        clearFogTexture.SetPixel(0, 0, Color.white);
        clearFogTexture.Apply(false, true);
    }

    private void ClearFogShaderGlobals()
    {
        EnsureClearFogTexture();

        Shader.SetGlobalTexture(FogVisibilityTexId, clearFogTexture);
        Shader.SetGlobalVector(FogGridMinId, Vector4.zero);
        Shader.SetGlobalVector(FogGridMaxId, Vector4.zero);
        Shader.SetGlobalVector(FogGridWorldMinId, Vector4.zero);
        Shader.SetGlobalVector(FogGridWorldSizeId, Vector4.one);
        Shader.SetGlobalFloat(FogCellSizeId, 1f);
    }
}
