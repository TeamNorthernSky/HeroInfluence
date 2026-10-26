using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UliaVfx {
 // Visual-only shield reaction. Gameplay owns hit resolution, immunity and damage.
 public sealed class UliaShieldRepelEffect:MonoBehaviour {
  public UliaShieldBreakEffect barrier;public GameObject impactPrefab;public Shader reactionShader;
  public Camera effectCamera;public bool playOnEnable=false;public float reactionSeconds=.7f;
  public string stage;public bool shieldMaintained;
  GameObject overlay,impact;Material reaction;Coroutine pulse;
  sealed class FillSurface { public GameObject go; public Mesh mesh; public Vector3[] rest,warped; }
  readonly List<FillSurface> fillSurfaces=new List<FillSurface>();
  Vector3 hitNormal;
  void ClearFill(){foreach(var fill in fillSurfaces){if(fill.go){fill.go.SetActive(false);Destroy(fill.go);}if(fill.mesh)Destroy(fill.mesh);}fillSurfaces.Clear();}
  Transform FindRestingRoot(){
   foreach(Transform child in barrier.transform)if(child.name=="ShieldHold_Continuous_Electric_Barrier"&&child.gameObject.activeSelf)return child;
   return null;
  }
  void BakeFill(){
   ClearFill();var root=FindRestingRoot();if(!root)return;
   foreach(var source in root.GetComponentsInChildren<ParticleSystemRenderer>(true)){
    if(source.name!="shield_add"&&source.name!="shield_AB")continue;
    var mesh=new Mesh();mesh.name="Preserved_Purple_Shield_Fill";source.BakeMesh(mesh,effectCamera?effectCamera:Camera.main,true);mesh.MarkDynamic();
    var go=new GameObject(source.name+"_DeformingFill");go.transform.SetParent(source.transform,false);
    go.AddComponent<MeshFilter>().sharedMesh=mesh;
    var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=source.sharedMaterials;
    renderer.sortingLayerID=source.sortingLayerID;renderer.sortingOrder=source.sortingOrder;
    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
    // Transform is already included in the baked vertices; do not apply it twice.
    go.transform.rotation=Quaternion.identity;
    Vector3 inherited=source.transform.lossyScale;
    go.transform.localScale=new Vector3(1/inherited.x,1/inherited.y,1/inherited.z);
    fillSurfaces.Add(new FillSurface{go=go,mesh=mesh,rest=mesh.vertices,warped=new Vector3[mesh.vertexCount]});go.SetActive(false);
   }
  }
  void DeformFill(float p){
   float radius=barrier.diameter*.5f*transform.lossyScale.x;
   foreach(var fill in fillSurfaces){
    for(int j=0;j<fill.rest.Length;j++){
     Vector3 world=fill.go.transform.TransformPoint(fill.rest[j]);Vector3 radial=(world-transform.position).normalized;
     float facing=Mathf.Clamp01(Vector3.Dot(radial,hitNormal));
     float dent=-radius*.65f*Mathf.Sin(p*Mathf.PI*5)*Mathf.Exp(-p*2.4f)*(1-p)*Mathf.Pow(facing,3.5f);
     float wave=radius*.12f*Mathf.Sin(Mathf.Acos(Mathf.Clamp(Vector3.Dot(radial,hitNormal),-.999f,.999f))*9-p*25)*Mathf.Sin(p*Mathf.PI)*(1-p)*(1-p)*facing;
     fill.warped[j]=fill.go.transform.InverseTransformPoint(world+radial*(dent+wave));
    }
    fill.mesh.vertices=fill.warped;fill.mesh.RecalculateBounds();
   }
  }
  void OnEnable(){if(Application.isPlaying&&playOnEnable&&barrier&&reactionShader){ShowShield();StartCoroutine(AutoHit());}}
  IEnumerator AutoHit(){yield return new WaitForSecondsRealtime(1);Hit(transform.position+Vector3.back*barrier.diameter*.5f);}
  public void ShowShield(){
   StopAllCoroutines();pulse=null;if(impact){impact.SetActive(false);Destroy(impact);}if(overlay)Destroy(overlay);if(reaction)Destroy(reaction);
   ClearFill();barrier.playOnEnable=false;barrier.previewKeys=false;barrier.ShowHold();
   overlay=GameObject.CreatePrimitive(PrimitiveType.Sphere);overlay.name="Shield_Contact_Flash_And_Surface_Wave";overlay.transform.SetParent(transform,false);overlay.transform.localScale=Vector3.one*(barrier.diameter);overlay.GetComponent<Collider>().enabled=false;
   reaction=new Material(reactionShader);reaction.SetColor("_Color",new Color(.56f,.1f,1,.8f));var r=overlay.GetComponent<Renderer>();r.sharedMaterial=reaction;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;overlay.SetActive(false);shieldMaintained=true;stage="Shield maintained";
  }
  void SetRestingSurface(bool visible){
   var root=FindRestingRoot();if(!root)return;
   var rim=root.Find("Soft_Purple_Shield_Rim");if(rim)rim.gameObject.SetActive(visible);
   foreach(var r in root.GetComponentsInChildren<ParticleSystemRenderer>(true))if(r.name=="shield_add"||r.name=="shield_AB")r.enabled=visible;
  }
  public void Hit(Vector3 worldPoint){
   if(!overlay)ShowShield();if(pulse!=null)StopCoroutine(pulse);
   Vector3 normal=(worldPoint-transform.position).normalized;if(normal.sqrMagnitude<.1f)normal=Vector3.back;
   Vector3 point=transform.position+normal*(barrier.diameter*.5f*transform.lossyScale.x);
   BakeFill();hitNormal=normal;DeformFill(0);foreach(var fill in fillSurfaces)fill.go.SetActive(true);
   reaction.SetVector("_Center",transform.position);reaction.SetVector("_Hit",point);reaction.SetFloat("_Progress",0);SetRestingSurface(false);overlay.SetActive(true);
   if(impact){impact.SetActive(false);Destroy(impact);}
   if(impactPrefab){impact=Instantiate(impactPrefab,point,Quaternion.LookRotation(normal),transform);impact.name="Native_Electric_Shield_Impact";impact.transform.localScale=Vector3.one*.15f;
    foreach(var script in impact.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
    foreach(var ps in impact.GetComponentsInChildren<ParticleSystem>(true)){ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.loop=false;main.useUnscaledTime=true;main.scalingMode=ParticleSystemScalingMode.Hierarchy;if(ps.gameObject.activeInHierarchy)ps.Play(false);}
   }
   stage="Shield absorbs frontal impact and deforms";pulse=StartCoroutine(Reaction());
  }
  IEnumerator Reaction(){float t=0;while(t<reactionSeconds){t+=Time.unscaledDeltaTime;float progress=Mathf.Clamp01(t/reactionSeconds);reaction.SetFloat("_Progress",progress);DeformFill(progress);yield return null;}overlay.SetActive(false);foreach(var fill in fillSurfaces)fill.go.SetActive(false);SetRestingSurface(true);if(impact){impact.SetActive(false);Destroy(impact);}pulse=null;shieldMaintained=true;stage="Shield restored to original shape after absorbing impact";}
  void OnDisable(){StopAllCoroutines();foreach(var fill in fillSurfaces)if(fill.go)fill.go.SetActive(false);if(barrier)SetRestingSurface(true);if(overlay)overlay.SetActive(false);if(impact)impact.SetActive(false);}
  void OnDestroy(){ClearFill();if(reaction)Destroy(reaction);}
 }
}
