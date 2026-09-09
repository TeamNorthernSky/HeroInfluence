using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DecorativeObjectPlacement : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    // [JC 수정 260908] 실루엣 마스크는 원본 재질 색/알파와 분리한 공통 불투명도를 사용한다.
    private static readonly int SilhouetteAlphaId = Shader.PropertyToID("_JcOcclusionAlpha");

    [SerializeField] private string prefabKey;
    [SerializeField] private Vector3 anchorLocalOffset;

    private DecorativeObjectRegistry registry;
    private Renderer[] cachedRenderers;
    private readonly Dictionary<Renderer, RendererFadeState> fadeStates = new Dictionary<Renderer, RendererFadeState>();

    public string PrefabKey => string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
    public Vector3 AnchorLocalOffset => anchorLocalOffset;

    private void OnEnable()
    {
        ResolveRegistry();
        registry?.Register(this);
    }

    private void OnDisable()
    {
        registry?.Unregister(this);
    }

    public Vector3 GetRootPositionForAnchor(Vector3 anchorWorldPosition)
    {
        Matrix4x4 localToRoot = Matrix4x4.TRS(Vector3.zero, transform.rotation, transform.localScale);
        return anchorWorldPosition - localToRoot.MultiplyPoint3x4(anchorLocalOffset);
    }

    public void SetPrefabKey(string nextPrefabKey)
    {
        prefabKey = string.IsNullOrWhiteSpace(nextPrefabKey) ? string.Empty : nextPrefabKey.Trim();
    }

    public void SetAnchorLocalOffset(Vector3 nextAnchorLocalOffset)
    {
        anchorLocalOffset = nextAnchorLocalOffset;
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

    public void SetOcclusionFadeAlpha(float alpha)
    {
        EnsureRenderersCached();
        alpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null)
                continue;

            RendererFadeState state = GetFadeState(renderer);
            state.ApplyAlpha(renderer, alpha);
        }
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
            registry = FindFirstObjectByType<DecorativeObjectRegistry>();
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

    private void OnDrawGizmosSelected()
    {
        Vector3 anchorWorldPosition = transform.TransformPoint(anchorLocalOffset);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(anchorWorldPosition, transform.rotation, Vector3.one);
        Gizmos.color = new Color(1f, 0.65f, 0.15f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1f, 0.02f, 1f));
        Gizmos.matrix = previousMatrix;

        Gizmos.color = new Color(1f, 0.85f, 0.15f, 1f);
        Gizmos.DrawWireSphere(anchorWorldPosition, 0.15f);
        Gizmos.DrawLine(transform.position, anchorWorldPosition);
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
