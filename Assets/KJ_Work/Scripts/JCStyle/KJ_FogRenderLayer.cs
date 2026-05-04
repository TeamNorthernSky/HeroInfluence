using UnityEngine;

/// <summary>
/// JC 원본 FogRenderLayer를 KJ_Work로 복제한 버전.
/// </summary>
public static class KJ_FogRenderLayer
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
