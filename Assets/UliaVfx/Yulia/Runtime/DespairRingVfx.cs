using System.Collections;
using UnityEngine;
using UnityEngine.VFX;
namespace UliaVfx {
 public sealed class DespairRingVfx:MonoBehaviour {
  public GameObject orbPrefab,rushPrefab,explosionPrefab;
  public Shader bodyShader;
  public float rushSeconds=.85f,hoverPeriod=3.8f,hoverAmplitude=.12f;
  public bool IsBusy {get;private set;}
  public bool IsVisible=>orb&&orb.activeSelf;
  public Vector3 VisualPosition=>orb?orb.transform.position:transform.position;
  public event System.Action RushFinished;
  public event System.Action Destroyed;
  GameObject orb,trail;Material body;Vector3 home;float height=2,elapsed;Transform owned;
  void EnsureRoot(){if(owned)return;owned=new GameObject("Ring_Owned_Vfx").transform;}
  public void Show(Vector3 center,float characterHeight){
   Stop();EnsureRoot();height=characterHeight;home=center;
   orb=VfxSpawn.Spawn(orbPrefab,center,Quaternion.identity,height/5.3f,owned);
   var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.name="Dense_Purple_Inner_Body";sphere.transform.SetParent(orb.transform,false);sphere.transform.localScale=Vector3.one*4.5f;sphere.GetComponent<Collider>().enabled=false;
   body=new Material(bodyShader);body.SetColor("_BaseColor",new Color(.20f,.012f,.38f,.62f));sphere.GetComponent<Renderer>().sharedMaterial=body;
   SetColors();elapsed=0;
  }
  public void Adopt(GameObject summonedOrb,float characterHeight){
   Stop();EnsureRoot();orb=summonedOrb;orb.transform.SetParent(owned,true);home=orb.transform.position;height=characterHeight;
   var inner=orb.transform.Find("Dense_Purple_Inner_Body");if(inner){var renderer=inner.GetComponent<Renderer>();body=new Material(renderer.sharedMaterial);renderer.sharedMaterial=body;}
   elapsed=0;
  }
  void SetColors(){foreach(var v in orb.GetComponentsInChildren<VisualEffect>(true)){if(v.HasVector4("Color"))v.SetVector4("Color",new Vector4(6,.65f,12,1));if(v.HasVector4("Color02"))v.SetVector4("Color02",new Vector4(9,.6f,16,1));v.Reinit();v.Play();}}
  void Update(){if(orb&&!IsBusy&&orb.activeSelf){elapsed+=Time.unscaledDeltaTime;orb.transform.position=home+Vector3.up*(Mathf.Sin(elapsed*Mathf.PI*2/Mathf.Max(.1f,hoverPeriod))*hoverAmplitude);}}
  public void RushTo(Vector3 worldEnd){if(!IsBusy&&IsVisible)StartCoroutine(Rush(worldEnd));}
  IEnumerator Rush(Vector3 end){
   IsBusy=true;Vector3 start=orb.transform.position;Vector3 direction=(end-start).normalized;if(direction.sqrMagnitude<.001f)direction=Vector3.back;
   trail=VfxSpawn.Spawn(rushPrefab,start,Quaternion.LookRotation(direction),.24f,owned);
   foreach(var ps in trail.GetComponentsInChildren<ParticleSystem>(true))if(ps.name=="electric"){var main=ps.main;main.startSizeMultiplier*=2.2f;}
   foreach(var tr in trail.GetComponentsInChildren<TrailRenderer>(true)){tr.widthMultiplier*=3;tr.time=Mathf.Max(tr.time,.28f);tr.Clear();}
   float time=0;while(time<rushSeconds){time+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(time/Mathf.Max(.01f,rushSeconds));orb.transform.position=Vector3.Lerp(start,end,t*(.35f+.65f*t));trail.transform.position=orb.transform.position;yield return null;}
   orb.SetActive(false);foreach(var ps in trail.GetComponentsInChildren<ParticleSystem>(true))ps.Stop(false,ParticleSystemStopBehavior.StopEmitting);foreach(var tr in trail.GetComponentsInChildren<TrailRenderer>(true))tr.emitting=false;
   yield return new WaitForSecondsRealtime(.25f);if(trail)Destroy(trail);IsBusy=false;RushFinished?.Invoke();
  }
  public void Explode(){if(!IsBusy&&IsVisible)StartCoroutine(Explosion());}
  IEnumerator Explosion(){IsBusy=true;float t=0;Vector3 size=orb.transform.localScale;while(t<.16f){t+=Time.unscaledDeltaTime;orb.transform.localScale=size*Mathf.Lerp(1,1.14f,Mathf.Clamp01(t/.16f));yield return null;}Vector3 point=orb.transform.position;orb.SetActive(false);VfxSpawn.Burst(explosionPrefab,point,.38f,owned);IsBusy=false;Destroyed?.Invoke();}
  public void Stop(){StopAllCoroutines();IsBusy=false;if(owned){owned.gameObject.SetActive(false);Destroy(owned.gameObject);}owned=null;orb=null;trail=null;if(body)Destroy(body);body=null;}
  void OnDisable(){Stop();}void OnDestroy(){Stop();}
 }
}
