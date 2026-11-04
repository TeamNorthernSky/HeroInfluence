using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace JC.Environment {
 // 원본 산의 외형·재질·충돌은 유지하고 그림자만 낮춘 복사본으로 대체합니다.
 [ExecuteAlways,DisallowMultipleComponent]
 public sealed class JcMountainShadows : MonoBehaviour {
  sealed class Entry {
   public MeshFilter source;
   public MeshRenderer original,proxy;
   public ShadowCastingMode mode;
   public Mesh mesh,sourceMesh;
   public Matrix4x4 matrix;
   public float height;
  }
  readonly List<Entry> entries=new List<Entry>();
  MaterialPropertyBlock block;
  MeshFilter[] sources=new MeshFilter[0];
  JcDayNightController owner;
  GameObject output;
  float height=-1;
  float leftHeight=-1;
  double nextScan;
  public int Count=>entries.Count;
  static bool Mountain(DecorativeObjectPlacement p)=>p.PrefabKey=="Mount01"||p.PrefabKey=="Mount02"||p.PrefabKey=="Mount03";
  void OnEnable(){owner=GetComponent<JcDayNightController>();ApplyNow();}
  void LateUpdate()=>ApplyNow();
  void OnDisable()=>Restore();
  void OnDestroy()=>Restore();
  static void Dispose(Object obj){if(obj==null)return;if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
  public void Restore(){
   foreach(var e in entries){if(e.original!=null)e.original.shadowCastingMode=e.mode;Dispose(e.mesh);}
   entries.Clear();sources=new MeshFilter[0];
   if(output!=null){output.SetActive(false);Dispose(output);output=null;}
   height=leftHeight=-1;nextScan=0;
  }
  public void ApplyNow(){
   if(owner==null)owner=GetComponent<JcDayNightController>();
   var scene=gameObject.scene;
   if(!isActiveAndEnabled||owner==null||!owner.isActiveAndEnabled||owner.Settings==null||
    (scene.path!=JcDayNightController.ScenePath&&scene.name!="TutorialExploreScene")){if(entries.Count>0)Restore();return;}
   float desired=Mathf.Clamp(owner.Settings.mountainShadowHeight,.05f,1);
   // JC | 2026-11-04 | 왼쪽 산의 추가 축소를 공통 비율과 분리하고, 두 비율이 모두1일 때 전체를 복원합니다.
   float desiredLeft=Mathf.Clamp(owner.Settings.leftMountainShadowHeight,.05f,1);
   if(desired>=.99999f&&desiredLeft>=.99999f){if(entries.Count>0)Restore();return;}
   if(block==null)block=new MaterialPropertyBlock();
   bool rebuild=!Mathf.Approximately(height,desired)||!Mathf.Approximately(leftHeight,desiredLeft);
   if(rebuild||Time.realtimeSinceStartupAsDouble>=nextScan){
    var found=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DecorativeObjectPlacement>(true))
     .Where(Mountain).SelectMany(p=>p.GetComponentsInChildren<MeshFilter>(true))
     .Where(f=>f.sharedMesh!=null&&f.GetComponent<MeshRenderer>()!=null).ToArray();
    rebuild|=!sources.SequenceEqual(found);
    if(rebuild){Restore();height=desired;leftHeight=desiredLeft;sources=found;
     foreach(var f in sources){var r=f.GetComponent<MeshRenderer>();if(r.shadowCastingMode==ShadowCastingMode.Off)continue;
      float ratio=f.GetComponentInParent<DecorativeObjectPlacement>().PrefabKey=="Mount03"?desiredLeft:desired;
      if(ratio>=.99999f)continue;
      if(!f.sharedMesh.isReadable){Debug.LogError("산 그림자 메시를 읽을 수 없습니다: "+f.name,f);continue;}
      if(output==null){output=new GameObject("JC_MountainShadowCopies"){hideFlags=HideFlags.DontSave};SceneManager.MoveGameObjectToScene(output,scene);}
      var go=new GameObject(f.name+" Shadow"){hideFlags=HideFlags.DontSave,layer=f.gameObject.layer};go.transform.SetParent(output.transform,false);
      var filter=go.AddComponent<MeshFilter>();var proxy=go.AddComponent<MeshRenderer>();proxy.shadowCastingMode=ShadowCastingMode.ShadowsOnly;proxy.receiveShadows=false;
      proxy.lightProbeUsage=LightProbeUsage.Off;proxy.reflectionProbeUsage=ReflectionProbeUsage.Off;proxy.allowOcclusionWhenDynamic=false;
      var e=new Entry{source=f,original=r,proxy=proxy,mode=r.shadowCastingMode,height=ratio};entries.Add(e);UpdateMesh(e);filter.sharedMesh=e.mesh;
      r.shadowCastingMode=ShadowCastingMode.Off;
     }
    }
    nextScan=Time.realtimeSinceStartupAsDouble+.5;
   }
   foreach(var e in entries){if(e.source==null||e.original==null){nextScan=0;continue;}
    if(e.sourceMesh!=e.source.sharedMesh||e.matrix!=e.source.transform.localToWorldMatrix){UpdateMesh(e);e.proxy.GetComponent<MeshFilter>().sharedMesh=e.mesh;}
    e.proxy.enabled=e.original.enabled&&e.original.gameObject.activeInHierarchy;
    e.proxy.forceRenderingOff=e.original.forceRenderingOff;e.proxy.gameObject.layer=e.original.gameObject.layer;
    e.proxy.renderingLayerMask=e.original.renderingLayerMask;e.proxy.sharedMaterials=e.original.sharedMaterials;
    e.original.GetPropertyBlock(block);e.proxy.SetPropertyBlock(block);
   }
  }
  void UpdateMesh(Entry e){
   Dispose(e.mesh);e.sourceMesh=e.source.sharedMesh;e.matrix=e.source.transform.localToWorldMatrix;
   e.mesh=Instantiate(e.sourceMesh);e.mesh.name=e.sourceMesh.name+" (낮은 그림자)";e.mesh.hideFlags=HideFlags.DontSave;
   var vertices=e.mesh.vertices;var normals=e.mesh.normals;var normalMatrix=e.matrix.inverse.transpose;
   // JC | 2026-11-04 | 원본 형상과 XZ 위치는 유지하고 산별 비율로 그림자 전용 높이와 법선만 변환합니다.
   for(int i=0;i<vertices.Length;i++){var v=e.matrix.MultiplyPoint3x4(vertices[i]);v.y*=e.height;vertices[i]=v;}
   for(int i=0;i<normals.Length;i++){var n=normalMatrix.MultiplyVector(normals[i]);n.y/=e.height;normals[i]=n.normalized;}
   e.mesh.vertices=vertices;e.mesh.normals=normals;e.mesh.RecalculateBounds();
  }
 }
}
