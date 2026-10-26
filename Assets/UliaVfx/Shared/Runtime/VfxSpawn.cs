using UnityEngine;
using UnityEngine.VFX;
namespace UliaVfx {
 public static class VfxSpawn {
  public static GameObject Spawn(GameObject prefab,Vector3 position,Quaternion rotation,float scale,Transform parent=null){
   if(!prefab)throw new System.ArgumentNullException(nameof(prefab));
   var go=Object.Instantiate(prefab,position,rotation,parent);WorldScale(go.transform,scale);
   foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
   foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true)){
    ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);var m=ps.main;m.useUnscaledTime=true;m.scalingMode=ParticleSystemScalingMode.Hierarchy;m.stopAction=ParticleSystemStopAction.None;
    if(ps.gameObject.activeInHierarchy)ps.Play(false);
   }
   foreach(var v in go.GetComponentsInChildren<VisualEffect>(true))if(v.gameObject.activeInHierarchy){v.pause=false;v.Reinit();v.Play();}
   return go;
  }
  public static void WorldScale(Transform t,float scale){Vector3 p=t.parent?t.parent.lossyScale:Vector3.one;t.localScale=new Vector3(scale/NonZero(p.x),scale/NonZero(p.y),scale/NonZero(p.z));}
  static float NonZero(float value)=>Mathf.Abs(value)<.0001f?1:value;
  public static GameObject Burst(GameObject prefab,Vector3 position,float scale,Transform parent){
   var go=Spawn(prefab,position,Quaternion.identity,scale,parent);
   foreach(var v in go.GetComponentsInChildren<VisualEffect>(true)){
    v.playRate=.7f;
    if(v.HasFloat("Delay"))v.SetFloat("Delay",0);if(v.HasFloat("GroundCount"))v.SetFloat("GroundCount",0);
    if(v.HasFloat("SparksCount"))v.SetFloat("SparksCount",65);if(v.HasFloat("SparksSize"))v.SetFloat("SparksSize",1.4f);
    if(v.HasFloat("ShockwaveCount"))v.SetFloat("ShockwaveCount",2);if(v.HasFloat("ShockwaveSize"))v.SetFloat("ShockwaveSize",9);
    if(v.HasVector4("FlareColor"))v.SetVector4("FlareColor",new Vector4(12,4,20,1));
    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(24,12,36),0),new GradientColorKey(new Color(7,.6f,16),.35f),new GradientColorKey(new Color(1.5f,.05f,4),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(.7f,.55f),new GradientAlphaKey(0,1)});
    if(v.HasGradient("SparksGradient"))v.SetGradient("SparksGradient",gradient);v.Reinit();v.Play();
   }
   Object.Destroy(go,4);return go;
  }
 }
}
