using UnityEngine;

/// <summary>
/// 부모 GameObject에 부착. 자식 GameObject 중 Renderer를 가진 것들만 FogHidable Layer로 설정.
/// 부모 자신의 Layer는 변경하지 않는다 (Enemy/Player/Mine 등 물리 Layer 유지).
///
/// 예: Enemy 유닛 프리팹
///   Enemy (부모, Layer=Enemy) + FogHidableTag
///     └── Visual (자식, Layer=FogHidable, MeshRenderer)
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class FogHidableTag : MonoBehaviour
{
    private void OnValidate() => Apply();
    private void Awake() => Apply();

    private void Apply()
    {
        int layer = FogRenderLayer.HidableLayerIndex;
        if (layer < 0) return; // Layer 미등록 시 스킵

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            // 부모 자신의 Renderer는 스킵 — 부모는 물리 Layer 유지
            if (r.gameObject == gameObject) continue;
            r.gameObject.layer = layer;
        }
    }
}
