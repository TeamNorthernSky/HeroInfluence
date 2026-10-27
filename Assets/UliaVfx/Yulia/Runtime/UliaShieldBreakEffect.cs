using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UliaVfx
{
 // Self-contained effect: place this transform at the shield center.
 public sealed class UliaShieldBreakEffect : MonoBehaviour
 {
  public GameObject shieldPrefab, impactPrefab;
  public Shader holdShader, peelShader;
  public bool playOnEnable=true;
  public float diameter=2.473502f, leadIn=1.5f, peelSeconds=1.05f;
  public Vector3 localImpactDirection=Vector3.back;
  GameObject shieldRoot, effects;
  Material holdMaterial, shellMaterial, shardMaterial;
  readonly List<Mesh> meshes=new List<Mesh>();
  struct Shard { public Transform tr; public Vector3 start, normal, axis; public float trigger; }
  void OnEnable() { if(Application.isPlaying && playOnEnable && shieldPrefab && holdShader && peelShader) Replay(); }
  [ContextMenu("Replay Shield Disappearance")]
  public void Replay() { ShowHold(); if(shieldRoot) StartCoroutine(Sequence()); }
  [ContextMenu("Show Maintained Shield")]
  public void ShowHold() {
   Clear();
   if(!shieldPrefab || !holdShader || !peelShader) { Debug.LogError("Shield break effect is missing an asset reference.",this); return; }
   shieldRoot=new GameObject("ShieldHold_Continuous_Electric_Barrier");
   shieldRoot.transform.SetParent(transform,false);
   var surface=Instantiate(shieldPrefab,shieldRoot.transform);
   surface.name="Native_Shield_Surface_And_Electricity";
   surface.transform.localPosition=Vector3.zero;
   surface.transform.localRotation=Quaternion.identity;
   surface.transform.localScale=Vector3.one*(diameter/2.3f);
            foreach (var script in surface.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            foreach (var ps in surface.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.useUnscaledTime = true;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                if (ps.name == "shield_add" || ps.name == "shield_AB")
                {
                    ps.transform.localPosition = Vector3.zero;
                    main.startSpeed = 0; main.startLifetime = 10000; main.startSize = 1;
                    main.startColor = new Color(.52f, .12f, .9f, .10f);
                    var shape = ps.shape; shape.enabled = false;
                    var size = ps.sizeOverLifetime; size.enabled = false;
                    var color = ps.colorOverLifetime; color.enabled = false;
                    var emission = ps.emission; emission.enabled = false;
                    ps.Emit(1); ps.Pause(false);
                }
                else if (ps.name == "flash_electric")
                {
                    ps.transform.localPosition = Vector3.zero;
                    ps.transform.localRotation = Quaternion.identity;
                    main.loop = true; main.startSpeed = 0;
                    main.startSize = new ParticleSystem.MinMaxCurve(.32f, .58f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .32f);
                    main.startColor = new Color(1.8f, .55f, 3, .85f);
                    var shape = ps.shape; shape.enabled = true;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 1.14f; shape.radiusThickness = 0;
                    var emission = ps.emission; emission.enabled = true;
                    emission.SetBursts(new ParticleSystem.Burst[0]); emission.rateOverTime = 12;
                    var size = ps.sizeOverLifetime; size.enabled = false;
                    ps.Play(false); ps.Simulate(.6f, false, false); ps.Play(false);
                }
                else if (ps.gameObject != surface) ps.gameObject.SetActive(false);
            }

   var rim=GameObject.CreatePrimitive(PrimitiveType.Sphere);
   rim.name="Soft_Purple_Shield_Rim";
   rim.transform.SetParent(shieldRoot.transform,false);
   rim.transform.localScale=Vector3.one*diameter;
   rim.GetComponent<Collider>().enabled=false;
   holdMaterial=new Material(holdShader);
   holdMaterial.SetColor("_Color",new Color(.56f,.1f,1,.8f));
   var renderer=rim.GetComponent<Renderer>(); renderer.sharedMaterial=holdMaterial;
   renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
   renderer.receiveShadows=false;
  }
  public void Hide() { Clear(); }
  void Clear() {
   StopAllCoroutines();
   if(shieldRoot) { shieldRoot.SetActive(false); Destroy(shieldRoot); }
   if(effects) { effects.SetActive(false); Destroy(effects); }
   if(holdMaterial) Destroy(holdMaterial);
   if(shellMaterial) Destroy(shellMaterial);
   if(shardMaterial) Destroy(shardMaterial);
   foreach(var mesh in meshes) if(mesh) Destroy(mesh);
   meshes.Clear();
  }
  IEnumerator Sequence() {
   yield return new WaitForSecondsRealtime(leadIn);
   var root=shieldRoot.transform;
   var rim=root.Find("Soft_Purple_Shield_Rim");
   Vector3 center=root.position;
   float radius=diameter*.5f*Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.y),Mathf.Abs(transform.lossyScale.z));
   Vector3 front=transform.TransformDirection(localImpactDirection).normalized;
   if(front.sqrMagnitude<.5f) front=-transform.forward;
   effects=new GameObject("ShieldBreak_Front_Impact_And_Peeled_Shell");
   effects.transform.SetParent(transform,false);
   var native=root.Find("Native_Shield_Surface_And_Electricity");
   if(native) native.gameObject.SetActive(false);
   shellMaterial=new Material(peelShader);
   shellMaterial.SetVector("_Center",center); shellMaterial.SetVector("_Front",front);
   shellMaterial.SetColor("_Color",new Color(.56f,.1f,1,.8f));
   if(holdMaterial) Destroy(holdMaterial);
   rim.GetComponent<Renderer>().sharedMaterial=shellMaterial;
   // Keep native collision flash; the moving peel boundary handles the shell loss.
   if(impactPrefab) {
    var hit=Instantiate(impactPrefab,center+front*radius,Quaternion.LookRotation(front),effects.transform);
    hit.name="Shield_Front_Collision"; hit.transform.localScale=Vector3.one*.5f;
    foreach(var script in hit.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled=false;
    foreach(var ps in hit.GetComponentsInChildren<ParticleSystem>(true)) {
     ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
     var main=ps.main; main.loop=false; main.useUnscaledTime=true;
     main.scalingMode=ParticleSystemScalingMode.Hierarchy;
     main.startColor=new Color(2,.65f,3,1);
     if(ps.gameObject.activeInHierarchy) ps.Play(false);
    }
   }
   shardMaterial=new Material(shellMaterial);
   shardMaterial.SetFloat("_Progress",0); shardMaterial.SetFloat("_Fill",.28f);
   var shards=new List<Shard>();
   Vector3 right=Vector3.Cross(Vector3.up,front).normalized;
   for(int i=0;i<24;i++) {
    float z=Mathf.Lerp(.88f,-.9f,(i+.5f)/24);
    float angle=i*2.399963f;
    Vector3 n=front*z+(right*Mathf.Cos(angle)+Vector3.up*Mathf.Sin(angle))*Mathf.Sqrt(1-z*z);
    var go=new GameObject("Peeling_Shell_Fragment_"+i);
    go.transform.SetParent(effects.transform,false); go.transform.position=center+n*radius;
    var mesh=Patch(n,radius,.15f+(i%3)*.025f); meshes.Add(mesh);
    go.AddComponent<MeshFilter>().sharedMesh=mesh;
    var r=go.AddComponent<MeshRenderer>(); r.sharedMaterial=shardMaterial;
    r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows=false;
    go.SetActive(false);
    shards.Add(new Shard {tr=go.transform,start=go.transform.position,normal=n,
     axis=Vector3.Cross(n,front+Vector3.up*.3f).normalized,trigger=(1-z)/2.12f});
   }
   float time=0;
   while(time<peelSeconds+.65f) {
    time+=Time.unscaledDeltaTime;
    float progress=Mathf.Clamp01(time/peelSeconds);
    shellMaterial.SetFloat("_Progress",progress);
    if(progress>=1) rim.gameObject.SetActive(false);
    foreach(var s in shards) {
     float age=time-s.trigger*peelSeconds;
     if(age<0 || age>.6f) { s.tr.gameObject.SetActive(false); continue; }
     s.tr.gameObject.SetActive(true);
     float t=age/.6f;
     s.tr.position=s.start+s.normal*(t*.65f)-front*(t*.4f)+Vector3.down*t*t*.35f;
     s.tr.rotation=Quaternion.AngleAxis(t*125,s.axis);
     s.tr.localScale=Vector3.one*(1-Mathf.SmoothStep(0,1,t));
    }
    yield return null;
   }
   effects.SetActive(false);
   root.gameObject.SetActive(false);
   Debug.Log("[Yulia saved shield break] Front impact, shell peel and 24 fragments completed.");
  }
  Mesh Patch(Vector3 n,float radius,float width) {
   Vector3 u=Vector3.Cross(n,Mathf.Abs(n.y)>.9f?Vector3.right:Vector3.up).normalized;
   Vector3 v=Vector3.Cross(n,u);
   var vertices=new Vector3[25]; var normals=new Vector3[25]; var triangles=new List<int>();
   for(int y=0;y<5;y++) for(int x=0;x<5;x++) {
    int k=y*5+x; Vector3 d=(n+u*((x/4f-.5f)*width*2)+v*((y/4f-.5f)*width*2)).normalized;
    vertices[k]=(d-n)*radius; normals[k]=d;
    if(x<4 && y<4) { triangles.AddRange(new[]{k,k+5,k+1,k+1,k+5,k+6}); }
   }
   var mesh=new Mesh {name="Curved_Shield_Shell_Patch"};
   mesh.vertices=vertices; mesh.normals=normals; mesh.triangles=triangles.ToArray(); mesh.RecalculateBounds(); return mesh;
  }

  void OnDisable() { Clear(); }
 }
}

