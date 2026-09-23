using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PartyOcclusionFadeTarget : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int SilhouetteAlphaId = Shader.PropertyToID("_JcOcclusionAlpha");

    private PartyOcclusionFadeTargetRegistry registry;
    private Renderer[] cachedRenderers;
    private readonly Dictionary<Renderer, RendererFadeState> fadeStates = new Dictionary<Renderer, RendererFadeState>();

    private void OnEnable()
    {
        ResolveRegistry();
        registry?.Register(this);
    }

    private void OnDisable()
    {
        registry?.Unregister(this);
    }

    public bool TryGetRenderBounds(out Bounds bounds, float padding = 0f)
    {
        EnsureRenderersCached();

        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds && padding > 0f)
            bounds.Expand(padding);

        return hasBounds;
    }

    public void SetOcclusionFadeAlpha(float alpha, Material overrideMaterial)
    {
        EnsureRenderersCached();
        alpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null)
                continue;

            RendererFadeState state = GetFadeState(renderer);
            if (overrideMaterial != null)
                state.ApplyOverrideMaterial(renderer, overrideMaterial);

            state.ApplyAlpha(renderer, alpha);
        }
    }

    public void RestoreOcclusionFade()
    {
        EnsureRenderersCached();

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null || !fadeStates.TryGetValue(renderer, out RendererFadeState state))
                continue;

            state.Restore(renderer);
        }
    }

    public void RefreshRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        fadeStates.Clear();
    }

    private void ResolveRegistry()
    {
        if (registry == null)
            registry = PartyOcclusionFadeTargetRegistry.EnsureInstance();
    }

    private void EnsureRenderersCached()
    {
        if (cachedRenderers == null)
            RefreshRenderers();
    }

    private RendererFadeState GetFadeState(Renderer renderer)
    {
        if (fadeStates.TryGetValue(renderer, out RendererFadeState state))
            return state;

        state = new RendererFadeState(renderer);
        fadeStates.Add(renderer, state);
        return state;
    }

    private sealed class RendererFadeState
    {
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly Material[] originalSharedMaterials;
        private readonly bool hasBaseColor;
        private readonly bool hasColor;
        private readonly Color baseColor;
        private readonly Color color;

        public RendererFadeState(Renderer renderer)
        {
            originalSharedMaterials = renderer != null ? renderer.sharedMaterials : null;

            Material material = renderer != null ? renderer.sharedMaterial : null;
            if (material == null)
                return;

            hasBaseColor = material.HasProperty(BaseColorId);
            hasColor = material.HasProperty(ColorId);

            if (hasBaseColor)
                baseColor = material.GetColor(BaseColorId);
            if (hasColor)
                color = material.GetColor(ColorId);
        }

        public void ApplyAlpha(Renderer renderer, float alpha)
        {
            if (renderer == null)
                return;

            renderer.GetPropertyBlock(block);
            Material currentMaterial = renderer.sharedMaterial;
            if (currentMaterial != null && currentMaterial.HasProperty(SilhouetteAlphaId))
                block.SetFloat(SilhouetteAlphaId, alpha);
            if (hasBaseColor)
                block.SetColor(BaseColorId, WithAlpha(baseColor, alpha));
            if (hasColor)
                block.SetColor(ColorId, WithAlpha(color, alpha));
            renderer.SetPropertyBlock(block);
        }

        public void ApplyOverrideMaterial(Renderer renderer, Material overrideMaterial)
        {
            if (renderer == null || overrideMaterial == null)
                return;

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = overrideMaterial;
            renderer.sharedMaterials = materials;
        }

        public void Restore(Renderer renderer)
        {
            if (renderer == null)
                return;

            if (originalSharedMaterials != null)
                renderer.sharedMaterials = originalSharedMaterials;

            if (!hasBaseColor && !hasColor)
                return;

            renderer.GetPropertyBlock(block);
            if (hasBaseColor)
                block.SetColor(BaseColorId, baseColor);
            if (hasColor)
                block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        private static Color WithAlpha(Color source, float alpha)
        {
            source.a *= alpha;
            return source;
        }
    }
}
