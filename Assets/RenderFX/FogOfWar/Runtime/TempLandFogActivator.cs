using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TempLandFogActivator : MonoBehaviour
{
    [SerializeField] private Material fogMaterial;

    private FogOfWarFeatureJC cachedFeature;
    private GameObject cachedDebugCanvas;
    private bool isApplied;

    private void OnEnable()
    {
        UpdateFogState();
    }

    private void OnDisable()
    {
        ClearFog();
    }

    private void OnDestroy()
    {
        ClearFog();
    }

    private void Update()
    {
        UpdateFogState();
    }

    private void UpdateFogState()
    {
        bool shouldShow = !IsDebugPanelOpen();
        if (shouldShow && !isApplied) ApplyFog();
        else if (!shouldShow && isApplied) ClearFog();
    }

    private bool IsDebugPanelOpen()
    {
        var canvas = GetDebugCanvas();
        return canvas != null && canvas.activeInHierarchy;
    }

    private GameObject GetDebugCanvas()
    {
        if (cachedDebugCanvas != null) return cachedDebugCanvas;

        var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (canvas == null) continue;
            if (canvas.name != "DebugCanvas") continue;
            if (!canvas.gameObject.scene.IsValid()) continue;
            cachedDebugCanvas = canvas.gameObject;
            break;
        }
        return cachedDebugCanvas;
    }

    private FogOfWarFeatureJC GetFeature()
    {
        if (cachedFeature != null) return cachedFeature;

        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null) return null;

        var rendererListField = typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
        if (rendererListField == null) return null;

        var rendererList = rendererListField.GetValue(pipeline) as ScriptableRendererData[];
        if (rendererList == null) return null;

        for (int i = 0; i < rendererList.Length; i++)
        {
            var renderer = rendererList[i];
            if (renderer == null) continue;
            var features = renderer.rendererFeatures;
            for (int j = 0; j < features.Count; j++)
            {
                if (features[j] is FogOfWarFeatureJC fogFeature)
                {
                    cachedFeature = fogFeature;
                    return cachedFeature;
                }
            }
        }
        return null;
    }

    private void ApplyFog()
    {
        var feature = GetFeature();
        if (feature == null || fogMaterial == null) return;
        feature.settings.fogMaterial = fogMaterial;
        isApplied = true;
    }

    private void ClearFog()
    {
        if (!isApplied) return;
        var feature = GetFeature();
        if (feature == null) return;
        feature.settings.fogMaterial = null;
        isApplied = false;
    }
}
