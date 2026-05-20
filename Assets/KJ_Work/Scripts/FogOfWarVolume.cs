using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable, VolumeComponentMenuForRenderPipeline("Custom/Fog Of War", typeof(UniversalRenderPipeline))]
public class FogOfWarVolume : VolumeComponent, IPostProcessComponent
{
    public ColorParameter fogColor = new ColorParameter(new Color(0.1f, 0.1f, 0.1f, 1f));
    public TextureParameter noiseTexture = new TextureParameter(null);
    public MinFloatParameter vignetteOuterRadius = new MinFloatParameter(15.0f, 0.1f);
    public MinFloatParameter vignetteInnerRadius = new MinFloatParameter(5.0f, 0.0f);

    public bool IsActive() => active && fogColor.value.a > 0f;
    public bool IsTileCompatible() => false;
}
