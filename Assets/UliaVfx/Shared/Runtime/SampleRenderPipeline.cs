using UnityEngine;
using UnityEngine.Rendering;
namespace UliaVfx {
 public sealed class SampleRenderPipeline:MonoBehaviour {
  public RenderPipelineAsset pipeline;RenderPipelineAsset previous,quality;
  void OnEnable(){previous=GraphicsSettings.defaultRenderPipeline;quality=QualitySettings.renderPipeline;GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;}
  void OnDisable(){GraphicsSettings.defaultRenderPipeline=previous;QualitySettings.renderPipeline=quality;}
 }
}
