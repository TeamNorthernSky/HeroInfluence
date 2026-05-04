using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class KJ_FogHidableTag : MonoBehaviour
{
    private void OnValidate()
    {
#if UNITY_EDITOR
        // OnValidate에서 직접 Layer를 변경하면 경고가 발생하므로 delayCall 사용
        EditorApplication.delayCall += Apply;
#endif
    }

    private void Awake() => Apply();

    private void Apply()
    {
        if (this == null) return;

        int layer = KJ_FogRenderLayer.HidableLayerIndex;
        if (layer < 0)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.gameObject == gameObject)
                continue;

            if (renderer.gameObject.layer != layer)
            {
                renderer.gameObject.layer = layer;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    EditorUtility.SetDirty(renderer.gameObject);
                }
#endif
            }
        }
    }
}
