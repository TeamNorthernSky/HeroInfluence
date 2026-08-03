using UnityEngine;

/// <summary>
/// Fog 시스템의 Renderer 레이어 규약.
/// 표준 Unity Layer "FogHidable"을 사용하여 카메라 cullingMask로 기본 Opaque에서 제외한다.
/// 명시 태깅되지 않은 Renderer는 기본 Layer에 남아 fog에 영향받지 않음.
///
/// 유닛 프리팹 구조 규약: 부모 GameObject는 물리 Layer(Enemy/Player/Mine 등)를 유지하고,
/// 자식 Renderer GameObject만 FogHidable Layer로 설정한다.
/// FogHidableTag 컴포넌트가 자식 Renderer들의 Layer를 자동 설정해준다.
/// </summary>
public static class FogRenderLayer
{
    public const string HidableLayerName = "FogHidable";

    public static int HidableLayerIndex => LayerMask.NameToLayer(HidableLayerName);
    public static int HidableLayerMask
    {
        get
        {
            int idx = HidableLayerIndex;
            return idx < 0 ? 0 : (1 << idx);
        }
    }
}
