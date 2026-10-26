using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace UliaVfx {
 // Runtime cue. The beam uses the authored top-down lightning particle layers.
 public sealed class UliaWormholeTrajectory : MonoBehaviour {
  public Camera effectCamera;public string stage;
  public int ReleaseFrame=>releaseFrame;public int BoltStartFrame=>boltFrame;
  bool captureVerificationFrames;
  public Shader crispShader;
  public float widthMultiplier=1.85f,afterglowSeconds=.1f;
  public float chargeDuration=.55f;
  public bool waitForAmplifierRelease;
  bool released;float releaseTime;int releaseFrame=-1,boltFrame=-1;
  GameObject portal,charge,preparedLightning;NativeBolt synchronizedBolt;Vector3 preparedStart,preparedDirection;Camera preparedCamera;float preparedLength;
  public void TriggerRelease(){
   if(released)return;released=true;releaseTime=Time.unscaledTime;releaseFrame=Time.frameCount;
   if(preparedLightning){synchronizedBolt=new NativeBolt(preparedLightning,transform,preparedStart,preparedDirection,preparedCamera,widthMultiplier,crispShader);synchronizedBolt.SetLength(preparedLength*.12f);boltFrame=Time.frameCount;}
  }
  public IEnumerator Play(Vector3 start,Vector3 end,GameObject lightning,GameObject circle,GameObject impact,Shader shader,GameObject volume=null,GameObject chargeAsset=null,GameObject branchAsset=null,GameObject nativeLightning=null){
   Vector3 direction=(end-start).normalized;var camera=effectCamera?effectCamera:Camera.main;
   float length=OffscreenLength(camera,start,direction,Vector3.Distance(start,end));
   preparedLightning=nativeLightning;preparedStart=start;preparedDirection=direction;preparedCamera=camera;preparedLength=length;
   portal=new GameObject("Trajectory_Purple_Wormhole");portal.transform.SetParent(transform,false);portal.transform.SetPositionAndRotation(start,Quaternion.LookRotation(direction));
   var mouth=SpawnParticles(volume?volume:circle,portal.transform,.36f);mouth.name="Appear04_Purple_Wormhole_Mouth";
   foreach(var ps in mouth.GetComponentsInChildren<ParticleSystem>()){var main=ps.main;main.simulationSpeed=.5f;}Warm(mouth,.5f);
   if(chargeAsset){charge=SpawnParticles(chargeAsset,portal.transform,.14f);charge.name="Energy08_Bright_Charging_Interior";charge.transform.localPosition=Vector3.forward*.08f;Warm(charge,.12f);}
   stage="Wormhole opening during amplifier charge";float t=0;bool capturedCharge=false;
   while(t<chargeDuration){
    t+=Time.unscaledDeltaTime;float progress=Mathf.Clamp01(t/Mathf.Max(.01f,chargeDuration));
    portal.transform.localScale=Vector3.one*Mathf.Lerp(.08f,1,Mathf.SmoothStep(0,1,progress));
    if(charge)charge.transform.localScale=Vector3.one*Mathf.Lerp(.08f,.22f,progress*progress);
    if(!capturedCharge&&progress>=.55f){Capture("01-wormhole-open");capturedCharge=true;}
    if(released)break;yield return null;
   }
   while(waitForAmplifierRelease&&!released)yield return null;
   if(!released)TriggerRelease();
   if(!nativeLightning){Debug.LogError("[Yulia trajectory] Missing authored top_down_lightning_line_Ulia_puple source.");stage="Complete";yield break;}
   var bolt=synchronizedBolt;
   stage="Native top-down lightning piercing beyond the screen";t=0;int hit=0;
   var contacts=System.Array.Empty<float>();
   while(t<.23f){
    t=Time.unscaledTime-releaseTime+Time.unscaledDeltaTime;float reach=Mathf.Clamp01(t/.075f);bolt.SetLength(length*reach);
    while(hit<contacts.Length&&reach*length>=contacts[hit]){Impact(impact,start+direction*contacts[hit]);hit++;}
    if(charge)charge.transform.localScale=Vector3.one*Mathf.Lerp(.28f,.20f,Mathf.Clamp01(t/.12f));
    if(t>=.15f&&t-Time.unscaledDeltaTime<.15f)Capture("02-piercing-flash");
    yield return null;
   }
   bolt.BeginAfterglow();stage="Native purple lightning afterimage";t=0;bool captured=false;
   while(t<afterglowSeconds){
    t+=Time.unscaledDeltaTime;float p=Mathf.Clamp01(t/afterglowSeconds);bolt.Fade(p);
    float collapse=Mathf.Clamp01(t/.24f);portal.transform.localScale=Vector3.one*Mathf.Lerp(1.12f,.01f,collapse*collapse);
    if(!captured&&p>=.32f){Capture("02b-purple-afterimage");captured=true;}yield return null;
   }
   bolt.Hide();
   // The lightning residue ends earlier; keep the portal's original collapse timing.
   while(t<.24f){t+=Time.unscaledDeltaTime;float collapse=Mathf.Clamp01(t/.24f);portal.transform.localScale=Vector3.one*Mathf.Lerp(1.12f,.01f,collapse*collapse);yield return null;}
   portal.SetActive(false);stage="Complete";Capture("03-complete");
  }
  static float OffscreenLength(Camera camera,Vector3 start,Vector3 direction,float minimum){
   if(!camera)return minimum+12;
   float length=minimum;
   for(int i=0;i<160;i++,length+=.5f){var p=camera.WorldToViewportPoint(start+direction*length);if(p.z>0&&(p.x<-.15f||p.x>1.15f||p.y<-.15f||p.y>1.15f))return length+1;}
   return minimum+20;
  }
  sealed class NativeBolt {
   readonly GameObject root;readonly Vector3 start,direction;readonly List<ParticleSystem> systems=new List<ParticleSystem>();
   readonly List<Color> originalColors=new List<Color>();readonly float width;readonly float started;float beamLength;readonly List<Material> materials=new List<Material>();
   public NativeBolt(GameObject source,Transform parent,Vector3 origin,Vector3 forward,Camera camera,float multiplier,Shader sharpShader){
    start=origin;direction=forward;width=1.6f*multiplier;started=Time.unscaledTime;
    root=new GameObject("Native_top_down_lightning_line_Ulia_puple_Beam");root.transform.SetParent(parent,false);
    Vector3 normal=camera?Vector3.ProjectOnPlane(camera.transform.position-origin,forward).normalized:Vector3.up;
    if(normal.sqrMagnitude<.01f)normal=Vector3.up;
    root.transform.rotation=Quaternion.LookRotation(normal,forward);
    var templates=source.GetComponentsInChildren<ParticleSystem>(true);
    // All layers reuse the authored lightning sprite; no generated bolt path. The halo
    // reuses the same lightning sprite and seed, keeping all layers aligned.
    foreach(string name in new[]{"lightning","lightning","lightning"}){
     var template=templates.First(p=>p.name==name);var go=Object.Instantiate(template.gameObject,root.transform);
     go.name=systems.Count==0?"Native_Lightning_Soft_Afterglow":name;go.SetActive(true);go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
     var ps=go.GetComponent<ParticleSystem>();
     // Use the same authored sprite sheet in grid mode. This avoids sprite-pivot
     // offsets when a vertical strike is reoriented as a horizontal beam.
     var uv=ps.textureSheetAnimation;var sprite=uv.mode==ParticleSystemAnimationMode.Sprites&&uv.spriteCount>0?uv.GetSprite(0):null;
     if(sprite){
      var rendererSource=go.GetComponent<ParticleSystemRenderer>();var mat=new Material(rendererSource.sharedMaterial);
      if(sharpShader){mat.shader=sharpShader;mat.SetColor("_TintColor",Color.white);mat.SetFloat("_DetailThreshold",systems.Count==0?.02f:systems.Count==2?.43f:.30f);mat.SetFloat("_EdgeSoftness",systems.Count==0?.7f:systems.Count==2?.12f:.14f);mat.SetFloat("_Intensity",systems.Count==0?.3f:systems.Count==2?1.45f:1.1f);}
      mat.SetTexture("_MainTex",sprite.texture);mat.SetTextureScale("_MainTex",new Vector2(1,.94f));mat.SetTextureOffset("_MainTex",new Vector2(0,.055f));rendererSource.sharedMaterial=mat;materials.Add(mat);
      uv.mode=ParticleSystemAnimationMode.Grid;uv.numTilesX=Mathf.RoundToInt(sprite.texture.width/sprite.rect.width);uv.numTilesY=Mathf.RoundToInt(sprite.texture.height/sprite.rect.height);
      uv.animation=ParticleSystemAnimationType.WholeSheet;
      if(sharpShader&&mat.HasProperty("_Frame")){mat.SetFloat("_Columns",uv.numTilesX);mat.SetFloat("_Frame",0);uv.enabled=false;}
     }
     ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
     var main=ps.main;main.loop=false;main.playOnAwake=false;main.useUnscaledTime=true;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
     main.startColor=systems.Count==2?new Color(1,.9f,1,1):new Color(.85f,.07f,1,1);var nativeColor=ps.colorOverLifetime;nativeColor.enabled=false;
     main.startLifetime=.85f;main.startSpeed=0;main.startSize3D=true;main.startSizeX=width*(systems.Count==0?1.15f:systems.Count==2?.36f:1);main.startSizeY=1;main.startSizeZ=1;main.startRotation3D=false;main.startRotation=0;main.maxParticles=1;
     var shape=ps.shape;shape.enabled=false;var emission=ps.emission;emission.enabled=false;var size=ps.sizeOverLifetime;size.enabled=false;var velocity=ps.velocityOverLifetime;velocity.enabled=false;var noise=ps.noise;noise.enabled=false;var subs=ps.subEmitters;subs.enabled=false;
     var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.alignment=ParticleSystemRenderSpace.Local;renderer.pivot=Vector3.zero;renderer.sortingOrder=3+systems.Count;
     ps.useAutoRandomSeed=false;ps.randomSeed=4206;ps.Emit(1);ps.Play(false);
     if(systems.Count==0){var particles=new ParticleSystem.Particle[1];ps.GetParticles(particles);particles[0].startColor=new Color(1,1,1,.14f);ps.SetParticles(particles,1);}
     systems.Add(ps);originalColors.Add(Color.white);
    }
    SetLength(.05f);
   }
   public void SetLength(float length){
    foreach(var mat in materials)if(mat.HasProperty("_Frame"))mat.SetFloat("_Frame",(Time.unscaledTime-started)*8f);
    beamLength=length;root.transform.position=start+direction*(length*.5f);
    for(int i=0;i<systems.Count;i++){var ps=systems[i];var p=new ParticleSystem.Particle[1];int count=ps.GetParticles(p);if(count==0)continue;p[0].position=Vector3.zero;p[0].startSize3D=new Vector3(width*(i==0?1.15f:i==2?.36f:1),Mathf.Max(.02f,length),1);ps.SetParticles(p,count);}
   }
   public void BeginAfterglow(){
    if(materials.Count>1&&materials[1].HasProperty("_DetailThreshold")){materials[1].SetFloat("_DetailThreshold",.08f);materials[1].SetFloat("_EdgeSoftness",.4f);materials[1].SetFloat("_Intensity",.9f);}
    systems[2].gameObject.SetActive(false);
    for(int i=0;i<2;i++){var ps=systems[i];ps.Pause(false);var p=new ParticleSystem.Particle[1];if(ps.GetParticles(p)>0)originalColors[i]=p[0].startColor;}
   }
   public void Fade(float p){
    for(int i=0;i<2;i++){var ps=systems[i];var particles=new ParticleSystem.Particle[1];int count=ps.GetParticles(particles);if(count==0)continue;var color=originalColors[i];color.a*=Mathf.Pow(1-p,1.5f);particles[0].startColor=color;particles[0].startSize3D=new Vector3(width*(i==0?1.15f:1)*Mathf.Lerp(1,1.2f,p),beamLength,1);ps.SetParticles(particles,count);}
   }
   public void Hide(){root.SetActive(false);foreach(var mat in materials)Object.Destroy(mat);}
  }
  GameObject SpawnParticles(GameObject prefab,Transform parent,float scale){
   var go=Instantiate(prefab,parent);go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one*scale;
   foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
   if(prefab.name.Contains("Energy_08"))foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true))if(new[]{"particle_pop","splash","dust_center","glow_floor","particle_gathering"}.Contains(ps.name))ps.gameObject.SetActive(false);
   foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true)){ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);var m=ps.main;m.useUnscaledTime=true;m.scalingMode=ParticleSystemScalingMode.Hierarchy;m.simulationSpace=ParticleSystemSimulationSpace.Local;if(ps.gameObject.activeInHierarchy)ps.Play(false);}return go;
  }
  void Warm(GameObject go,float seconds){foreach(var ps in go.GetComponentsInChildren<ParticleSystem>()){ps.Simulate(seconds,false,true,true);ps.Play(false);}}
  void Impact(GameObject prefab,Vector3 point){if(!prefab)return;var go=SpawnParticles(prefab,transform,.35f);go.transform.SetPositionAndRotation(point,Quaternion.identity);go.name="Piercing_Contact_Flash";Destroy(go,.8f);}
  void Capture(string name){
  }
 }
}

