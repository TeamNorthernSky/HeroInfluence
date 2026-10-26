using System;
using System.Collections;
using UnityEngine;
using UnityEngine.VFX;
namespace UliaVfx {
 public sealed class AmplifierVfxRig:MonoBehaviour {
  public GameObject idlePrefab,stormPrefab,supplyPrefab,explosionPrefab,smokePrefab;
  public UliaSavedEffect castPrefab,trajectoryPrefab;
  [Header("Assign scene references or call Bind()")]
  public Transform coreAnchor;
  public GameObject coreVisual;
  public Light[] coreLights;
  public Camera effectCamera;
  public bool IsBroken {get;private set;}
  public bool IsBusy {get;private set;}
  public UliaWormholeTrajectory LastTrajectory {get;private set;}
  public GameObject BrokenSmoke=>smoke;
  public event Action SkillReleased;
  public event Action CoreBroken;
  public event Action CoreRecovered;
  public event Action SupplyReleased;
  Transform owned;GameObject idle,smoke;Vector3 originalScale;float[] lightStrengths;bool[] lightEnabled;
  public void Bind(Transform core,GameObject visibleCore,Light[] lights=null,Camera camera=null){
   if(!core||!visibleCore)throw new ArgumentException("Assign the core anchor and core visual object.");
   coreAnchor=core;coreVisual=visibleCore;coreLights=lights??new Light[0];effectCamera=camera;originalScale=coreVisual.transform.localScale;
   lightStrengths=new float[coreLights.Length];lightEnabled=new bool[coreLights.Length];for(int i=0;i<coreLights.Length;i++)if(coreLights[i]){lightStrengths[i]=coreLights[i].intensity;lightEnabled[i]=coreLights[i].enabled;}
   IsBroken=false;
  }
  void EnsureBound(){if(!coreAnchor||!coreVisual)throw new InvalidOperationException("Bind the amplifier core before playing effects.");if(!owned)owned=new GameObject("Amplifier_Owned_Vfx").transform;}
  void LateUpdate(){if(idle&&coreAnchor)idle.transform.position=coreAnchor.position;if(smoke&&coreAnchor)smoke.transform.position=coreAnchor.position;}
  public void SetIdle(bool visible){EnsureBound();if(idle){idle.SetActive(false);Destroy(idle);idle=null;}if(visible&&!IsBroken)idle=VfxSpawn.Spawn(idlePrefab,coreAnchor.position,Quaternion.identity,.28f,owned);}
  UliaSavedEffect Charge(Action release){
   var cast=Instantiate(castPrefab,coreAnchor.position,Quaternion.identity,owned);cast.playOnEnable=false;cast.ChargeReleased+=()=>{SkillReleased?.Invoke();release?.Invoke();};cast.Replay();Destroy(cast.gameObject,4);return cast;
  }
  public void PlayCast(){EnsureBound();if(IsBroken)return;Charge(null);}
  public void PlayStorm(Vector3 columnCenter){EnsureBound();if(IsBroken)return;Charge(()=>{
   var strike=VfxSpawn.Spawn(stormPrefab,columnCenter+Vector3.up*.02f,stormPrefab.transform.rotation,1,owned);
   strike.transform.localScale=stormPrefab.transform.localScale;
   var particles=strike.GetComponentsInChildren<ParticleSystem>(true);
   foreach(var ps in particles){ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.loop=false;}
   foreach(var ps in particles)if(ps.transform==strike.transform||ps.name=="shot_controller"||ps.name=="hit_controller")if(ps.gameObject.activeInHierarchy)ps.Play(false);
   Destroy(strike,7);
  });}
  public void PlayTrajectory(Vector3 worldOrigin,Vector3 worldEnd,Camera camera=null){
   EnsureBound();if(IsBroken)return;if((worldEnd-worldOrigin).sqrMagnitude<.001f)throw new ArgumentException("Trajectory endpoint must differ from origin.");
   var shot=Instantiate(trajectoryPrefab,worldOrigin,Quaternion.identity,owned);shot.playOnEnable=false;shot.effectCamera=camera?camera:effectCamera;shot.localEndPoint=shot.transform.InverseTransformPoint(worldEnd);shot.endTarget=null;shot.Replay();
   var worm=shot.ActiveTrajectory;LastTrajectory=worm;
   if(worm){worm.waitForAmplifierRelease=true;worm.chargeDuration=castPrefab.chargeSeconds;Charge(worm.TriggerRelease);}else Charge(null);
   Destroy(shot.gameObject,6);
  }
  public void PlayEnergySupply(Action onBurst=null){EnsureBound();StartCoroutine(Supply(onBurst));}
  IEnumerator Supply(Action onBurst){var go=VfxSpawn.Spawn(supplyPrefab,coreAnchor.position-Vector3.up*(1.77f*.22f),Quaternion.identity,.22f,owned);Destroy(go,6);yield return new WaitForSecondsRealtime(2.7f);SupplyReleased?.Invoke();onBurst?.Invoke();}
  public void BreakCore(){EnsureBound();if(IsBusy||IsBroken)return;Stop();EnsureBound();StartCoroutine(Break());}
  IEnumerator Break(){
   IsBusy=true;if(idle){Destroy(idle);idle=null;}
   VfxSpawn.Burst(explosionPrefab,coreAnchor.position,.28f,owned);coreVisual.SetActive(false);IsBroken=true;
   foreach(var l in coreLights)if(l)l.enabled=false;CoreBroken?.Invoke();
   yield return new WaitForSecondsRealtime(.32f);
   smoke=VfxSpawn.Spawn(smokePrefab,coreAnchor.position,Quaternion.identity,.75f,owned);
   foreach(var v in smoke.GetComponentsInChildren<VisualEffect>(true)){
    if(v.HasFloat("SmokeLifetime"))v.SetFloat("SmokeLifetime",1.15f);if(v.HasFloat("SmokeRate"))v.SetFloat("SmokeRate",4);if(v.HasFloat("SmokeSize"))v.SetFloat("SmokeSize",1.05f);
    if(v.HasVector3("SmokeVelocity"))v.SetVector3("SmokeVelocity",new Vector3(.08f,.7f,.08f));
    if(v.HasAnimationCurve("SmokeErosion"))v.SetAnimationCurve("SmokeErosion",new AnimationCurve(new Keyframe(0,.9f),new Keyframe(.1f,.05f),new Keyframe(.65f,.15f),new Keyframe(1,1)));
    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.9f,.9f,.9f),.4f),new GradientColorKey(new Color(.75f,.75f,.75f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.9f,.08f),new GradientAlphaKey(.75f,.4f),new GradientAlphaKey(.2f,.7f),new GradientAlphaKey(0,1)});
    if(v.HasGradient("SmokeGradient"))v.SetGradient("SmokeGradient",gradient);v.Reinit();v.Play();
   }
   yield return new WaitForSecondsRealtime(.4f);
   foreach(var v in smoke.GetComponentsInChildren<VisualEffect>(true)){
    if(v.HasFloat("SmokeRate"))v.SetFloat("SmokeRate",2);if(v.HasFloat("SmokeSize"))v.SetFloat("SmokeSize",.65f);if(v.HasFloat("SmokeLifetime"))v.SetFloat("SmokeLifetime",1.6f);if(v.HasVector3("SmokeVelocity"))v.SetVector3("SmokeVelocity",new Vector3(.05f,.6f,.05f));
   }
   IsBusy=false;
  }
  public void RecoverCore(Action onBurst=null){EnsureBound();if(IsBusy||!IsBroken)return;StartCoroutine(Recover(onBurst));}
  IEnumerator Recover(Action onBurst){
   IsBusy=true;if(smoke)foreach(var v in smoke.GetComponentsInChildren<VisualEffect>(true))v.Stop();
   var supply=VfxSpawn.Spawn(supplyPrefab,coreAnchor.position-Vector3.up*(1.77f*.22f),Quaternion.identity,.22f,owned);Destroy(supply,6);
   float time=0;while(time<2.7f){time+=Time.unscaledDeltaTime;float form=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-2.05f)/.65f));
    if(form>0){coreVisual.SetActive(true);coreVisual.transform.localScale=originalScale*Mathf.Lerp(.025f,1,form);for(int i=0;i<coreLights.Length;i++)if(coreLights[i]){coreLights[i].enabled=true;coreLights[i].intensity=lightStrengths[i]*form;}}
    if(smoke&&time>1.6f){Destroy(smoke);smoke=null;}yield return null;
   }
   coreVisual.SetActive(true);coreVisual.transform.localScale=originalScale;IsBroken=false;IsBusy=false;
   for(int i=0;i<coreLights.Length;i++)if(coreLights[i]){coreLights[i].intensity=lightStrengths[i];coreLights[i].enabled=lightEnabled[i];}
   foreach(var ps in coreVisual.GetComponentsInChildren<ParticleSystem>(true))if(ps.gameObject.activeInHierarchy)ps.Play(false);
   foreach(var v in coreVisual.GetComponentsInChildren<VisualEffect>(true))if(v.gameObject.activeInHierarchy){v.Reinit();v.Play();}
   SupplyReleased?.Invoke();onBurst?.Invoke();CoreRecovered?.Invoke();
  }
  public void Stop(){StopAllCoroutines();IsBusy=false;if(owned){owned.gameObject.SetActive(false);Destroy(owned.gameObject);}owned=null;idle=null;smoke=null;}
  // Explicit reset is separate from Stop: callers decide whether a broken core stays broken.
  public void ResetVisualState(){Stop();if(!coreVisual)return;coreVisual.SetActive(true);coreVisual.transform.localScale=originalScale;IsBroken=false;for(int i=0;i<coreLights.Length;i++)if(coreLights[i]){coreLights[i].intensity=lightStrengths[i];coreLights[i].enabled=lightEnabled[i];}}
  void OnDisable(){Stop();}void OnDestroy(){Stop();}
 }
}
