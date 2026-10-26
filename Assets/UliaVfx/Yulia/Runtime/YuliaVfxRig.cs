using System;
using System.Collections;
using UnityEngine;
namespace UliaVfx {
 public sealed class YuliaVfxRig:MonoBehaviour {
  public GameObject idlePrefab,bodyHitPrefab,supplyPrefab;
  public DespairRingVfx ringPrefab;
  public YuliaCastVfx casting;
  public UliaShieldBreakEffect shield;
  public UliaShieldRepelEffect shieldHit;
  [Header("Assign actor references or call Bind()")]
  public Transform unit,handSocket,shieldCenter;
  public Animator actorAnimator;
  public Camera effectCamera;
  public float characterHeight=2.061252f;
  public bool IsShieldVisible {get;private set;}
  public bool IsCasting {get;private set;}
  public DespairRingVfx[] LastSummoned {get;private set;}
  public event Action<DespairRingVfx[]> Summoned;
  public event Action ShieldGenerated;
  Transform owned;GameObject idle;Coroutine generation;
  void Awake(){if(shield){shield.playOnEnable=false;shield.previewKeys=false;}if(shieldHit)shieldHit.playOnEnable=false;}
  public void Bind(Transform actor,Animator animator,Transform hand,Transform center,float height,Camera camera=null){
   if(!actor||!animator||!hand||!center||height<=0)throw new ArgumentException("Assign actor, Animator, hand socket, shield center and positive character height.");
   unit=actor;actorAnimator=animator;handSocket=hand;shieldCenter=center;characterHeight=height;effectCamera=camera;shieldHit.effectCamera=camera;
   VfxSpawn.WorldScale(transform,1);transform.rotation=Quaternion.identity;UpdateShieldAnchor();
  }
  void EnsureBound(){if(!unit||!actorAnimator||!handSocket||!shieldCenter)throw new InvalidOperationException("Bind Yulia actor references before playing effects.");if(!owned)owned=new GameObject("Yulia_Owned_Vfx").transform;}
  void UpdateShieldAnchor(){if(shieldCenter&&shield){shield.transform.position=shieldCenter.position;shield.transform.rotation=Quaternion.identity;shield.diameter=characterHeight*1.2f;}}
  void LateUpdate(){UpdateShieldAnchor();if(idle&&handSocket)idle.transform.SetPositionAndRotation(handSocket.position,handSocket.rotation*Quaternion.Euler(-28.183f,35.378f,124.165f));}
  public void SetIdle(bool visible){EnsureBound();if(idle){idle.SetActive(false);Destroy(idle);idle=null;}if(visible)idle=VfxSpawn.Spawn(idlePrefab,handSocket.position,handSocket.rotation*Quaternion.Euler(-28.183f,35.378f,124.165f),.35f,owned);}
  public void PlayBodyHit(Vector3 worldPoint,Vector3 outwardNormal){EnsureBound();var hit=VfxSpawn.Spawn(bodyHitPrefab,worldPoint,Quaternion.LookRotation(outwardNormal.sqrMagnitude>.001f?outwardNormal:Vector3.back),1,owned);Destroy(hit,3);}
  public void ShowShield(){EnsureBound();UpdateShieldAnchor();shieldHit.enabled=true;shieldHit.ShowShield();IsShieldVisible=true;}
  public void HitShield(Vector3 worldPoint){EnsureBound();if(IsShieldVisible)shieldHit.Hit(worldPoint);}
  public void BreakShield(Vector3 worldImpactPoint){EnsureBound();if(!IsShieldVisible)return;shieldHit.enabled=false;shield.localImpactDirection=shield.transform.InverseTransformDirection((worldImpactPoint-shield.transform.position).normalized);shield.leadIn=0;shield.Replay();IsShieldVisible=false;}
  public void ShowShieldPop(){ShowShield();StartCoroutine(Pop());}
  IEnumerator Pop(){Transform root=null;foreach(Transform child in shield.transform)if(child.name=="ShieldHold_Continuous_Electric_Barrier"&&child.gameObject.activeSelf)root=child;if(!root)yield break;float t=0;while(t<.32f){t+=Time.unscaledDeltaTime;float scale=t<.08f?Mathf.Lerp(.92f,1.25f,t/.08f):Mathf.Lerp(1.25f,1,Mathf.SmoothStep(0,1,(t-.08f)/.24f));root.localScale=Vector3.one*scale;yield return null;}root.localScale=Vector3.one;ShieldGenerated?.Invoke();}
  public void GenerateShield(Vector3[] amplifierCorePositions){EnsureBound();if(amplifierCorePositions==null||amplifierCorePositions.Length==0)throw new ArgumentException("Supply at least one amplifier core position.");if(generation!=null)StopCoroutine(generation);generation=StartCoroutine(Generate((Vector3[])amplifierCorePositions.Clone()));}
  IEnumerator Generate(Vector3[] cores){foreach(var point in cores){var go=VfxSpawn.Spawn(supplyPrefab,point-Vector3.up*(1.77f*.22f),Quaternion.identity,.22f,owned);Destroy(go,6);}yield return new WaitForSecondsRealtime(2.7f);ShowShield();yield return Pop();generation=null;}
  public void CastDespairRings(Vector3[] summonTilePositions){
   EnsureBound();if(IsCasting)return;IsCasting=true;if(idle)idle.SetActive(false);casting.leadIn=0;
   casting.Play(unit,actorAnimator,handSocket,summonTilePositions,characterHeight,born=>{
    var rings=new DespairRingVfx[born.Length];for(int i=0;i<born.Length;i++){rings[i]=Instantiate(ringPrefab);rings[i].Adopt(born[i],characterHeight);}LastSummoned=rings;IsCasting=false;if(idle)idle.SetActive(true);Summoned?.Invoke(rings);
   });
  }
  // Stopping this rig does not destroy independent summons already handed to gameplay.
  public void Stop(){StopAllCoroutines();generation=null;IsCasting=false;if(casting)casting.Stop();if(shieldHit)shieldHit.enabled=false;if(shield)shield.Hide();IsShieldVisible=false;if(owned){owned.gameObject.SetActive(false);Destroy(owned.gameObject);}owned=null;idle=null;}
  void OnDisable(){Stop();}void OnDestroy(){Stop();}
 }
}
