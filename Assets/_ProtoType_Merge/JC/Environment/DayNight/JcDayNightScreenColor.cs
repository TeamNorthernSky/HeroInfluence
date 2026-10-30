using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
namespace JC.Environment {
 // Own only the temporary color override; never edit scene volumes or shared profiles.
 public sealed class JcDayNightScreenColor : IDisposable {
  GameObject root;
  Volume volume;
  VolumeProfile profile;
  ColorAdjustments color;
  UniversalAdditionalCameraData cameraData;
  bool oldPost;
  public void Apply(Scene scene,JcDayNightProfile.Segment settings){
   float weight=settings.screenColorEnabled?Mathf.Clamp01(settings.screenColorStrength):0;
   if(weight<=0){Dispose();return;}
   Camera camera=null;
   foreach(var obj in scene.GetRootGameObjects())foreach(var candidate in obj.GetComponentsInChildren<Camera>())
    if(candidate.isActiveAndEnabled && candidate.CompareTag("MainCamera")){camera=candidate;break;}
   if(camera==null){Dispose();return;}
   var data=camera.GetComponent<UniversalAdditionalCameraData>();
   if(data==null || data.volumeLayerMask.value==0){Dispose();return;}
   if(cameraData!=data){Dispose();cameraData=data;oldPost=data.renderPostProcessing;}
   if(volume==null){
    root=new GameObject("JC_ScreenColor"){hideFlags=HideFlags.HideAndDontSave};SceneManager.MoveGameObjectToScene(root,scene);
    volume=root.AddComponent<Volume>();volume.isGlobal=true;volume.priority=1000;
    profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.hideFlags=HideFlags.HideAndDontSave;
    color=profile.Add<ColorAdjustments>(false);volume.sharedProfile=profile;
   }
   int mask=data.volumeLayerMask.value;for(int i=0;i<32;i++)if((mask&(1<<i))!=0){root.layer=i;break;}
   data.renderPostProcessing=true;
   volume.weight=weight;
   color.colorFilter.Override(settings.screenColorFilter);
   color.saturation.Override(settings.screenSaturation);
   color.contrast.Override(settings.screenContrast);
   color.postExposure.Override(settings.screenExposure);
  }
  public void Dispose(){
   if(cameraData!=null)cameraData.renderPostProcessing=oldPost;
   cameraData=null;
   // Disable immediately, including when Destroy is deferred to the end of a frame.
   if(volume!=null){volume.enabled=false;volume.sharedProfile=null;}
   Destroy(root);if(profile!=null){foreach(var component in profile.components)Destroy(component);Destroy(profile);}
   root=null;volume=null;profile=null;color=null;
  }
  static void Destroy(UnityEngine.Object o){if(o==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
 }
}
